using TrueRag.Core.Models;

namespace TrueRag.Core.Abstractions;

public interface IEmbeddingProvider
{
    string Name { get; }

    EmbeddingProviderCapabilities Capabilities { get; }

    Task<EmbedTextResult> EmbedTextAsync(EmbedTextRequest request, CancellationToken cancellationToken = default);

    Task<EmbedBatchResult> EmbedBatchAsync(EmbedBatchRequest request, CancellationToken cancellationToken = default);
}
