using System.Text.Json;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;
using TrueRag.Ingestion.Admission;
using TrueRag.Ingestion.Configuration;
using TrueRag.Ingestion.Models;
using TrueRag.Ingestion.Normalization;
using TrueRag.Ingestion.Queue;
using TrueRag.Ingestion.Wal;

namespace TrueRag.Ingestion.Execution;

internal sealed class IngestionExecutionService : IIngestionExecutionService
{
    private readonly IIngestionNormalizer _normalizer;
    private readonly IIngestionAcceptanceLog _acceptanceLog;
    private readonly IQueuePublisher _queuePublisher;
    private readonly IIngestionRepository _ingestionRepository;
    private readonly IFamilyQueueDepthTracker _queueDepthTracker;
    private readonly IIngestionPressureTracker _pressureTracker;
    private readonly ICollectionEmbeddingModeResolver? _embeddingModeResolver;
    private readonly IEmbeddingProfileResolver? _embeddingProfileResolver;
    private readonly IActiveEmbeddingProfileStore? _activeProfileStore;
    private readonly SemaphoreSlim _syncGate;
    private readonly IngestionRuntimeOptions _runtimeOptions;
    private readonly QueueConfiguration _queueOptions;
    private readonly IngestionBackpressureOptions _backpressureOptions;

    public IngestionExecutionService(
        IIngestionNormalizer normalizer,
        IIngestionAcceptanceLog acceptanceLog,
        IQueuePublisher queuePublisher,
        IIngestionRepository ingestionRepository,
        IFamilyQueueDepthTracker queueDepthTracker,
        IIngestionPressureTracker pressureTracker,
        IEnumerable<ICollectionEmbeddingModeResolver> embeddingModeResolvers,
        IEnumerable<IEmbeddingProfileResolver> embeddingProfileResolvers,
        IEnumerable<IActiveEmbeddingProfileStore> activeProfileStores,
        Microsoft.Extensions.Options.IOptions<IngestionRuntimeOptions> runtimeOptions,
        Microsoft.Extensions.Options.IOptions<QueueConfiguration> queueOptions,
        Microsoft.Extensions.Options.IOptions<IngestionBackpressureOptions> backpressureOptions)
    {
        _normalizer = normalizer;
        _acceptanceLog = acceptanceLog;
        _queuePublisher = queuePublisher;
        _ingestionRepository = ingestionRepository;
        _queueDepthTracker = queueDepthTracker;
        _pressureTracker = pressureTracker;
        _embeddingModeResolver = embeddingModeResolvers.FirstOrDefault();
        _embeddingProfileResolver = embeddingProfileResolvers.FirstOrDefault();
        _activeProfileStore = activeProfileStores.FirstOrDefault();
        _runtimeOptions = runtimeOptions.Value;
        _queueOptions = queueOptions.Value;
        _backpressureOptions = backpressureOptions.Value;
        _syncGate = new SemaphoreSlim(Math.Max(1, _runtimeOptions.SyncMaxConcurrency));
    }

