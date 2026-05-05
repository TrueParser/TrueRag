using TrueRag.Core.Models;

namespace TrueRag.Core.Validation;

public static class EmbeddingContractValidator
{
    public static void ValidateDescriptor(EmbeddingModelDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (string.IsNullOrWhiteSpace(descriptor.Provider))
        {
            throw new ArgumentException("Embedding provider is required.", nameof(descriptor));
        }

        if (string.IsNullOrWhiteSpace(descriptor.Model))
        {
            throw new ArgumentException("Embedding model is required.", nameof(descriptor));
        }

        if (descriptor.Dimensions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), "Embedding dimensions must be greater than zero.");
        }

        if (descriptor.MaxTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), "Embedding max tokens must be greater than zero.");
        }

        if (!Enum.IsDefined(descriptor.DistanceMetric))
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), "Embedding distance metric is not supported.");
        }
    }

    public static void ValidateRequest(EmbedTextRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Input);
        ValidateDescriptor(request.Model);

        if (string.IsNullOrWhiteSpace(request.Input.Text))
        {
            throw new ArgumentException("Embedding input text is required.", nameof(request));
        }
    }

    public static void ValidateRequest(EmbedBatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Inputs);
        ValidateDescriptor(request.Model);

        if (request.Inputs.Count == 0)
        {
            throw new ArgumentException("At least one embedding input is required.", nameof(request));
        }

        foreach (var input in request.Inputs)
        {
            if (input is null || string.IsNullOrWhiteSpace(input.Text))
            {
                throw new ArgumentException("All embedding batch inputs must include non-empty text.", nameof(request));
            }
        }
    }
}
