using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Core.Abstractions;

public interface IRetrievalService
{
    Task<Result<RetrievalResponse>> SearchVectorAsync(
        IRequestContext requestContext,
        RetrievalQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<RetrievalResponse>> SearchTextAsync(
        IRequestContext requestContext,
        RetrievalQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<RetrievalResponse>> SearchHybridAsync(
        IRequestContext requestContext,
        RetrievalQuery query,
        CancellationToken cancellationToken = default);
}