using Microsoft.Extensions.Options;
using TrueRag.Core.Abstractions;
using TrueRag.Core.Context;
using TrueRag.Core.Models;
using TrueRag.Core.Primitives;
using TrueRag.Retrieval.Configuration;

namespace TrueRag.Retrieval;

internal sealed class RetrievalService : IRetrievalService
{
    private readonly IRetrievalRepository _retrievalRepository;
    private readonly RetrievalEngineOptions _options;

    public RetrievalService(IRetrievalRepository retrievalRepository, IOptions<RetrievalEngineOptions> options)
    {
        _retrievalRepository = retrievalRepository;
        _options = options.Value;
    }

    public async Task<Result<RetrievalResponse>> SearchVectorAsync(
        IRequestContext requestContext,
        RetrievalQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateShared(requestContext, query);
        if (validation.IsFailure)
        {
            return Result<RetrievalResponse>.Failure(validation.Error!);
        }

        var vectorValidation = RetrievalQueryValidator.ValidateVector(query);
        if (vectorValidation.IsFailure)
        {
            return Result<RetrievalResponse>.Failure(vectorValidation.Error!);
        }

        var effectiveQuery = ApplyFidelityRequirement(query);
        var baseResult = await _retrievalRepository.QueryVectorAsync(requestContext, effectiveQuery, cancellationToken);
        return await ExpandByFidelityProfileAsync(requestContext, baseResult, effectiveQuery.TopK, cancellationToken);
    }

    public async Task<Result<RetrievalResponse>> SearchTextAsync(
        IRequestContext requestContext,
        RetrievalQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateShared(requestContext, query);
        if (validation.IsFailure)
        {
            return Result<RetrievalResponse>.Failure(validation.Error!);
        }

        var textValidation = RetrievalQueryValidator.ValidateText(query);
        if (textValidation.IsFailure)
        {
            return Result<RetrievalResponse>.Failure(textValidation.Error!);
        }

        var effectiveQuery = ApplyFidelityRequirement(query);
        var baseResult = await _retrievalRepository.QueryTextAsync(requestContext, effectiveQuery, cancellationToken);
        return await ExpandByFidelityProfileAsync(requestContext, baseResult, effectiveQuery.TopK, cancellationToken);
    }

    public async Task<Result<RetrievalResponse>> SearchHybridAsync(
        IRequestContext requestContext,
        RetrievalQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateShared(requestContext, query);
        if (validation.IsFailure)
        {
            return Result<RetrievalResponse>.Failure(validation.Error!);
        }

        var textValidation = RetrievalQueryValidator.ValidateText(query);
        if (textValidation.IsFailure)
        {
            return Result<RetrievalResponse>.Failure(textValidation.Error!);
        }

        var vectorValidation = RetrievalQueryValidator.ValidateVector(query);
        if (vectorValidation.IsFailure)
        {
            return Result<RetrievalResponse>.Failure(vectorValidation.Error!);
        }

        var effectiveQuery = ApplyFidelityRequirement(query);
        var baseResult = await _retrievalRepository.QueryHybridAsync(requestContext, effectiveQuery, cancellationToken);
        return await ExpandByFidelityProfileAsync(requestContext, baseResult, effectiveQuery.TopK, cancellationToken);
    }

    private async Task<Result<RetrievalResponse>> ExpandByFidelityProfileAsync(
        IRequestContext requestContext,
        Result<RetrievalResponse> baseResult,
        int topK,
        CancellationToken cancellationToken)
    {
        if (baseResult.IsFailure)
        {
            return baseResult;
        }

        var baseNodes = baseResult.Value!.Nodes;

        var highFidelitySeeds = BuildStructuralSeeds(baseNodes);
        if (highFidelitySeeds.Count > 0)
        {
            var expanded = await _retrievalRepository.ExpandByLogicalSectionAsync(
                requestContext,
                highFidelitySeeds,
                limit: Math.Max(topK, topK * 3),
                cancellationToken);

            if (expanded.IsSuccess)
            {
                return Result<RetrievalResponse>.Success(new RetrievalResponse(MergeNodes(baseNodes, expanded.Value!.Nodes, topK)));
            }

            return baseResult;
        }

        if (!_options.FallbackToStandardRag)
        {
            return baseResult;
        }

        var standardSeeds = BuildAdjacentSeeds(baseNodes);
        if (standardSeeds.Count > 0)
        {
            var expanded = await _retrievalRepository.ExpandAdjacentChunksAsync(
                requestContext,
                standardSeeds,
                limit: Math.Max(topK, topK * 2),
                cancellationToken);

            if (expanded.IsSuccess)
            {
                return Result<RetrievalResponse>.Success(new RetrievalResponse(MergeNodes(baseNodes, expanded.Value!.Nodes, topK)));
            }
        }

        return baseResult;
    }

    private RetrievalQuery ApplyFidelityRequirement(RetrievalQuery query)
    {
        if (!_options.RequireHighFidelity)
        {
            return query;
        }

        var filters = query.Filters is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(query.Filters, StringComparer.OrdinalIgnoreCase);

        filters["fidelity_level"] = "high";
        return query with { Filters = filters };
    }

    private static IReadOnlyCollection<StructuralExpansionSeed> BuildStructuralSeeds(IReadOnlyCollection<RetrievedNode> nodes)
    {
        var seeds = new List<StructuralExpansionSeed>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in nodes)
        {
            if (!string.Equals(node.FidelityLevel, "high", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.LogicalPath))
            {
                continue;
            }

            var sectionPrefix = ExtractSectionPrefix(node.LogicalPath);
            if (string.IsNullOrWhiteSpace(sectionPrefix))
            {
                continue;
            }

            var key = node.DocumentId + "::" + sectionPrefix;
            if (seen.Add(key))
            {
                seeds.Add(new StructuralExpansionSeed(node.DocumentId, sectionPrefix));
            }
        }

        return seeds;
    }

    private static IReadOnlyCollection<AdjacentExpansionSeed> BuildAdjacentSeeds(IReadOnlyCollection<RetrievedNode> nodes)
    {
        var seeds = new List<AdjacentExpansionSeed>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in nodes)
        {
            if (!string.Equals(node.FidelityLevel, "standard", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var key = node.DocumentId + "::" + node.NodeId;
            if (seen.Add(key))
            {
                seeds.Add(new AdjacentExpansionSeed(node.DocumentId, node.NodeId));
            }
        }

        return seeds;
    }

    private static string? ExtractSectionPrefix(string logicalPath)
    {
        var parts = logicalPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        return string.Join('/', parts.Take(2)) + "/";
    }

    private static IReadOnlyCollection<RetrievedNode> MergeNodes(
        IReadOnlyCollection<RetrievedNode> primary,
        IReadOnlyCollection<RetrievedNode> expanded,
        int topK)
    {
        var merged = new List<RetrievedNode>(Math.Max(topK, primary.Count));
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in primary)
        {
            if (seen.Add(node.NodeId))
            {
                merged.Add(node);
            }
        }

        foreach (var node in expanded)
        {
            if (seen.Add(node.NodeId))
            {
                merged.Add(node);
            }

            if (merged.Count >= Math.Max(topK, primary.Count))
            {
                break;
            }
        }

        return merged;
    }

    private static Result ValidateShared(IRequestContext requestContext, RetrievalQuery query)
    {
        var contextValidation = RetrievalQueryValidator.ValidateContext(requestContext);
        if (contextValidation.IsFailure)
        {
            return contextValidation;
        }

        return RetrievalQueryValidator.ValidateTopK(query);
    }
}
