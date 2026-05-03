using TrueRag.Core.Primitives;
using TrueRag.Ingestion.Models;

namespace TrueRag.Ingestion.Adapters;

public interface IIngestionPayloadAdapter<in TPayload>
{
    Result<NormalizedIngestionDocument> Map(TPayload payload);
}