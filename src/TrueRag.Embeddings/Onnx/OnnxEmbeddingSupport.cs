using System.Globalization;
using System.Text;
using TrueRag.Core.Models;
using TrueRag.Embeddings.Configuration;

namespace TrueRag.Embeddings.Onnx;

internal static class OnnxEmbeddingDefaults
{
    public static EmbeddingModelDescriptor CreateDescriptor(OnnxEmbeddingOptions options)
    {
        return new EmbeddingModelDescriptor(
            options.ProviderName,
            options.ModelId,
            options.Dimensions,
            options.MaxTokens,
            EmbeddingDistanceMetric.Cosine);
    }
}

internal sealed class OnnxTextPreprocessor
{
    public string Normalize(string input, int maxTokens)
    {
        ArgumentNullException.ThrowIfNull(input);

        var normalized = input.Normalize(NormalizationForm.FormKC).Replace("\r", " ").Replace("\n", " ").Trim();
        var compact = string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (string.IsNullOrWhiteSpace(compact) || maxTokens <= 0)
        {
            return compact;
        }

        var tokens = compact.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length <= maxTokens)
        {
            return compact;
        }

        return string.Join(' ', tokens.Take(maxTokens));
    }
}

internal sealed class OnnxModelArtifactValidator
{
    public string ValidateAndGetModelPath(OnnxEmbeddingOptions options)
    {
        var root = options.ModelArtifactsPath;
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException("Embeddings:Onnx:ModelArtifactsPath is required.");
        }

        var modelPath = Path.Combine(root, options.ModelFileName);

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"ONNX model file was not found at '{modelPath}'.", modelPath);
        }

        var fileInfo = new FileInfo(modelPath);
        if (fileInfo.Length == 0)
        {
            throw new InvalidOperationException($"ONNX model file is empty at '{modelPath}'.");
        }

        return modelPath;
    }
}
