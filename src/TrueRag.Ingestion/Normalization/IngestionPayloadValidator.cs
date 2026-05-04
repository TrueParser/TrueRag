using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Ingestion.Normalization;

internal static class IngestionPayloadValidator
{
    public static Result Validate(IngestionRequestDto payload)
    {
        if (string.IsNullOrWhiteSpace(payload.DocumentId))
        {
            return Result.Failure(new Error("ingestion.document_id_required", "DocumentId is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(payload.DocumentGroupId))
        {
            return Result.Failure(new Error("ingestion.document_group_id_required", "DocumentGroupId is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(payload.VersionNumber))
        {
            return Result.Failure(new Error("ingestion.version_required", "VersionNumber is required.", ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(payload.CollectionId))
        {
            return Result.Failure(new Error("ingestion.collection_id_required", "CollectionId is required.", ErrorType.Validation));
        }

        if (payload.AllowedDocumentGroups.Count == 0 ||
            payload.AllowedDocumentGroups.All(static group => string.IsNullOrWhiteSpace(group)))
        {
            return Result.Failure(new Error(
                "ingestion.allowed_document_groups_required",
                "AllowedDocumentGroups is required and must contain at least one non-empty group.",
                ErrorType.Validation));
        }

        if (payload.Chunks.Count == 0)
        {
            return Result.Failure(new Error("ingestion.chunks_required", "At least one chunk is required.", ErrorType.Validation));
        }

        foreach (var chunk in payload.Chunks)
        {
            if (string.IsNullOrWhiteSpace(chunk.Id))
            {
                return Result.Failure(new Error("ingestion.chunk_id_required", "Chunk Id is required.", ErrorType.Validation));
            }

            if (string.IsNullOrWhiteSpace(chunk.Type))
            {
                return Result.Failure(new Error("ingestion.chunk_type_required", "Chunk Type is required.", ErrorType.Validation));
            }

            if (string.IsNullOrWhiteSpace(chunk.Text))
            {
                return Result.Failure(new Error("ingestion.chunk_text_required", "Chunk Text is required.", ErrorType.Validation));
            }

            if (chunk.Vector.Length == 0)
            {
                return Result.Failure(new Error("ingestion.chunk_vector_required", "Chunk Vector is required.", ErrorType.Validation));
            }
        }

        return Result.Success();
    }
}
