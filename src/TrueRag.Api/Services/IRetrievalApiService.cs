using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Api.Services;

public interface IRetrievalApiService
{
    Task<Result<RetrievalResponse>> SearchVector(IRequestContext context, RetrievalQuery query, CancellationToken cancellationToken = default);

    Task<Result<RetrievalResponse>> SearchText(IRequestContext context, RetrievalQuery query, CancellationToken cancellationToken = default);

    Task<Result<RetrievalResponse>> SearchHybrid(IRequestContext context, RetrievalQuery query, CancellationToken cancellationToken = default);
}
