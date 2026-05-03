using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Api.Services;

internal sealed class RetrievalApiService : IRetrievalApiService
{
    private readonly IRetrievalService _retrievalService;

    public RetrievalApiService(IRetrievalService retrievalService)
    {
        _retrievalService = retrievalService;
    }

    public Task<Result<RetrievalResponse>> SearchVector(IRequestContext context, RetrievalQuery query, CancellationToken cancellationToken = default)
        => _retrievalService.SearchVectorAsync(context, query, cancellationToken);

    public Task<Result<RetrievalResponse>> SearchText(IRequestContext context, RetrievalQuery query, CancellationToken cancellationToken = default)
        => _retrievalService.SearchTextAsync(context, query, cancellationToken);

    public Task<Result<RetrievalResponse>> SearchHybrid(IRequestContext context, RetrievalQuery query, CancellationToken cancellationToken = default)
        => _retrievalService.SearchHybridAsync(context, query, cancellationToken);
}
