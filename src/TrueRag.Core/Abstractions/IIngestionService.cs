using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Core.Abstractions;

public interface IIngestionService
{
    Task<Result> IngestAsync(
        IRequestContext requestContext,
        IngestionRequestDto request,
        CancellationToken cancellationToken = default);
}