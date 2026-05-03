using TrueRag.Core.Models;
using TrueRag.Core.Primitives;
using TrueRag.Ingestion.Models;

namespace TrueRag.Ingestion.Normalization;

public interface IIngestionNormalizer
{
    Result<NormalizedIngestionDocument> Normalize(IngestionRequestDto payload);
}