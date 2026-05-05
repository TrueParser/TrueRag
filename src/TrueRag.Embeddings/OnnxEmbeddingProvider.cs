using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Models;
using TrueRag.Core.Validation;
using TrueRag.Embeddings.Configuration;
using TrueRag.Embeddings.Onnx;

namespace TrueRag.Embeddings;

internal sealed class OnnxEmbeddingProvider : IEmbeddingProvider
{
    private static readonly EmbeddingProviderCapabilities ProviderCapabilities = new(
        EmbeddingCapabilityFlags.SingleText | EmbeddingCapabilityFlags.BatchText | EmbeddingCapabilityFlags.InternalExecution,
        16,
        [EmbeddingDistanceMetric.Cosine]);

    private readonly IOptionsMonitor<OnnxEmbeddingOptions> _optionsMonitor;
    private readonly IOptionsMonitor<OnnxProfileSelectionOptions> _profileSelectionOptions;
    private readonly IEmbeddingProfileResolver _profileResolver;
    private readonly IOnnxModelProfileRegistry _profileRegistry;
    private readonly OnnxTextPreprocessor _preprocessor;
    private readonly OnnxModelArtifactValidator _artifactValidator;
    private readonly IOnnxEmbeddingExecutor _executor;
    private readonly ILogger<OnnxEmbeddingProvider> _logger;

    public OnnxEmbeddingProvider(
        IOptionsMonitor<OnnxEmbeddingOptions> optionsMonitor,
        IOptionsMonitor<OnnxProfileSelectionOptions> profileSelectionOptions,
        IEmbeddingProfileResolver profileResolver,
        IOnnxModelProfileRegistry profileRegistry,
        OnnxTextPreprocessor preprocessor,
        OnnxModelArtifactValidator artifactValidator,
        IOnnxEmbeddingExecutor executor,
        ILogger<OnnxEmbeddingProvider> logger)
    {
        _optionsMonitor = optionsMonitor;
        _profileSelectionOptions = profileSelectionOptions;
        _profileResolver = profileResolver;
        _profileRegistry = profileRegistry;
        _preprocessor = preprocessor;
        _artifactValidator = artifactValidator;
        _executor = executor;
        _logger = logger;
    }

    public string Name => _optionsMonitor.CurrentValue.ProviderName;

    public EmbeddingProviderCapabilities Capabilities
    {
        get
        {
            var options = _optionsMonitor.CurrentValue;
            return ProviderCapabilities with { MaxBatchSize = Math.Max(1, options.MaxBatchSize) };
        }
    }

    public async Task<EmbedTextResult> EmbedTextAsync(EmbedTextRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EmbeddingContractValidator.ValidateRequest(request);

        var options = _optionsMonitor.CurrentValue;
        var descriptor = await ResolveDescriptorAsync(request.Context, cancellationToken);
        var profile = _profileRegistry.GetRequiredProfileByModelId(descriptor.Model);

        EnsureInitialized(options, profile, descriptor);

        using var timeoutCts = CreateTimeoutToken(options, cancellationToken);
        var normalized = _preprocessor.Normalize(request.Input.Text, descriptor.MaxTokens);
        var vector = await _executor.GenerateVectorAsync(normalized, descriptor.Dimensions, timeoutCts.Token);

        return new EmbedTextResult(
            vector,
            descriptor,
            new EmbeddingUsage(CountTokens(normalized)));
    }

    public async Task<EmbedBatchResult> EmbedBatchAsync(EmbedBatchRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EmbeddingContractValidator.ValidateRequest(request);

        var options = _optionsMonitor.CurrentValue;
        var descriptor = await ResolveDescriptorAsync(request.Context, cancellationToken);
        var profile = _profileRegistry.GetRequiredProfileByModelId(descriptor.Model);

        EnsureInitialized(options, profile, descriptor);

        using var timeoutCts = CreateTimeoutToken(options, cancellationToken);

        var maxBatch = Math.Max(1, options.MaxBatchSize);
        var vectors = new List<float[]>(request.Inputs.Count);
        var totalTokens = 0;

        foreach (var batch in request.Inputs.Chunk(maxBatch))
        {
            timeoutCts.Token.ThrowIfCancellationRequested();

            foreach (var input in batch)
            {
                var normalized = _preprocessor.Normalize(input.Text, descriptor.MaxTokens);
                totalTokens += CountTokens(normalized);
                vectors.Add(await _executor.GenerateVectorAsync(normalized, descriptor.Dimensions, timeoutCts.Token));
            }

            await Task.Yield();
        }

        return new EmbedBatchResult(vectors, descriptor, new EmbeddingUsage(totalTokens));
    }

