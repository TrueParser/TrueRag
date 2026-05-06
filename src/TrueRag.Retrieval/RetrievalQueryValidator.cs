using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;

namespace TrueRag.Retrieval;

internal static class RetrievalQueryValidator
{
    private const double MinHybridWeight = 0d;
    private const double MaxHybridWeight = 10d;
    private const int DefaultRrfK = 60;
    private const int MinRrfK = 1;
    private const int MaxRrfK = 500;
    private const double DefaultHybridWeight = 1d;

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
        if (string.IsNullOrWhiteSpace(requestContext.TenantId) ||
            string.IsNullOrWhiteSpace(requestContext.AppId) ||
            string.IsNullOrWhiteSpace(requestContext.CollectionId))
        {
            return Result.Failure(new Error("retrieval.scope_required", "Tenant, app, and collection scope are required.", ErrorType.Validation));
        }

        return Result.Success();
    }

    public static Result ValidateCollectionScope(IRequestContext requestContext, RetrievalQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.CollectionId))
        {
            return Result.Success();
        }

        if (!string.Equals(query.CollectionId, requestContext.CollectionId, StringComparison.Ordinal))
        {
            return Result.Failure(new Error("retrieval.collection_scope_mismatch", "Query CollectionId does not match request context collection scope.", ErrorType.Validation));
        }

        return Result.Success();
    }

    public static Result<RetrievalQuery> NormalizeAndValidateHybridOptions(RetrievalQuery query)
    {
        var vectorWeight = query.VectorWeight ?? DefaultHybridWeight;
        var textWeight = query.TextWeight ?? DefaultHybridWeight;
        var rrfK = query.RrfK ?? DefaultRrfK;

        if (!double.IsFinite(vectorWeight) || vectorWeight < MinHybridWeight || vectorWeight > MaxHybridWeight)
        {
            return Result<RetrievalQuery>.Failure(
                new Error("retrieval.hybrid_vector_weight_invalid", $"VectorWeight must be between {MinHybridWeight} and {MaxHybridWeight}.", ErrorType.Validation));
        }

        if (!double.IsFinite(textWeight) || textWeight < MinHybridWeight || textWeight > MaxHybridWeight)
        {
            return Result<RetrievalQuery>.Failure(
                new Error("retrieval.hybrid_text_weight_invalid", $"TextWeight must be between {MinHybridWeight} and {MaxHybridWeight}.", ErrorType.Validation));
        }

        if (vectorWeight <= 0d && textWeight <= 0d)
        {
            return Result<RetrievalQuery>.Failure(
                new Error("retrieval.hybrid_weight_sum_invalid", "At least one of VectorWeight or TextWeight must be greater than zero.", ErrorType.Validation));
        }

        if (rrfK < MinRrfK || rrfK > MaxRrfK)
        {
            return Result<RetrievalQuery>.Failure(
                new Error("retrieval.hybrid_rrfk_invalid", $"RrfK must be between {MinRrfK} and {MaxRrfK}.", ErrorType.Validation));
        }

        return Result<RetrievalQuery>.Success(query with
        {
            VectorWeight = vectorWeight,
            TextWeight = textWeight,
            RrfK = rrfK
        });
    }
}
