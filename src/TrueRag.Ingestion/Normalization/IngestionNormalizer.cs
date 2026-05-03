using TrueRag.Core.Models;
using TrueRag.Core.Primitives;
using TrueRag.Ingestion.Adapters;
using TrueRag.Ingestion.Models;

namespace TrueRag.Ingestion.Normalization;

internal sealed class IngestionNormalizer : IIngestionNormalizer
{
    private readonly IIngestionPayloadAdapter<IngestionRequestDto> _adapter;

    public IngestionNormalizer(IIngestionPayloadAdapter<IngestionRequestDto> adapter)
    {
        _adapter = adapter;
    }

    public Result<NormalizedIngestionDocument> Normalize(IngestionRequestDto payload)
    {
        return _adapter.Map(payload);
    }
}