    internal void Warmup(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = _optionsMonitor.CurrentValue;
        var defaultProfileName = _profileSelectionOptions.CurrentValue.DefaultProfileName;
        var profile = _profileRegistry.GetRequiredProfile(defaultProfileName);
        var descriptor = new EmbeddingModelDescriptor(
            options.ProviderName,
            profile.ModelId,
            profile.Dimensions,
            profile.MaxTokens,
            profile.DistanceMetric);

        EnsureInitialized(options, profile, descriptor);

        var probeText = _preprocessor.Normalize("warmup probe", descriptor.MaxTokens);
        _ = _executor.GenerateVectorAsync(probeText, descriptor.Dimensions, cancellationToken).GetAwaiter().GetResult();

        _logger.LogInformation("ONNX embedding warmup succeeded for model {ModelId}.", descriptor.Model);
    }

    private async Task<EmbeddingModelDescriptor> ResolveDescriptorAsync(EmbeddingGenerationContext? context, CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        if (context is null
            || string.IsNullOrWhiteSpace(context.TenantId)
            || string.IsNullOrWhiteSpace(context.AppId)
            || string.IsNullOrWhiteSpace(context.CollectionId))
        {
            var defaultProfileName = _profileSelectionOptions.CurrentValue.DefaultProfileName;
            var profile = _profileRegistry.GetRequiredProfile(defaultProfileName);
            return new EmbeddingModelDescriptor(options.ProviderName, profile.ModelId, profile.Dimensions, profile.MaxTokens, profile.DistanceMetric);
        }

        var resolved = await _profileResolver.ResolveActiveDescriptorAsync(
            context.TenantId,
            context.AppId,
            context.CollectionId,
            cancellationToken);

        return resolved with { Provider = options.ProviderName };
    }

    private void EnsureInitialized(OnnxEmbeddingOptions options, OnnxModelProfile profile, EmbeddingModelDescriptor descriptor)
    {
        var effectiveOptions = new OnnxEmbeddingOptions
        {
            ProviderName = options.ProviderName,
            ModelId = options.ModelId,
            Dimensions = options.Dimensions,
            MaxTokens = options.MaxTokens,
            DistanceMetric = options.DistanceMetric,
            ModelArtifactsPath = !string.IsNullOrWhiteSpace(options.ModelArtifactsPath)
                ? options.ModelArtifactsPath
                : profile.ModelArtifactsPath ?? string.Empty,
            ModelFileName = !string.IsNullOrWhiteSpace(options.ModelFileName)
                ? options.ModelFileName
                : profile.ModelFileName ?? "model.onnx",
            MaxBatchSize = options.MaxBatchSize,
            EnableWarmup = options.EnableWarmup,
            WarmupTimeoutSeconds = options.WarmupTimeoutSeconds,
            InferenceTimeoutSeconds = options.InferenceTimeoutSeconds
        };

        _ = _artifactValidator.ValidateAndGetModelPath(effectiveOptions);

        if (descriptor.Dimensions <= 0)
        {
            throw new InvalidOperationException("Embeddings:Onnx descriptor dimensions must be greater than zero.");
        }

        if (descriptor.MaxTokens <= 0)
        {
            throw new InvalidOperationException("Embeddings:Onnx descriptor max tokens must be greater than zero.");
        }

        if (options.InferenceTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Embeddings:Onnx:InferenceTimeoutSeconds must be greater than zero.");
        }
    }

    private static CancellationTokenSource CreateTimeoutToken(OnnxEmbeddingOptions options, CancellationToken cancellationToken)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(TimeSpan.FromSeconds(options.InferenceTimeoutSeconds));
        return linkedCts;
    }

    private static int CountTokens(string text) => string.IsNullOrWhiteSpace(text)
        ? 0
        : text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
}
