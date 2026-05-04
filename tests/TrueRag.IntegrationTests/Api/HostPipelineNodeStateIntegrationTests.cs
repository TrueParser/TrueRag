using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TrueRag.Ingestion.Admission;

namespace TrueRag.IntegrationTests.Api;

public sealed class HostPipelineNodeStateIntegrationTests
{
    [Fact]
    public async Task NodeState_Returns503_WhenMonitorReportsOverloaded()
    {
        await using var factory = new HostPipelineFactory(new FixedPressureProvider(overloaded: true));
        using var client = factory.CreateClient();

        await Task.Delay(75);
        var response = await client.GetAsync("/health/node-state");

        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"state\":\"overloaded\"", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResourceGuard_RejectsWhenOverloaded_AndAllowsWhenHealthy()
    {
        await using var overloadedFactory = new HostPipelineFactory(new FixedPressureProvider(overloaded: true));
        using var overloadedClient = overloadedFactory.CreateClient();

        await Task.Delay(75);
        var overloadedResponse = await overloadedClient.PostAsJsonAsync("/api/v1/search/text", new
        {
            queryText = "hello",
            topK = 3
        });

        Assert.Equal((System.Net.HttpStatusCode)429, overloadedResponse.StatusCode);

        await using var healthyFactory = new HostPipelineFactory(new FixedPressureProvider(overloaded: false));
        using var healthyClient = healthyFactory.CreateClient();

        await Task.Delay(75);
        var healthyResponse = await healthyClient.PostAsJsonAsync("/api/v1/search/text", new
        {
            queryText = "hello",
            topK = 3
        });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, healthyResponse.StatusCode);
    }

    private sealed class HostPipelineFactory : WebApplicationFactory<Program>
    {
        private readonly IIngestionPressureSnapshotProvider _pressureProvider;

        public HostPipelineFactory(IIngestionPressureSnapshotProvider pressureProvider)
        {
            _pressureProvider = pressureProvider;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Production);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IIngestionPressureSnapshotProvider>();
                services.AddSingleton(_pressureProvider);
            });
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ResourceGuard:Enabled"] = "true",
                    ["ResourceGuard:SampleIntervalMs"] = "25",
                    ["ResourceGuard:ConsecutiveSamplesForOverload"] = "1",
                    ["ResourceGuard:ConsecutiveSamplesForRecovery"] = "1",
                    ["ResourceGuard:MinimumOverloadedDurationMs"] = "0",
                    ["ResourceGuard:MemoryDegradedPercent"] = "100",
                    ["ResourceGuard:MemoryOverloadedPercent"] = "100",
                    ["ResourceGuard:CpuDegradedPercent"] = "100",
                    ["ResourceGuard:CpuOverloadedPercent"] = "100",
                    ["ResourceGuard:ThreadPoolQueuePerCoreDegradedThreshold"] = "1000000",
                    ["ResourceGuard:ThreadPoolQueuePerCoreOverloadedThreshold"] = "1000000",
                    ["ResourceGuard:ActiveRequestsDegradedThreshold"] = "1000000",
                    ["ResourceGuard:ActiveRequestsOverloadedThreshold"] = "1000000",
                    ["ResourceGuard:LiveQueueDepthDegradedThreshold"] = "1000000",
                    ["ResourceGuard:LiveQueueDepthOverloadedThreshold"] = "1000000",
                    ["ResourceGuard:DrainCapacityRatioDegradedThreshold"] = "1.3",
                    ["ResourceGuard:DrainCapacityRatioOverloadedThreshold"] = "1.8",
                    ["ResourceGuard:BypassPaths:0"] = "/health/live",
                    ["ResourceGuard:BypassPaths:1"] = "/health/ready",
                    ["ResourceGuard:BypassPaths:2"] = "/health/node-state"
                });
            });
        }
    }

    private sealed class FixedPressureProvider : IIngestionPressureSnapshotProvider
    {
        private readonly bool _overloaded;

        public FixedPressureProvider(bool overloaded)
        {
            _overloaded = overloaded;
        }

        public IngestionPressureSnapshot CaptureSnapshot()
            => _overloaded
                ? new IngestionPressureSnapshot(2.5, 100, 20, 2048, DateTime.UtcNow)
                : new IngestionPressureSnapshot(0.2, 5, 20, 0, DateTime.UtcNow);
    }
}
