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
                _queueDepthTracker.Release(context.TenantId, context.AppId, familyKey, payload.DocumentId);
            }

            return Result<IngestionJobMessage>.Failure(new Error(
                "wal_backpressure_high",
                $"Node is under WAL backpressure. drain_ratio={pressure.DrainCapacityRatio:F2}, depth={currentDepth}.",
                ErrorType.Unavailable));
        }

        var materialized = MaterializeResolvedPayload(payload, normalized.Value!);

        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(materialized);
            await using var stream = new MemoryStream(bytes, writable: false);
            var append = await _acceptanceLog.AppendAsync(
                new IngestionWalRecordMetadata(
                    context.TenantId,
                    context.AppId,
                    payload.DocumentId,
                    Guid.NewGuid().ToString("N"),
                    _runtimeOptions.NodeId),
                stream,
                bytes.Length,
                cancellationToken);

            var message = new IngestionJobMessage(
                _runtimeOptions.NodeId,
                context.TenantId,
                context.AppId,
                context.UserId,
                context.Roles,
                context.AllowedDocumentGroups,
                append.WalPath,
                append.WalSegmentId,
                append.Offset,
                append.Length);

            var topic = $"{_queueOptions.IngestSubjectBase}.{_runtimeOptions.NodeId}";
            await _queuePublisher.PublishAsync(topic, message, cancellationToken);
            _queueDepthTracker.MarkPublished(context.TenantId, context.AppId, familyKey, payload.DocumentId);
            _pressureTracker.RecordAccepted();
            return Result<IngestionJobMessage>.Success(message);
        }
        catch
        {
            if (reservedNew)
            {
                _queueDepthTracker.Release(context.TenantId, context.AppId, familyKey, payload.DocumentId);
            }

            throw;
        }
    }

    public async Task<Result> IngestSyncAsync(
        IRequestContext context,
        IngestionRequestDto payload,
        CancellationToken cancellationToken = default)
    {
        var normalized = _normalizer.Normalize(payload);
        if (normalized.IsFailure)
        {
            return Result.Failure(normalized.Error!);
        }

        var materialized = MaterializeResolvedPayload(payload, normalized.Value!);

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

    private static IngestionRequestDto MaterializeResolvedPayload(IngestionRequestDto payload, NormalizedIngestionDocument normalized)
        => payload with
        {
            Fidelity = normalized.FidelityLevel.ToString().ToLowerInvariant()
        };

    private static string ResolveFamilyKey(IngestionRequestDto payload)
        => string.IsNullOrWhiteSpace(payload.DocumentGroupId)
            ? "_default"
            : payload.DocumentGroupId.Trim();
}
