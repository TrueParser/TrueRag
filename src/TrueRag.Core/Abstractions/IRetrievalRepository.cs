using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Core.Abstractions;

public interface IRetrievalRepository
{
    Task<Result<RetrievalResponse>> QueryVectorAsync(
        IRequestContext requestContext,
        RetrievalQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<RetrievalResponse>> QueryTextAsync(
        IRequestContext requestContext,
        RetrievalQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<RetrievalResponse>> QueryHybridAsync(
        IRequestContext requestContext,
        RetrievalQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<RetrievalResponse>> ExpandByLogicalSectionAsync(
        IRequestContext requestContext,
        IReadOnlyCollection<StructuralExpansionSeed> seeds,
        int limit,
        CancellationToken cancellationToken = default);

    Task<Result<RetrievalResponse>> ExpandAdjacentChunksAsync(
        IRequestContext requestContext,
        IReadOnlyCollection<AdjacentExpansionSeed> seeds,
        int limit,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyCollection<RetrievedNode>>> GetNodesByIdsAsync(
        IRequestContext requestContext,
        IReadOnlyCollection<string> nodeIds,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyCollection<StructuralDiffResult>>> GetStructuralDiffsAsync(
        IRequestContext requestContext,
        IReadOnlyCollection<StructuralDiffRequest> requests,
        CancellationToken cancellationToken = default);
}
