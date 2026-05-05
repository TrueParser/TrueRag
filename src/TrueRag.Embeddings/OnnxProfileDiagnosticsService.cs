using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrueRag.Embeddings.Configuration;
using TrueRag.Embeddings.Onnx;

namespace TrueRag.Embeddings;

internal sealed class OnnxProfileDiagnosticsService(
    IOptions<OnnxProfileSelectionOptions> selectionOptions,
    IOnnxModelProfileRegistry profileRegistry,
    ILogger<OnnxProfileDiagnosticsService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var options = selectionOptions.Value;
        var defaultProfile = profileRegistry.GetRequiredProfile(options.DefaultProfileName);

        logger.LogInformation(
            "Embedding default profile '{ProfileName}' ({ModelId}, dims={Dimensions}) is active.",
            defaultProfile.Name,
            defaultProfile.ModelId,
            defaultProfile.Dimensions);

        foreach (var scoped in options.ScopedProfiles)
        {
            var profile = profileRegistry.GetRequiredProfile(scoped.ProfileName);
            logger.LogInformation(
                "Embedding scoped profile '{ProfileName}' mapped to tenant={TenantId}, app={AppId}, collection={CollectionId}.",
                profile.Name,
                scoped.TenantId,
                scoped.AppId,
                scoped.CollectionId);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
