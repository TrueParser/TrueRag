using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Api.Services;

public interface IEmbeddingProfileApiService
{
    Task<Result<ActiveEmbeddingProfileRecord?>> GetCurrentAsync(IRequestContext context, CancellationToken cancellationToken = default);

    Task<Result<EmbeddingProfileTransitionRecord?>> GetPendingAsync(IRequestContext context, CancellationToken cancellationToken = default);

    Task<Result<EmbeddingProfileMigrationReadiness>> GetReadinessAsync(IRequestContext context, CancellationToken cancellationToken = default);

    Task<Result<EmbeddingProfileTransitionRecord>> ProposeAsync(IRequestContext context, EmbeddingProfileTransitionProposal proposal, CancellationToken cancellationToken = default);

    Task<Result<EmbeddingProfileTransitionRecord>> MarkReembeddingCompletedAsync(string transitionId, CancellationToken cancellationToken = default);

    Task<Result<EmbeddingProfileTransitionRecord>> ActivateAsync(string transitionId, CancellationToken cancellationToken = default);

    Task<Result<EmbeddingProfileTransitionRecord>> RollbackAsync(string transitionId, CancellationToken cancellationToken = default);
}

