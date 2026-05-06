using System.Text.Json;
using TrueRag.Core.Models;

namespace TrueRag.Conversations.Llm;

internal sealed class LlmResponseParser : ILlmResponseParser
{
    public LlmCompletionResponse Parse(string provider, string rawText, int promptTokensEstimate)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return new LlmCompletionResponse(
                Text: string.Empty,
                ToolCalls: [],
                Usage: new LlmUsage(promptTokensEstimate, 0, promptTokensEstimate),
                Provider: provider,
                LlmCertainty: null);
        }

        var trimmed = rawText.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                var root = document.RootElement;

                var text = root.TryGetProperty("answer", out var answerElement)
                    ? answerElement.GetString() ?? string.Empty
                    : rawText;

                var certainty = root.TryGetProperty("llm_certainty", out var certaintyElement) && certaintyElement.TryGetDouble(out var c)
                    ? c
                    : (double?)null;

                var toolCalls = new List<LlmToolCall>();
                if (root.TryGetProperty("tool_calls", out var toolsElement) && toolsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tool in toolsElement.EnumerateArray())
                    {
                        var id = tool.TryGetProperty("id", out var idElement)
                            ? idElement.GetString() ?? Guid.NewGuid().ToString("N")
                            : Guid.NewGuid().ToString("N");
                        var name = tool.TryGetProperty("name", out var nameElement)
                            ? nameElement.GetString() ?? "unknown_tool"
                            : "unknown_tool";
                        var args = tool.TryGetProperty("arguments", out var argsElement)
                            ? argsElement.GetRawText()
                            : "{}";
                        toolCalls.Add(new LlmToolCall(id, name, args));
                    }
                }

                var completionTokens = Math.Max(1, PromptAssembly.PromptAssemblyService.EstimateTokens(text));
                var grounded = TryParseGroundedResponse(root, text, certainty, out var schemaErrorCode);
                return new LlmCompletionResponse(
                    Text: text,
                    ToolCalls: toolCalls,
                    Usage: new LlmUsage(promptTokensEstimate, completionTokens, promptTokensEstimate + completionTokens),
                    Provider: provider,
                    LlmCertainty: certainty,
                    GroundedResponse: grounded,
                    SchemaValidationErrorCode: schemaErrorCode);
            }
            catch
            {
            }
        }

        var fallbackTokens = Math.Max(1, PromptAssembly.PromptAssemblyService.EstimateTokens(rawText));
        return new LlmCompletionResponse(
            Text: rawText,
            ToolCalls: [],
            Usage: new LlmUsage(promptTokensEstimate, fallbackTokens, promptTokensEstimate + fallbackTokens),
            Provider: provider,
            LlmCertainty: null,
            GroundedResponse: null,
            SchemaValidationErrorCode: "schema_invalid.not_json_object");
    }

    private static GroundedResponseContract? TryParseGroundedResponse(
        JsonElement root,
        string answer,
        double? certainty,
        out string? schemaErrorCode)
    {
        schemaErrorCode = null;

        if (!root.TryGetProperty("claims", out var claimsElement) || claimsElement.ValueKind != JsonValueKind.Array)
        {
            schemaErrorCode = "schema_invalid.claims_missing";
            return null;
        }

        if (!root.TryGetProperty("citations", out var citationsElement) || citationsElement.ValueKind != JsonValueKind.Array)
        {
            schemaErrorCode = "schema_invalid.citations_missing";
            return null;
        }

        var groundingStatus = GroundingStatus.Grounded;
        if (root.TryGetProperty("grounding_status", out var statusElement))
        {
            var statusText = statusElement.GetString();
            if (!TryParseGroundingStatus(statusText, out groundingStatus))
            {
                schemaErrorCode = "schema_invalid.grounding_status_invalid";
                return null;
            }
        }

        var claims = new List<GroundedClaim>();
        foreach (var claimElement in claimsElement.EnumerateArray())
        {
            if (claimElement.ValueKind != JsonValueKind.Object)
            {
                schemaErrorCode = "schema_invalid.claim_entry_invalid";
                return null;
            }

            var claimId = claimElement.TryGetProperty("claim_id", out var claimIdElement)
                ? claimIdElement.GetString()
                : null;
            var claimText = claimElement.TryGetProperty("text", out var claimTextElement)
                ? claimTextElement.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(claimId) || string.IsNullOrWhiteSpace(claimText))
            {
                schemaErrorCode = "schema_invalid.claim_fields_missing";
                return null;
            }

            if (!claimElement.TryGetProperty("citation_ids", out var citationIdsElement) || citationIdsElement.ValueKind != JsonValueKind.Array)
            {
                schemaErrorCode = "schema_invalid.claim_citation_ids_missing";
                return null;
            }

            var citationIds = citationIdsElement
                .EnumerateArray()
                .Where(static idElement => idElement.ValueKind == JsonValueKind.String)
                .Select(static idElement => idElement.GetString())
                .Where(static id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .ToArray();

            if (citationIds.Length == 0 && groundingStatus is GroundingStatus.Grounded or GroundingStatus.PartiallyGrounded)
            {
                schemaErrorCode = "schema_invalid.claim_citations_empty";
                return null;
            }

            claims.Add(new GroundedClaim(claimId, claimText, citationIds));
        }

        var citations = new List<GroundedCitation>();
        foreach (var citationElement in citationsElement.EnumerateArray())
        {
            if (citationElement.ValueKind != JsonValueKind.Object)
            {
                schemaErrorCode = "schema_invalid.citation_entry_invalid";
                return null;
            }

            var citationId = citationElement.TryGetProperty("citation_id", out var citationIdElement)
                ? citationIdElement.GetString()
                : null;
            var nodeId = citationElement.TryGetProperty("node_id", out var nodeIdElement)
                ? nodeIdElement.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(citationId) || string.IsNullOrWhiteSpace(nodeId))
            {
                schemaErrorCode = "schema_invalid.citation_fields_missing";
                return null;
            }

            var documentId = citationElement.TryGetProperty("document_id", out var documentIdElement) ? documentIdElement.GetString() : null;
            var tenantId = citationElement.TryGetProperty("tenant_id", out var tenantIdElement) ? tenantIdElement.GetString() : null;
            var appId = citationElement.TryGetProperty("app_id", out var appIdElement) ? appIdElement.GetString() : null;
            var collectionId = citationElement.TryGetProperty("collection_id", out var collectionIdElement) ? collectionIdElement.GetString() : null;
            var sectionPath = citationElement.TryGetProperty("section_path", out var sectionPathElement) ? sectionPathElement.GetString() : null;
            var pageNumber = citationElement.TryGetProperty("page_number", out var pageElement) && pageElement.TryGetInt32(out var page) ? page : (int?)null;
            var supportScore = citationElement.TryGetProperty("support_score", out var supportElement) && supportElement.TryGetDouble(out var support) ? support : (double?)null;
            var spanId = citationElement.TryGetProperty("span_id", out var spanIdElement) ? spanIdElement.GetString() : null;
            var startOffset = citationElement.TryGetProperty("start_offset", out var startElement) && startElement.TryGetInt32(out var start) ? start : (int?)null;
            var endOffset = citationElement.TryGetProperty("end_offset", out var endElement) && endElement.TryGetInt32(out var end) ? end : (int?)null;
            var quote = citationElement.TryGetProperty("quote", out var quoteElement) ? quoteElement.GetString() : null;

            citations.Add(new GroundedCitation(citationId, nodeId, documentId, tenantId, appId, collectionId, sectionPath, pageNumber, supportScore, spanId, startOffset, endOffset, quote));
        }

        var insufficiencyReason = root.TryGetProperty("insufficiency_reason", out var insufficiencyElement)
            ? insufficiencyElement.GetString()
            : null;
        var confidence = root.TryGetProperty("confidence", out var confidenceElement) && confidenceElement.TryGetDouble(out var c2)
            ? c2
            : certainty;

        return new GroundedResponseContract(answer, claims, citations, insufficiencyReason, confidence, groundingStatus);
    }

    private static bool TryParseGroundingStatus(string? value, out GroundingStatus status)
    {
        status = GroundingStatus.Grounded;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "grounded" => true,
            "partially_grounded" => SetStatus(GroundingStatus.PartiallyGrounded, out status),
            "insufficient_evidence" => SetStatus(GroundingStatus.InsufficientEvidence, out status),
            "conflicting_evidence" => SetStatus(GroundingStatus.ConflictingEvidence, out status),
            "validation_failed" => SetStatus(GroundingStatus.ValidationFailed, out status),
            _ => false
        };
    }

    private static bool SetStatus(GroundingStatus statusValue, out GroundingStatus status)
    {
        status = statusValue;
        return true;
    }
}
