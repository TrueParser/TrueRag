using Microsoft.Extensions.Options;

namespace TrueRag.UnitTests.Embeddings;

internal sealed class StaticOptionsMonitor<TOptions> : IOptionsMonitor<TOptions>
    where TOptions : class
{
    public StaticOptionsMonitor(TOptions currentValue)
    {
        CurrentValue = currentValue;
    }

    public TOptions CurrentValue { get; }

    public TOptions Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
}
