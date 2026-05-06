using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using TrueRag.Storage.Migrations;

namespace TrueRag.Host.Migrations;

public static class SchemaMigrationStartupPolicy
{
    public static async Task EnforceAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var options = services.GetRequiredService<IOptions<SchemaMigrationStartupOptions>>().Value;
        var migration = services.GetRequiredService<ISchemaMigrationService>();

        if (options.AutoMigrateOnStartup)
        {
            var planBefore = await migration.GetPlanAsync(cancellationToken);
            var planAfter = await migration.ApplyPendingAsync(cancellationToken);
            var applied = Math.Max(0, planBefore.Pending.Count - planAfter.Pending.Count);

            logger.LogInformation(
                "Startup migration auto-apply completed. applied={Applied} pending={Pending}",
                applied,
                planAfter.Pending.Count);
            return;
        }

        var plan = await migration.GetPlanAsync(cancellationToken);
        if (plan.Pending.Count == 0)
        {
            logger.LogInformation("Startup migration check completed. pending=0.");
            return;
        }

        logger.LogWarning(
            "Pending migrations detected at startup. pending={Pending} versions={PendingVersions}",
            plan.Pending.Count,
            string.Join(",", plan.Pending.Select(static p => p.Version)));

        if (options.FailFastOnPendingMigrations)
        {
            throw new InvalidOperationException(
                $"Pending schema migrations detected ({plan.Pending.Count}). Run 'migrate up' before starting host. Pending versions: {string.Join(",", plan.Pending.Select(static p => p.Version))}");
        }
    }
}
