using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;
using TrueRag.Ingestion.Execution;

namespace TrueRag.Api.Services;

internal sealed class IngestionApiService : IIngestionApiService
{
    private readonly IIngestionExecutionService _executionService;

    public IngestionApiService(IIngestionExecutionService executionService)
    {
        _executionService = executionService;
    }

    public async Task<Result<IngestAsyncResult>> IngestAsync(IRequestContext context, IngestionRequestDto payload, CancellationToken cancellationToken = default)
    {
        var result = await _executionService.IngestAsyncBuffered(context, payload, cancellationToken);
        if (result.IsFailure)
        {
            return Result<IngestAsyncResult>.Failure(result.Error!);
        }

        var value = result.Value!;
        return Result<IngestAsyncResult>.Success(new IngestAsyncResult(
            value.NodeId,
            value.TenantId,
            value.AppId,
            value.WalPath,
            value.WalSegmentId,
            value.WalOffset,
            value.WalLength));
    }

    public Task<Result> IngestSync(IRequestContext context, IngestionRequestDto payload, CancellationToken cancellationToken = default)
        => _executionService.IngestSyncAsync(context, payload, cancellationToken);
}