    public async Task<Result<IngestionJobMessage>> IngestAsyncBuffered(
        IRequestContext context,
        IngestionRequestDto payload,
        CancellationToken cancellationToken = default)
    {
        var asyncPrecomputedGuard = ValidateAsyncNoPrecomputedVectors(payload);
        if (asyncPrecomputedGuard.IsFailure)
        {
            return Result<IngestionJobMessage>.Failure(asyncPrecomputedGuard.Error!);
        }

        var embeddingIntent = ResolveEmbeddingIntent(payload);
        var mode = await ResolveModeAsync(context, cancellationToken);
        var modeValidation = ValidateModeContractForAsync(embeddingIntent, mode);
        if (modeValidation.IsFailure)
        {
            return Result<IngestionJobMessage>.Failure(modeValidation.Error!);
        }

        var descriptorValidation = await ValidatePrecomputedEmbeddingCompatibilityAsync(context, payload, embeddingIntent.UsesPrecomputedVectors, cancellationToken);
        if (descriptorValidation.IsFailure)
        {
            return Result<IngestionJobMessage>.Failure(descriptorValidation.Error!);
        }

        var scopedPayload = EnsureCollectionScope(context, payload);
        if (scopedPayload.IsFailure)
        {
            return Result<IngestionJobMessage>.Failure(scopedPayload.Error!);
        }

        payload = scopedPayload.Value!;
        var normalized = _normalizer.Normalize(payload);
        if (normalized.IsFailure)
        {
            return Result<IngestionJobMessage>.Failure(normalized.Error!);
        }

        var familyKey = ResolveFamilyKey(payload);
        var maxFamilyDepth = Math.Max(1, _backpressureOptions.MaxFamilyQueueDepth);
        if (!_queueDepthTracker.TryReserve(
                context.TenantId,
                context.AppId,
                context.CollectionId,
                familyKey,
                payload.DocumentId,
                maxFamilyDepth,
                out var currentDepth,
                out var reservedNew))
        {
            return Result<IngestionJobMessage>.Failure(new Error(
                "queue_depth_exhausted",
                $"Queue depth limit reached for tenant/app family. current_depth={currentDepth}, max_depth={maxFamilyDepth}",
                ErrorType.Unavailable));
        }

        var pressure = _pressureTracker.CaptureSnapshot();
        var shouldRejectForDrainPressure =
            currentDepth >= Math.Max(1, _backpressureOptions.MinDepthBeforeDrainRatioReject) &&
            pressure.AcceptedItemsPerSec + pressure.DrainedItemsPerSec > 0 &&
            pressure.DrainCapacityRatio >= _backpressureOptions.DrainCapacityRatioRejectThreshold;

        if (shouldRejectForDrainPressure)
        {
            if (reservedNew)
            {
                _queueDepthTracker.Release(context.TenantId, context.AppId, context.CollectionId, familyKey, payload.DocumentId);
            }

            return Result<IngestionJobMessage>.Failure(new Error(
                "wal_backpressure_high",
                $"Node is under WAL backpressure. drain_ratio={pressure.DrainCapacityRatio:F2}, depth={currentDepth}.",
                ErrorType.Unavailable));
        }

        var materialized = MaterializeResolvedPayload(payload, normalized.Value!, mode == CollectionEmbeddingMode.ExternalEmbedding ? "external_embedding" : "internal_embedding");

        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(materialized);
            await using var stream = new MemoryStream(bytes, writable: false);
            var append = await _acceptanceLog.AppendAsync(
                new IngestionWalRecordMetadata(
                    context.TenantId,
                    context.AppId,
                    context.CollectionId,
                    payload.DocumentId,
                    Guid.NewGuid().ToString("N"),
                    _runtimeOptions.NodeId,
                    embeddingIntent.RequiresInternalEmbeddingGeneration,
                    embeddingIntent.UsesPrecomputedVectors),
                stream,
                bytes.Length,
                cancellationToken);

            var message = new IngestionJobMessage(
                _runtimeOptions.NodeId,
                context.TenantId,
                context.AppId,
                context.CollectionId,
                context.UserId,
                context.Roles,
                context.AllowedDocumentGroups,
                append.WalPath,
                append.WalSegmentId,
                append.Offset,
                append.Length,
                embeddingIntent.RequiresInternalEmbeddingGeneration,
                embeddingIntent.UsesPrecomputedVectors);

            var topic = $"{_queueOptions.IngestSubjectBase}.{_runtimeOptions.NodeId}";
            await _queuePublisher.PublishAsync(topic, message, cancellationToken);
            _queueDepthTracker.MarkPublished(context.TenantId, context.AppId, context.CollectionId, familyKey, payload.DocumentId);
            _pressureTracker.RecordAccepted();
            return Result<IngestionJobMessage>.Success(message);
        }
        catch
        {
            if (reservedNew)
            {
                _queueDepthTracker.Release(context.TenantId, context.AppId, context.CollectionId, familyKey, payload.DocumentId);
            }

            throw;
        }
    }

    public async Task<Result> IngestSyncAsync(
        IRequestContext context,
        IngestionRequestDto payload,
        CancellationToken cancellationToken = default)
    {
        var syncVectorGuard = ValidateSyncPrecomputedVectors(payload);
        if (syncVectorGuard.IsFailure)
        {
            return syncVectorGuard;
        }

        var mode = await ResolveModeAsync(context, cancellationToken);
        if (mode == CollectionEmbeddingMode.InternalEmbedding)
        {
            return Result.Failure(new Error(
                "ingestion.sync_disabled_for_internal_embedding_mode",
                "Sync ingestion is disabled for collections configured for internal embedding mode.",
                ErrorType.Validation));
        }

        var descriptorValidation = await ValidatePrecomputedEmbeddingCompatibilityAsync(context, payload, true, cancellationToken);
        if (descriptorValidation.IsFailure)
        {
            return descriptorValidation;
        }

        var scopedPayload = EnsureCollectionScope(context, payload);
        if (scopedPayload.IsFailure)
        {
            return Result.Failure(scopedPayload.Error!);
        }

        payload = scopedPayload.Value!;
        var normalized = _normalizer.Normalize(payload);
        if (normalized.IsFailure)
        {
            return Result.Failure(normalized.Error!);
        }

        var materialized = MaterializeResolvedPayload(payload, normalized.Value!, "external_embedding");

        await _syncGate.WaitAsync(cancellationToken);
        try
        {
            return await _ingestionRepository.UpsertDocumentAsync(context, materialized, cancellationToken);
        }
        finally
        {
            _syncGate.Release();
        }
    }

    private static IngestionRequestDto MaterializeResolvedPayload(IngestionRequestDto payload, NormalizedIngestionDocument normalized, string embeddingModeTag)
        => payload with
        {
            Fidelity = normalized.FidelityLevel.ToString().ToLowerInvariant(),
            EmbeddingModeTag = embeddingModeTag
        };

    private static Result<IngestionRequestDto> EnsureCollectionScope(IRequestContext context, IngestionRequestDto payload)
    {
        if (string.IsNullOrWhiteSpace(payload.CollectionId))
        {
            return Result<IngestionRequestDto>.Success(payload with { CollectionId = context.CollectionId });
        }

        if (!string.Equals(payload.CollectionId, context.CollectionId, StringComparison.Ordinal))
        {
            return Result<IngestionRequestDto>.Failure(
                new Error("ingestion.collection_scope_mismatch", "Payload CollectionId does not match request context collection scope.", ErrorType.Validation));
        }

        return Result<IngestionRequestDto>.Success(payload);
    }

    private static string ResolveFamilyKey(IngestionRequestDto payload)
        => string.IsNullOrWhiteSpace(payload.DocumentGroupId)
            ? "_default"
            : payload.DocumentGroupId.Trim();

    private static Result ValidateSyncPrecomputedVectors(IngestionRequestDto payload)
    {
        if (payload.Chunks.Count == 0)
        {
            return Result.Failure(new Error(
                "ingestion.sync_precomputed_vectors_required",
                "Sync ingestion requires precomputed vectors for all chunks.",
                ErrorType.Validation));
        }

        foreach (var chunk in payload.Chunks)
        {
            if (chunk.Vector is null || chunk.Vector.Length == 0)
            {
                return Result.Failure(new Error(
                    "ingestion.sync_precomputed_vectors_required",
                    "Sync ingestion requires precomputed vectors for all chunks.",
                    ErrorType.Validation));
            }
        }

        return Result.Success();
    }

    private static (bool RequiresInternalEmbeddingGeneration, bool UsesPrecomputedVectors) ResolveEmbeddingIntent(IngestionRequestDto payload)
        => (true, false);

    private static Result ValidateAsyncNoPrecomputedVectors(IngestionRequestDto payload)
    {
        foreach (var chunk in payload.Chunks)
        {
            if (chunk.Vector is { Length: > 0 })
            {
                return Result.Failure(new Error(
                    "ingestion.async_precomputed_vectors_not_allowed",
                    "Async ingestion does not accept client-provided vectors; embeddings are generated by pipeline orchestration.",
                    ErrorType.Validation));
            }
        }

        return Result.Success();
    }

    private async Task<Result> ValidatePrecomputedEmbeddingCompatibilityAsync(
        IRequestContext context,
        IngestionRequestDto payload,
        bool usesPrecomputedVectors,
        CancellationToken cancellationToken)
    {
        if (!usesPrecomputedVectors || _embeddingProfileResolver is null)
        {
            return Result.Success();
        }

        var descriptor = await _embeddingProfileResolver.ResolveActiveDescriptorAsync(
            context.TenantId,
            context.AppId,
            context.CollectionId,
            cancellationToken);

        foreach (var chunk in payload.Chunks)
        {
            if (chunk.Vector.Length != descriptor.Dimensions)
            {
                return Result.Failure(new Error(
                    "ingestion.embedding_space_mismatch",
                    $"Chunk vector dimensions ({chunk.Vector.Length}) do not match active embedding descriptor dimensions ({descriptor.Dimensions}).",
                    ErrorType.Validation));
            }
        }

        if (!string.IsNullOrWhiteSpace(payload.PrecomputedEmbeddingProvider) &&
            !string.Equals(payload.PrecomputedEmbeddingProvider, descriptor.Provider, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(new Error(
                "ingestion.embedding_space_mismatch",
                $"Precomputed embedding provider '{payload.PrecomputedEmbeddingProvider}' does not match active embedding provider '{descriptor.Provider}'.",
                ErrorType.Validation));
        }

        if (!string.IsNullOrWhiteSpace(payload.PrecomputedEmbeddingModel) &&
            !string.Equals(payload.PrecomputedEmbeddingModel, descriptor.Model, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(new Error(
                "ingestion.embedding_space_mismatch",
                $"Precomputed embedding model '{payload.PrecomputedEmbeddingModel}' does not match active embedding model '{descriptor.Model}'.",
                ErrorType.Validation));
        }

        if (_activeProfileStore is not null && payload.Chunks.Count > 0)
        {
            var firstVectorLength = payload.Chunks.First().Vector.Length;
            var provider = payload.PrecomputedEmbeddingProvider ?? descriptor.Provider;
            var model = payload.PrecomputedEmbeddingModel ?? descriptor.Model;
            var compatibility = await _activeProfileStore.CheckCompatibilityAsync(
                context.TenantId,
                context.AppId,
                context.CollectionId,
                provider,
                model,
                firstVectorLength,
                cancellationToken);
            if (!compatibility.IsCompatible)
            {
                return Result.Failure(new Error(
                    "ingestion.embedding_space_mismatch",
                    $"Precomputed vectors are incompatible with active profile for this scope ({compatibility.Reason}).",
                    ErrorType.Validation));
            }
        }

        return Result.Success();
    }

    private async Task<CollectionEmbeddingMode> ResolveModeAsync(IRequestContext context, CancellationToken cancellationToken)
    {
        if (_embeddingModeResolver is null)
        {
            return CollectionEmbeddingMode.ExternalEmbedding;
        }

        return await _embeddingModeResolver.ResolveModeAsync(context, cancellationToken);
    }

    private static Result ValidateModeContractForAsync(
        (bool RequiresInternalEmbeddingGeneration, bool UsesPrecomputedVectors) intent,
        CollectionEmbeddingMode mode)
    {
        if (intent.UsesPrecomputedVectors)
        {
            return Result.Failure(new Error(
                "ingestion.async_precomputed_vectors_not_allowed",
                "Async ingestion does not accept client-provided vectors; embeddings are generated by pipeline orchestration.",
                ErrorType.Validation));
        }

        return Result.Success();
    }
}
