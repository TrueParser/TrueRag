using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrueRag.Ingestion.Wal;

namespace TrueRag.Workers;

internal sealed class IngestionWalPruneService : BackgroundService
{
    private readonly IIngestionAcceptanceLog _acceptanceLog;
    private readonly IWalReadLeaseTracker _leaseTracker;
    private readonly ILogger<IngestionWalPruneService> _logger;

    public IngestionWalPruneService(
        IIngestionAcceptanceLog acceptanceLog,
        IWalReadLeaseTracker leaseTracker,
        ILogger<IngestionWalPruneService> logger)
    {
        _acceptanceLog = acceptanceLog;
        _leaseTracker = leaseTracker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var walPath in _acceptanceLog.EnumerateLaneFiles())
                {
                    if (_leaseTracker.IsLeased(walPath))
                    {
                        continue;
                    }

                    var completionMarkers = Directory.EnumerateFiles(
                        Path.GetDirectoryName(walPath) ?? string.Empty,
                        $"{Path.GetFileName(walPath)}.completed.*",
                        SearchOption.TopDirectoryOnly);

                    if (!completionMarkers.Any())
                    {
                        continue;
                    }

                    var checkpointPath = walPath + ".checkpoint";
                    if (!File.Exists(checkpointPath))
                    {
                        continue;
                    }

                    if (IsFileIdle(walPath))
                    {
                        File.Delete(walPath);
                        foreach (var marker in completionMarkers)
                        {
                            File.Delete(marker);
                        }

                        File.Delete(checkpointPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WAL prune sweep failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    private static bool IsFileIdle(string path)
    {
        var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);
        return age > TimeSpan.FromSeconds(30);
    }
}
