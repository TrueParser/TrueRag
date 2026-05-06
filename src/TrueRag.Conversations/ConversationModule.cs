using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrueRag.Conversations.Configuration;
using TrueRag.Conversations.Llm;
using TrueRag.Conversations.PromptAssembly;
using TrueRag.Core.Abstractions;

namespace TrueRag.Conversations;

public static class ConversationModule
{
    public static IServiceCollection AddTrueRagConversations(this IServiceCollection services)
    {
        services.AddOptions<LlmProviderOptions>()
            .BindConfiguration(LlmProviderOptions.SectionName);
        services.AddOptions<PromptAssemblyOptions>()
            .BindConfiguration(PromptAssemblyOptions.SectionName);
        services.AddOptions<GroundingGovernanceOptions>()
            .BindConfiguration(GroundingGovernanceOptions.SectionName);

        services.TryAddScoped<IConversationService, ConversationService>();
        services.TryAddSingleton<IConversationStateStore, DistributedConversationStateStore>();
        services.TryAddSingleton<IConversationSummaryBuilder, ConversationSummaryBuilder>();
        services.TryAddSingleton<IPromptAssemblyService, PromptAssemblyService>();
        services.TryAddSingleton<ILlmResponseParser, LlmResponseParser>();

        services.TryAddSingleton<ILlmProvider, LocalLlmProvider>();
        services.TryAddSingleton<ILlmProvider, OpenAiLlmProvider>();
        services.TryAddSingleton<ILlmProvider, AnthropicLlmProvider>();
        services.TryAddSingleton<ILlmProviderFactory, LlmProviderFactory>();
        return services;
    }
}
