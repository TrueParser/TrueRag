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
    private readonly IRetrievalSemanticCache _semanticCache;
    private readonly IDistributedRetrievalRateLimitStore _rateLimitStore;
    private readonly ICollectionEmbeddingModeResolver _embeddingModeResolver;
    private readonly IQueryEmbeddingGenerator _queryEmbeddingGenerator;
    private readonly IEmbeddingProfileResolver? _embeddingProfileResolver;
    private readonly IActiveEmbeddingProfileStore? _activeProfileStore;

    public RetrievalService(
        IRetrievalRepository retrievalRepository,
        IOptions<RetrievalEngineOptions> options,
        IRetrievalSemanticCache semanticCache,
        IDistributedRetrievalRateLimitStore rateLimitStore,
        ICollectionEmbeddingModeResolver embeddingModeResolver,
        IQueryEmbeddingGenerator queryEmbeddingGenerator,
        IEnumerable<IEmbeddingProfileResolver> embeddingProfileResolvers,
        IEnumerable<IActiveEmbeddingProfileStore> activeProfileStores)
    {
        _retrievalRepository = retrievalRepository;
        _options = options.Value;
        _semanticCache = semanticCache;
        _rateLimitStore = rateLimitStore;
        _embeddingModeResolver = embeddingModeResolver;
        _queryEmbeddingGenerator = queryEmbeddingGenerator;
        _embeddingProfileResolver = embeddingProfileResolvers.FirstOrDefault();
        _activeProfileStore = activeProfileStores.FirstOrDefault();
    }

    public Task<Result<RetrievalResponse>> SearchVectorAsync(
        IRequestContext requestContext,
        RetrievalQuery query,
        CancellationToken cancellationToken = default)
        => SearchInternalAsync("vector", requestContext, query, _retrievalRepository.QueryVectorAsync, cancellationToken);

    public Task<Result<RetrievalResponse>> SearchTextAsync(
        IRequestContext requestContext,
        RetrievalQuery query,
        CancellationToken cancellationToken = default)
        => SearchInternalAsync("text", requestContext, query, _retrievalRepository.QueryTextAsync, cancellationToken);

    public Task<Result<RetrievalResponse>> SearchHybridAsync(
        IRequestContext requestContext,
        RetrievalQuery query,
        CancellationToken cancellationToken = default)
        => SearchInternalAsync("hybrid", requestContext, query, _retrievalRepository.QueryHybridAsync, cancellationToken);

    private async Task<Result<RetrievalResponse>> SearchInternalAsync(
        string lane,
        IRequestContext requestContext,
        RetrievalQuery query,
        Func<IRequestContext, RetrievalQuery, CancellationToken, Task<Result<RetrievalResponse>>> modeQuery,
        CancellationToken cancellationToken)
    {
        var validation = ValidateShared(requestContext, query);
        if (validation.IsFailure)
        {
            return Result<RetrievalResponse>.Failure(validation.Error!);
        }

        var embeddingMode = await _embeddingModeResolver.ResolveModeAsync(requestContext, cancellationToken);

        if (lane is "vector" or "hybrid")
        {
            var vectorReady = await EnsureVectorQueryByModeAsync(requestContext, query, embeddingMode, cancellationToken);
            if (vectorReady.IsFailure)
            {
                return Result<RetrievalResponse>.Failure(vectorReady.Error!);
            }

            query = vectorReady.Value!;

            var descriptorMismatch = await ValidateVectorDescriptorCompatibilityAsync(requestContext, query, cancellationToken);
            if (descriptorMismatch.IsFailure)
            {
                return Result<RetrievalResponse>.Failure(descriptorMismatch.Error!);
            }
        }

        if (lane is "text" or "hybrid")
        {
            var textValidation = RetrievalQueryValidator.ValidateText(query);
            if (textValidation.IsFailure)
            {
                return Result<RetrievalResponse>.Failure(textValidation.Error!);
            }
        }

        if (_options.EnableDistributedRateLimit)
        {
            var allowed = await _rateLimitStore.TryAcquireAsync(
                requestContext,
                lane,
                _options.DistributedRateLimitRequests,
                _options.DistributedRateLimitWindow,
                cancellationToken);

            if (!allowed)
            {
                return Result<RetrievalResponse>.Failure(
                    new Error("retrieval.rate_limited", "Rate limit exceeded for this tenant/application lane.", ErrorType.Validation));
            }
        }

        var effectiveQuery = ApplyFidelityRequirement(query) with { CollectionId = requestContext.CollectionId };

        if (_options.EnableSemanticCache)
        {
            var cached = await _semanticCache.GetAsync(requestContext, lane, effectiveQuery, cancellationToken);
            if (cached is not null)
            {
                return Result<RetrievalResponse>.Success(cached);
            }
        }

        var baseResult = await modeQuery(requestContext, effectiveQuery, cancellationToken);
        if (baseResult.IsFailure)
        {
            return baseResult;
        }

        var fidelityExpanded = await ExpandByFidelityProfileAsync(requestContext, baseResult.Value!, effectiveQuery.TopK, cancellationToken);
        var multiHopExpanded = await ExpandMultiHopAsync(requestContext, fidelityExpanded, effectiveQuery.TopK, cancellationToken);
        var withDiffs = await AttachStructuralDiffsAsync(requestContext, effectiveQuery, multiHopExpanded, cancellationToken);
        var withConfidence = AttachConfidence(withDiffs);

        if (_options.EnableSemanticCache)
        {
            await _semanticCache.SetAsync(requestContext, lane, effectiveQuery, withConfidence, _options.SemanticCacheTtl, cancellationToken);
        }

        return Result<RetrievalResponse>.Success(withConfidence);
    }

    private async Task<Result<RetrievalQuery>> EnsureVectorQueryByModeAsync(
        IRequestContext requestContext,
        RetrievalQuery query,
        CollectionEmbeddingMode mode,
        CancellationToken cancellationToken)
    {
        if (mode == CollectionEmbeddingMode.ExternalEmbedding)
        {
            var textValidation = RetrievalQueryValidator.ValidateText(query);
            if (textValidation.IsFailure)
            {
                return Result<RetrievalQuery>.Failure(textValidation.Error!);
            }

            var vectorValidation = RetrievalQueryValidator.ValidateVector(query);
            if (vectorValidation.IsFailure)
            {
                return Result<RetrievalQuery>.Failure(vectorValidation.Error!);
            }

            return Result<RetrievalQuery>.Success(query);
        }

        var requireText = RetrievalQueryValidator.ValidateText(query);
        if (requireText.IsFailure)
        {
            return Result<RetrievalQuery>.Failure(requireText.Error!);
        }

        if (query.QueryVector is { Length: > 0 })
        {
            return Result<RetrievalQuery>.Success(query);
        }

        var generated = await _queryEmbeddingGenerator.GenerateQueryVectorAsync(requestContext, query.QueryText, cancellationToken);
        if (generated.IsFailure)
        {
            return Result<RetrievalQuery>.Failure(generated.Error!);
        }

        return Result<RetrievalQuery>.Success(query with { QueryVector = generated.Value! });
    }

    private async Task<Result> ValidateVectorDescriptorCompatibilityAsync(
        IRequestContext requestContext,
        RetrievalQuery query,
        CancellationToken cancellationToken)
    {
        if (_embeddingProfileResolver is null || query.QueryVector is null)
        {
            return Result.Success();
        }

        var descriptor = await _embeddingProfileResolver.ResolveActiveDescriptorAsync(
            requestContext.TenantId,
            requestContext.AppId,
            requestContext.CollectionId,
            cancellationToken);

        if (query.QueryVector.Length != descriptor.Dimensions)
        {
            return Result.Failure(new Error(
                "retrieval.embedding_space_mismatch",
                $"QueryVector dimensions ({query.QueryVector.Length}) do not match active embedding descriptor dimensions ({descriptor.Dimensions}).",
                ErrorType.Validation));
        }

        if (!string.IsNullOrWhiteSpace(query.QueryVectorProvider) &&
            !string.Equals(query.QueryVectorProvider, descriptor.Provider, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(new Error(
                "retrieval.embedding_space_mismatch",
                $"QueryVector provider '{query.QueryVectorProvider}' does not match active embedding provider '{descriptor.Provider}'.",
                ErrorType.Validation));
        }

        if (!string.IsNullOrWhiteSpace(query.QueryVectorModel) &&
            !string.Equals(query.QueryVectorModel, descriptor.Model, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(new Error(
                "retrieval.embedding_space_mismatch",
                $"QueryVector model '{query.QueryVectorModel}' does not match active embedding model '{descriptor.Model}'.",
                ErrorType.Validation));
        }

        if (_activeProfileStore is not null)
        {
            var provider = query.QueryVectorProvider ?? descriptor.Provider;
            var model = query.QueryVectorModel ?? descriptor.Model;
            var compatibility = await _activeProfileStore.CheckCompatibilityAsync(
                requestContext.TenantId,
                requestContext.AppId,
                requestContext.CollectionId,
                provider,
                model,
                query.QueryVector.Length,
                cancellationToken);
            if (!compatibility.IsCompatible)
            {
                return Result.Failure(new Error(
                    "retrieval.embedding_space_mismatch",
                    $"QueryVector is incompatible with active profile for this scope ({compatibility.Reason}).",
                    ErrorType.Validation));
            }
        }

        return Result.Success();
    }

    private async Task<RetrievalResponse> ExpandByFidelityProfileAsync(
        IRequestContext requestContext,
        RetrievalResponse baseResponse,
        int topK,
        CancellationToken cancellationToken)
    {
        var baseNodes = baseResponse.Nodes;

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
                return new RetrievalResponse(MergeNodes(baseNodes, expanded.Value!.Nodes, topK));
            }

            return baseResponse;
        }

        if (!_options.FallbackToStandardRag)
        {
            return baseResponse;
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
                return new RetrievalResponse(MergeNodes(baseNodes, expanded.Value!.Nodes, topK));
            }
        }

        return baseResponse;
    }

    private async Task<RetrievalResponse> ExpandMultiHopAsync(
        IRequestContext requestContext,
        RetrievalResponse input,
        int topK,
        CancellationToken cancellationToken)
    {
        if (!_options.EnableMultiHopLinking)
        {
            return input;
        }

        var nodeIds = input.Nodes
            .SelectMany(static n => n.ReferencedNodeIds ?? [])
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Take(Math.Max(1, _options.MultiHopMaxNodes))
            .ToArray();

        if (nodeIds.Length == 0)
        {
            return input;
        }

        var referenced = await _retrievalRepository.GetNodesByIdsAsync(requestContext, nodeIds, cancellationToken);
        if (referenced.IsFailure)
        {
            return input;
        }

        return input with { Nodes = MergeNodes(input.Nodes, referenced.Value!, Math.Max(topK, input.Nodes.Count)) };
    }

    private async Task<RetrievalResponse> AttachStructuralDiffsAsync(
        IRequestContext requestContext,
        RetrievalQuery query,
        RetrievalResponse input,
        CancellationToken cancellationToken)
    {
        if (!_options.EnableStructuralDiffing)
        {
            return input;
        }

        if (query.Filters is null)
        {
            return input;
        }

        if (!query.Filters.TryGetValue("document_group_id", out var documentGroupId) || string.IsNullOrWhiteSpace(documentGroupId) ||
            !query.Filters.TryGetValue("left_version", out var leftVersion) || string.IsNullOrWhiteSpace(leftVersion) ||
            !query.Filters.TryGetValue("right_version", out var rightVersion) || string.IsNullOrWhiteSpace(rightVersion) ||
            !query.Filters.TryGetValue("logical_path", out var logicalPath) || string.IsNullOrWhiteSpace(logicalPath))
        {
            return input;
        }

        var requests = new[]
        {
            new StructuralDiffRequest(documentGroupId, leftVersion, rightVersion, logicalPath)
        }.Take(_options.StructuralDiffMaxRequests).ToArray();

        var diffs = await _retrievalRepository.GetStructuralDiffsAsync(requestContext, requests, cancellationToken);
        if (diffs.IsFailure)
        {
            return input;
        }

        return input with { Diffs = diffs.Value! };
    }

    private RetrievalResponse AttachConfidence(RetrievalResponse input)
    {
        if (input.Nodes.Count == 0)
        {
            return input with { RetrievalConfidence = 0d, OverallConfidence = 0d };
        }

        var normalized = input.Nodes
            .Select(static n => Clamp01(n.Score))
            .DefaultIfEmpty(0d)
            .Average();

        var retrievalConfidence = Clamp01(normalized);
        var llmCertainty = input.LlmCertainty;
        if (llmCertainty is null)
        {
            return input with { RetrievalConfidence = retrievalConfidence, OverallConfidence = retrievalConfidence };
        }

        var totalWeight = Math.Max(0.0001, _options.RetrievalConfidenceWeight + _options.LlmCertaintyWeight);
        var overall = ((retrievalConfidence * _options.RetrievalConfidenceWeight) + (Clamp01(llmCertainty.Value) * _options.LlmCertaintyWeight)) / totalWeight;

        return input with
        {
            RetrievalConfidence = retrievalConfidence,
            OverallConfidence = Clamp01(overall)
        };
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

        var collectionValidation = RetrievalQueryValidator.ValidateCollectionScope(requestContext, query);
        if (collectionValidation.IsFailure)
        {
            return collectionValidation;
        }

        return RetrievalQueryValidator.ValidateTopK(query);
    }

    private static double Clamp01(double value) => Math.Max(0d, Math.Min(1d, value));
}
