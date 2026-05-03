using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Ingestion.Execution;

public interface IIngestionExecutionService
{
    Task<Result<Queue.IngestionJobMessage>> IngestAsyncBuffered(
        IRequestContext context,
        IngestionRequestDto payload,
        CancellationToken cancellationToken = default);

    Task<Result> IngestSyncAsync(
        IRequestContext context,
        IngestionRequestDto payload,
        CancellationToken cancellationToken = default);
}