using TrueRag.Conversations;
using TrueRag.Core.Models;

namespace TrueRag.UnitTests.Conversations;

public sealed class ConversationSummaryBuilderTests
{
    [Fact]
    public void Build_UsesRecentMessagesOnly_AndNormalizesContent()
    {
        var builder = new ConversationSummaryBuilder();
        var now = DateTimeOffset.UtcNow;
        var messages = Enumerable.Range(0, 12)
            .Select(i => new ConversationMessage("t1", i % 2 == 0 ? "user" : "assistant", $"line-{i}\nnext", now.AddMinutes(i)))
            .ToArray();

        var summary = builder.Build(messages);

        Assert.DoesNotContain("line-0", summary, StringComparison.Ordinal);
        Assert.Contains("line-11 next", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", summary, StringComparison.Ordinal);
    }
}
