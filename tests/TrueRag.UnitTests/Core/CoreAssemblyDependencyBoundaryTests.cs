using System.Reflection;

namespace TrueRag.UnitTests.Core;

public sealed class CoreAssemblyDependencyBoundaryTests
{
    [Fact]
    public void CoreAssembly_DoesNotReferenceApiOrProviderSdkAssemblies()
    {
        var references = typeof(TrueRag.Core.Context.RequestContext).Assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, name => name.StartsWith("TrueRag.Api", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("OpenAI", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, name => name.Contains("Anthropic", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, name => name.Contains("Cohere", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, name => name.Contains("Voyage", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, name => name.Contains("OnnxRuntime", StringComparison.OrdinalIgnoreCase));
    }
}
