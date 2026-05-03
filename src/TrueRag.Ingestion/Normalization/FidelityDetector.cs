using TrueRag.Ingestion.Configuration;
using TrueRag.Ingestion.Models;

namespace TrueRag.Ingestion.Normalization;

internal static class FidelityDetector
{
    public static FidelityLevel Resolve(string? fidelityValue, IReadOnlyCollection<NormalizedNode> nodes, IngestionFidelityOptions options)
    {
        var requested = string.IsNullOrWhiteSpace(fidelityValue)
            ? options.DefaultMode
            : fidelityValue;

        if (options.AllowExplicitOverride)
        {
            if (string.Equals(requested, "high", StringComparison.OrdinalIgnoreCase))
            {
                return FidelityLevel.High;
            }

            if (string.Equals(requested, "standard", StringComparison.OrdinalIgnoreCase))
            {
                return FidelityLevel.Standard;
            }
        }

        var hasHierarchy = nodes.Any(static node => node.HasHierarchyMetadata);
        var hasProvenance = nodes.Any(static node => node.HasProvenanceMetadata);
        var hasTables = nodes.Any(static node => node.HasTableMetadata);

        return hasHierarchy || hasProvenance || hasTables
            ? FidelityLevel.High
            : FidelityLevel.Standard;
    }
}