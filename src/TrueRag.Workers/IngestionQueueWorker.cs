using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Ingestion.Admission;
using TrueRag.Ingestion.Configuration;
using TrueRag.Ingestion.Queue;
using TrueRag.Ingestion.Wal;

namespace TrueRag.Workers;

internal sealed class IngestionQueueWorker : BackgroundService
{
    private readonly IQueueSubscriber _queueSubscriber;
    private readonly IIngestionWalReader _walReader;
    private readonly IIngestionRepository _ingestionRepository;
    private readonly IngestionRuntimeOptions _options;
    private readonly QueueConfiguration _queueOptions;
    private readonly ILogger<IngestionQueueWorker> _logger;
    private readonly Channel<QueuedJob> _channel = Channel.CreateUnbounded<QueuedJob>();
    private readonly IFamilyQueueDepthTracker _queueDepthTracker;
    private readonly IIngestionPressureTracker _pressureTracker;

    public IngestionQueueWorker(
        IQueueSubscriber queueSubscriber,
        IIngestionWalReader walReader,
        IIngestionRepository ingestionRepository,
        IFamilyQueueDepthTracker queueDepthTracker,
        IIngestionPressureTracker pressureTracker,
        IOptions<IngestionRuntimeOptions> options,
        IOptions<QueueConfiguration> queueOptions,
        ILogger<IngestionQueueWorker> logger)
    {
        _queueSubscriber = queueSubscriber;
        _walReader = walReader;
        _ingestionRepository = ingestionRepository;
        _queueDepthTracker = queueDepthTracker;
        _pressureTracker = pressureTracker;
        _options = options.Value;
        _queueOptions = queueOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var topic = $"{_queueOptions.IngestSubjectBase}.{_options.NodeId}";
        var consumerGroup = $"truerag-ingest-{_options.NodeId}";

        var batchingTask = ProcessBatchesAsync(stoppingToken);

        var subscribeTask = _queueSubscriber.SubscribeAsync<IngestionJobMessage>(
            topic,
            consumerGroup,
            async (job, cancellationToken) =>
            {
                var pending = new QueuedJob(job);
                await _channel.Writer.WriteAsync(pending, cancellationToken);
                return await pending.Completion.Task.WaitAsync(cancellationToken);
            },
            stoppingToken);

        await Task.WhenAll(subscribeTask, batchingTask);
    }

    private async Task ProcessBatchesAsync(CancellationToken stoppingToken)
    {
        var batchSize = Math.Max(1, _options.WorkerBatchSize);
        var batchWait = TimeSpan.FromMilliseconds(Math.Max(50, _options.WorkerBatchWaitMs));

        var buffer = new List<QueuedJob>(batchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                while (buffer.Count < batchSize && _channel.Reader.TryRead(out var queued))
                {
                    buffer.Add(queued);
                }

                if (buffer.Count == 0)
                {
                    var next = await _channel.Reader.ReadAsync(stoppingToken);
                    buffer.Add(next);
                }

                var waitUntil = DateTime.UtcNow + batchWait;
                while (buffer.Count < batchSize && DateTime.UtcNow < waitUntil)
                {
                    if (_channel.Reader.TryRead(out var queued))
                    {
                        buffer.Add(queued);
                        continue;
                    }

                    await Task.Delay(10, stoppingToken);
                }

                foreach (var queued in buffer)
                {
                    var success = await ProcessSingleAsync(queued.Job, stoppingToken);
                    queued.Completion.TrySetResult(success);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch processing loop failed.");
                foreach (var queued in buffer)
                {
                    queued.Completion.TrySetResult(false);
                }
            }
            finally
            {
                buffer.Clear();
            }
        }
    }

    private async Task<bool> ProcessSingleAsync(IngestionJobMessage job, CancellationToken cancellationToken)
    {
        IngestionRequestDto? payload = null;
        try
        {
            await using var payloadStream = await _walReader.OpenPayloadAsync(
                job.NodeId,
                job.WalPath,
                job.WalSegmentId,
                job.WalOffset,
                job.WalLength,
                cancellationToken);

            payload = await System.Text.Json.JsonSerializer.DeserializeAsync<IngestionRequestDto>(payloadStream, cancellationToken: cancellationToken);
            if (payload is null)
            {
                return true;
            }

            var context = new RequestContext(job.TenantId, job.AppId, job.UserId, job.Roles, job.AllowedDocumentGroups, job.CollectionId);
            var result = await _ingestionRepository.UpsertDocumentAsync(context, payload, cancellationToken);
            if (result.IsSuccess)
            {
                WriteCompletionMarker(job.WalPath, job.WalOffset);
            }

            return result.IsSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed processing ingestion job.");
            return false;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(payload?.DocumentId))
            {
                var familyKey = string.IsNullOrWhiteSpace(payload.DocumentGroupId)
                    ? "_default"
                    : payload.DocumentGroupId.Trim();
                _queueDepthTracker.MarkTerminal(job.TenantId, job.AppId, job.CollectionId, familyKey, payload.DocumentId);
                _pressureTracker.RecordDrained();
            }
        }
    }

    private static void WriteCompletionMarker(string walPath, long offset)
    {
        var markerPath = $"{walPath}.completed.{offset}";
        File.WriteAllText(markerPath, "ok");
    }

    private sealed class QueuedJob
    {
        public QueuedJob(IngestionJobMessage job)
        {
            Job = job;
            Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public IngestionJobMessage Job { get; }

        public TaskCompletionSource<bool> Completion { get; }
    }
}
