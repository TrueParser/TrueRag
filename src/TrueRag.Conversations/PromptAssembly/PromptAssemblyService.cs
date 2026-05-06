using Microsoft.Extensions.Options;
using TrueRag.Conversations.Configuration;
using TrueRag.Conversations.Security;
using TrueRag.Core.Models;

namespace TrueRag.Conversations.PromptAssembly;

internal sealed class PromptAssemblyService : IPromptAssemblyService
{
    private readonly PromptAssemblyOptions _options;
    private readonly GroundingGovernanceOptions _governanceOptions;

    public PromptAssemblyService(
        IOptions<PromptAssemblyOptions> options,
        IOptions<GroundingGovernanceOptions> governanceOptions)
    {
        _options = options.Value;
        _governanceOptions = governanceOptions.Value;
    }

    public PromptAssemblyResult Assemble(
        ConversationGenerateRequest request,
        ConversationThreadSnapshot snapshot)
    {
        var totalBudget = Math.Max(512, request.PromptTokenBudget ?? _options.DefaultTokenBudget);
        var promptBudget = Math.Max(256, totalBudget - Math.Max(128, _options.ReservedCompletionTokens));
        var budgetUsed = 0;
        var messages = new List<LlmMessage>();

        Add("system", _options.SystemInstruction, required: true);
        if (request.PolicyMode == GenerationPolicyMode.Grounded)
        {
            Add("system", _options.GroundedPolicyInstruction, required: true);
            Add("system", _options.RetrievedContentSafetyInstruction, required: true);
            Add("system", BuildMemoryCitationInstruction(_governanceOptions.MemoryCitationPolicy), required: true);
        }

        Add("system", BuildSummary(snapshot.State.Summary), required: false);
        Add("user", request.UserMessage, required: true);

        foreach (var evidence in BuildEvidencePack(request.RetrievedContext))
        {
            Add("system", evidence, required: false);
        }

        foreach (var history in snapshot.Messages
                     .OrderByDescending(static item => item.OccurredAtUtc)
                     .Take(10)
                     .OrderBy(static item => item.OccurredAtUtc))
        {
            Add(history.Role, history.Message, required: false);
        }

        var estimated = messages.Sum(static message => EstimateTokens(message.Content));
        return new PromptAssemblyResult(messages, estimated, budgetUsed, promptBudget);

        void Add(string role, string content, bool required)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            var text = Normalize(content);
            var tokens = EstimateTokens(text);
            if (budgetUsed + tokens <= promptBudget)
            {
                messages.Add(new LlmMessage(role, text));
                budgetUsed += tokens;
                return;
            }

            if (!required)
            {
                return;
            }

            var remaining = Math.Max(16, promptBudget - budgetUsed);
            var truncated = TruncateToTokens(text, remaining);
            if (!string.IsNullOrWhiteSpace(truncated))
            {
                messages.Add(new LlmMessage(role, truncated));
                budgetUsed += EstimateTokens(truncated);
            }
        }
    }

    private static string BuildSummary(string? summary)
        => string.IsNullOrWhiteSpace(summary) ? string.Empty : $"[summary] {summary}";

    private static string Normalize(string value)
        => value.Replace('\r', ' ').Replace('\n', ' ').Trim();

    private static string BuildMemoryCitationInstruction(ConversationMemoryCitationPolicy policy)
        => policy switch
        {
            ConversationMemoryCitationPolicy.NonCiteable =>
                "Conversation memory, summaries, history, and user-provided facts are contextual only and must not be cited as retrieved-document evidence.",
            ConversationMemoryCitationPolicy.CiteableWhenRetrievedEvidence =>
                "Conversation memory can be cited only when represented as retrieved evidence in the current evidence pack; otherwise it is contextual only.",
            _ =>
                "Conversation memory is contextual only unless explicitly present in retrieved evidence."
        };

    private static IReadOnlyList<string> BuildEvidencePack(IReadOnlyCollection<RetrievedContextItem> contexts)
    {
        var ordered = contexts
            .Where(static item => item.IsCiteable)
            .OrderByDescending(static item => item.Score ?? 0d)
            .ThenBy(static item => item.NodeId, StringComparer.Ordinal)
            .ToArray();

        var evidence = new List<string>(ordered.Length);
        var dedupe = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < ordered.Length; i++)
        {
            var item = ordered[i];
            if (string.IsNullOrWhiteSpace(item.Text))
            {
                continue;
            }

            var normalizedText = Normalize(item.Text).ToLowerInvariant();
            if (!dedupe.Add(normalizedText))
            {
                continue;
            }

            var sanitized = RetrievedContentSanitizer.Sanitize(item.Text);
            if (string.IsNullOrWhiteSpace(sanitized.Text))
            {
                continue;
            }

            var evidenceId = $"ev-{i + 1:D4}";
            var sourceDocumentId = string.IsNullOrWhiteSpace(item.SourceDocumentId) ? "unknown" : item.SourceDocumentId;
            var sectionPath = string.IsNullOrWhiteSpace(item.SectionPath) ? "n/a" : item.SectionPath;
            var title = string.IsNullOrWhiteSpace(item.Title) ? "n/a" : item.Title;
            var page = item.PageNumber?.ToString() ?? "n/a";
            var startOffset = item.StartOffset?.ToString() ?? "n/a";
            var endOffset = item.EndOffset?.ToString() ?? "n/a";
            var score = (item.Score ?? 0d).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
            var aclScope = item.AclScopes is { Count: > 0 }
                ? string.Join('|', item.AclScopes.Where(static s => !string.IsNullOrWhiteSpace(s)).OrderBy(static s => s, StringComparer.Ordinal))
                : "unknown";

            var injectionFlag = sanitized.HadInjectionSignals ? "true" : "false";
            var packed = $"[evidence id={evidenceId}; node_id={item.NodeId}; doc_id={sourceDocumentId}; score={score}; title={title}; section={sectionPath}; page={page}; span={startOffset}:{endOffset}; acl_scope={aclScope}; untrusted=true; injection_signals={injectionFlag}] {Normalize(sanitized.Text)}";
            evidence.Add(packed);
        }

        return evidence;
    }

    internal static int EstimateTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Max(1, (int)Math.Ceiling(words * 1.35));
    }

    private static string TruncateToTokens(string text, int tokenBudget)
    {
        if (tokenBudget <= 0)
        {
            return string.Empty;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return string.Empty;
        }

        var allowedWords = Math.Max(1, (int)Math.Floor(tokenBudget / 1.35));
        if (allowedWords >= words.Length)
        {
            return text;
        }

        return string.Join(' ', words.Take(allowedWords)) + " ...";
    }
}
