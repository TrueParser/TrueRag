using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Core.Abstractions;

public interface ILlmProvider
{
    string ProviderId { get; }

    Task<Result<LlmCompletionResponse>> CompleteAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<LlmCompletionChunk> StreamAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default);
}
