using TrueRag.Core.Primitives;

namespace TrueRag.Core.Abstractions;

public interface ILlmProviderFactory
{
    Result<ILlmProvider> Resolve(string? providerId);
}
