using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Retrieval;

internal static class RetrievalQueryValidator
{
    public static Result ValidateTopK(RetrievalQuery query)
    {
        if (query.TopK <= 0)
        {
            return Result.Failure(new Error("retrieval.topk_invalid", "TopK must be greater than zero.", ErrorType.Validation));
        }

        return Result.Success();
    }

    public static Result ValidateText(RetrievalQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.QueryText))
        {
            return Result.Failure(new Error("retrieval.query_text_required", "QueryText is required.", ErrorType.Validation));
        }

        return Result.Success();
    }

    public static Result ValidateVector(RetrievalQuery query)
    {
        if (query.QueryVector is null || query.QueryVector.Length == 0)
        {
            return Result.Failure(new Error("retrieval.query_vector_required", "QueryVector is required.", ErrorType.Validation));
        }

        return Result.Success();
    }

    public static Result ValidateContext(IRequestContext requestContext)
    {
        if (string.IsNullOrWhiteSpace(requestContext.TenantId) || string.IsNullOrWhiteSpace(requestContext.AppId))
        {
            return Result.Failure(new Error("retrieval.scope_required", "Tenant and app scope are required.", ErrorType.Validation));
        }

        return Result.Success();
    }
}