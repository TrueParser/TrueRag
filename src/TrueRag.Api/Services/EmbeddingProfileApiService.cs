using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Api.Services;

internal sealed class EmbeddingProfileApiService(
    IActiveEmbeddingProfileStore store,
    IEmbeddingProfileGovernanceService governance) : IEmbeddingProfileApiService
{
    public async Task<Result<ActiveEmbeddingProfileRecord?>> GetCurrentAsync(IRequestContext context, CancellationToken cancellationToken = default)
        => Result<ActiveEmbeddingProfileRecord?>.Success(await store.GetActiveAsync(context.TenantId, context.AppId, context.CollectionId, cancellationToken));

    public async Task<Result<EmbeddingProfileTransitionRecord?>> GetPendingAsync(IRequestContext context, CancellationToken cancellationToken = default)
        => Result<EmbeddingProfileTransitionRecord?>.Success(await governance.GetPendingTransitionAsync(context.TenantId, context.AppId, context.CollectionId, cancellationToken));

    public async Task<Result<EmbeddingProfileMigrationReadiness>> GetReadinessAsync(IRequestContext context, CancellationToken cancellationToken = default)
        => Result<EmbeddingProfileMigrationReadiness>.Success(await governance.GetMigrationReadinessAsync(context.TenantId, context.AppId, context.CollectionId, cancellationToken));

    public Task<Result<EmbeddingProfileTransitionRecord>> ProposeAsync(
        IRequestContext context,
        EmbeddingProfileTransitionProposal proposal,
        CancellationToken cancellationToken = default)
        => Execute(async () =>
        {
            var scoped = proposal with
            {
                TenantId = context.TenantId,
                AppId = context.AppId,
                CollectionId = context.CollectionId
            };
            return await governance.ProposeTransitionAsync(scoped, cancellationToken);
        });

    public Task<Result<EmbeddingProfileTransitionRecord>> MarkReembeddingCompletedAsync(string transitionId, CancellationToken cancellationToken = default)
        => Execute(() => governance.MarkReembeddingCompletedAsync(transitionId, cancellationToken));

    public Task<Result<EmbeddingProfileTransitionRecord>> ActivateAsync(string transitionId, CancellationToken cancellationToken = default)
        => Execute(() => governance.ActivateTransitionAsync(transitionId, cancellationToken));

    public Task<Result<EmbeddingProfileTransitionRecord>> RollbackAsync(string transitionId, CancellationToken cancellationToken = default)
        => Execute(() => governance.RollbackTransitionAsync(transitionId, cancellationToken));

    private static async Task<Result<EmbeddingProfileTransitionRecord>> Execute(Func<Task<EmbeddingProfileTransitionRecord>> action)
    {
        try
        {
            return Result<EmbeddingProfileTransitionRecord>.Success(await action());
        }
        catch (InvalidOperationException ex)
        {
            return Result<EmbeddingProfileTransitionRecord>.Failure(new Error(
                "embedding_profile_transition_invalid",
                ex.Message,
                ErrorType.Validation));
        }
    }
}

