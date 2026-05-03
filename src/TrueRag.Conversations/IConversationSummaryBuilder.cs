using TrueRag.Core.Models;

namespace TrueRag.Conversations;

internal interface IConversationSummaryBuilder
{
    string Build(IReadOnlyCollection<ConversationMessage> messages);
}
