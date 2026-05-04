using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Api.Services;

public interface IIngestionApiService
{
    Task<Result<IngestAsyncResult>> IngestAsync(IRequestContext context, IngestionRequestDto payload, CancellationToken cancellationToken = default);

    Task<Result> IngestSync(IRequestContext context, IngestionRequestDto payload, CancellationToken cancellationToken = default);
}

public sealed record IngestAsyncResult(
    string NodeId,
    string TenantId,
    string AppId,
    string CollectionId,
    string WalPath,
    string WalSegmentId,
    long WalOffset,
    long WalLength);
