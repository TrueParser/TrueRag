using System.Text.Json;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;
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
    private readonly SemaphoreSlim _syncGate;
    private readonly IngestionRuntimeOptions _runtimeOptions;

    public IngestionExecutionService(
        IIngestionNormalizer normalizer,
        IIngestionAcceptanceLog acceptanceLog,
        IQueuePublisher queuePublisher,
        IIngestionRepository ingestionRepository,
        Microsoft.Extensions.Options.IOptions<IngestionRuntimeOptions> runtimeOptions)
    {
        _normalizer = normalizer;
        _acceptanceLog = acceptanceLog;
        _queuePublisher = queuePublisher;
        _ingestionRepository = ingestionRepository;
        _runtimeOptions = runtimeOptions.Value;
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

        var materialized = MaterializeResolvedPayload(payload, normalized.Value!);

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

        var topic = $"TrueRAG.Job.Ingest.{_runtimeOptions.NodeId}";
        await _queuePublisher.PublishAsync(topic, message, cancellationToken);
        return Result<IngestionJobMessage>.Success(message);
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
}
