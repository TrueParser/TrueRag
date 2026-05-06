using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrueRag.Storage.Migrations;

namespace TrueRag.Host.Migrations;

public static class MigrationCommandHandler
{
    private static readonly Meter MigrationMeter = new("TrueRag.Host.Migrations");
    private static readonly Counter<long> CommandTotal =
        MigrationMeter.CreateCounter<long>("truerag_migration_commands_total");
    private static readonly Counter<long> AppliedTotal =
        MigrationMeter.CreateCounter<long>("truerag_migration_applied_total");
    private static readonly Histogram<double> DurationSeconds =
        MigrationMeter.CreateHistogram<double>("truerag_migration_duration_seconds");

    public static bool TryParse(string[] args, out string command)
    {
        command = string.Empty;
        if (args.Length < 2)
        {
            return false;
        }

        if (!string.Equals(args[0], "migrate", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidate = args[1].Trim();
        if (string.Equals(candidate, "up", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "status", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "validate", StringComparison.OrdinalIgnoreCase))
        {
            command = candidate.ToLowerInvariant();
            return true;
        }

        return false;
    }

    public static async Task<bool> TryHandleAsync(
        string[] args,
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!TryParse(args, out var command))
        {
            return false;
        }

        var stopwatch = Stopwatch.StartNew();
        var migration = services.GetRequiredService<ISchemaMigrationService>();

        try
        {
            switch (command)
            {
                case "up":
                {
                    var before = await migration.GetPlanAsync(cancellationToken);
                    var after = await migration.ApplyPendingAsync(cancellationToken);
                    var applied = before.Pending.Count - after.Pending.Count;
                    logger.LogInformation(
                        "Migration command {Command} completed. applied={Applied} pending={Pending} alreadyApplied={AppliedCount}",
                        command,
                        Math.Max(0, applied),
                        after.Pending.Count,
                        after.Applied.Count);
                    AppliedTotal.Add(Math.Max(0, applied));
                    break;
                }
                case "status":
                {
                    var plan = await migration.GetPlanAsync(cancellationToken);
                    logger.LogInformation(
                        "Migration command {Command} completed. pending={Pending} applied={Applied} pendingVersions={PendingVersions}",
                        command,
                        plan.Pending.Count,
                        plan.Applied.Count,
                        string.Join(",", plan.Pending.Select(static p => p.Version)));
                    break;
                }
                case "validate":
                {
                    var drift = await migration.ValidateChecksumsAsync(cancellationToken);
                    if (drift.Count > 0)
                    {
                        logger.LogError(
                            "Migration command {Command} detected checksum drift. driftCount={DriftCount} versions={DriftVersions}",
                            command,
                            drift.Count,
                            string.Join(",", drift.Select(static d => d.Version)));
                        Environment.ExitCode = 2;
                    }
                    else
                    {
                        logger.LogInformation("Migration command {Command} completed. checksum drift not detected.", command);
                    }

                    break;
                }
            }

            CommandTotal.Add(1, new KeyValuePair<string, object?>("command", command), new KeyValuePair<string, object?>("status", "success"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Migration command {Command} failed.", command);
            CommandTotal.Add(1, new KeyValuePair<string, object?>("command", command), new KeyValuePair<string, object?>("status", "failed"));
            Environment.ExitCode = 1;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            DurationSeconds.Record(stopwatch.Elapsed.TotalSeconds, new KeyValuePair<string, object?>("command", command));
        }

        return true;
    }
}
