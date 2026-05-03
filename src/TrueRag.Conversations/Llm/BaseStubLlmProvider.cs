using TrueRag.Core.Abstractions;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Conversations.Llm;

internal abstract class BaseStubLlmProvider : ILlmProvider
{
    private readonly ILlmResponseParser _parser;

    protected BaseStubLlmProvider(ILlmResponseParser parser)
    {
        _parser = parser;
    }

    public abstract string ProviderId { get; }

    public Task<Result<LlmCompletionResponse>> CompleteAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var raw = BuildRawResponse(request);
        var estimate = request.Messages.Sum(static message => PromptAssembly.PromptAssemblyService.EstimateTokens(message.Content));
        var parsed = _parser.Parse(ProviderId, raw, estimate);
        return Task.FromResult(Result<LlmCompletionResponse>.Success(parsed));
    }

    public async IAsyncEnumerable<LlmCompletionChunk> StreamAsync(
        LlmCompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var full = await CompleteAsync(request, cancellationToken);
        if (full.IsFailure)
        {
            yield break;
        }

        var text = full.Value!.Text;
        var chunkSize = 32;
        for (var i = 0; i < text.Length; i += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var delta = text.Substring(i, Math.Min(chunkSize, text.Length - i));
            yield return new LlmCompletionChunk(delta, IsFinal: false);
        }

        yield return new LlmCompletionChunk(
            DeltaText: string.Empty,
            IsFinal: true,
            ToolCalls: full.Value.ToolCalls,
            LlmCertainty: full.Value.LlmCertainty);
    }

    protected abstract string BuildRawResponse(LlmCompletionRequest request);
}
