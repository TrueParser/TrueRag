using TrueRag.Core.Context;
using TrueRag.Core.Primitives;

namespace TrueRag.Api.Services;

public interface IDependencyReadinessEvaluator
{
    Task<Result<IReadOnlyDictionary<string, string>>> EvaluateAsync(CancellationToken cancellationToken = default);
}
