using Microsoft.Extensions.Options;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;
using TrueRag.Ingestion.Adapters;
using TrueRag.Ingestion.Configuration;
using TrueRag.Ingestion.Models;
using TrueRag.Ingestion.Normalization;

namespace TrueRag.Ingestion.Adapters;

internal sealed class CanonicalIngestionPayloadAdapter : IIngestionPayloadAdapter<IngestionRequestDto>
{
    private readonly IngestionFidelityOptions _fidelityOptions;

    public CanonicalIngestionPayloadAdapter(IOptions<IngestionFidelityOptions> fidelityOptions)
    {
        _fidelityOptions = fidelityOptions.Value;
    }

    public Result<NormalizedIngestionDocument> Map(IngestionRequestDto payload)
    {
        var validation = IngestionPayloadValidator.Validate(payload);
        if (validation.IsFailure)
        {
            return Result<NormalizedIngestionDocument>.Failure(validation.Error!);
        }

        var nodes = payload.Chunks.Select(chunk =>
        {
            var hasHierarchy = !string.IsNullOrWhiteSpace(chunk.ParentId) || !string.IsNullOrWhiteSpace(chunk.LogicalPath);
            var hasProvenance = chunk.BoundingBox is not null;
            var hasTable = string.Equals(chunk.Type, "Table", StringComparison.OrdinalIgnoreCase);

            return new NormalizedNode(
                NodeId: chunk.Id,
                NodeType: chunk.Type,
                Text: chunk.Text,
                Vector: chunk.Vector,
                ParentId: chunk.ParentId,
                LogicalPath: chunk.LogicalPath,
                BoundingBox: chunk.BoundingBox is null
                    ? null
                    : new NormalizedBoundingBox(
                        chunk.BoundingBox.Page,
                        chunk.BoundingBox.X,
                        chunk.BoundingBox.Y,
                        chunk.BoundingBox.W,
                        chunk.BoundingBox.H),
                ReferencedNodeIds: chunk.ReferencedNodeIds ?? Array.Empty<string>(),
                HasHierarchyMetadata: hasHierarchy,
                HasProvenanceMetadata: hasProvenance,
                HasTableMetadata: hasTable);
        }).ToArray();

        var fidelity = FidelityDetector.Resolve(payload.Fidelity, nodes, _fidelityOptions);

        var normalized = new NormalizedIngestionDocument(
            DocumentId: payload.DocumentId,
            DocumentGroupId: payload.DocumentGroupId,
            VersionNumber: payload.VersionNumber,
            FidelityLevel: fidelity,
            AllowedDocumentGroups: payload.AllowedDocumentGroups,
            Nodes: nodes);

        return Result<NormalizedIngestionDocument>.Success(normalized);
    }
}