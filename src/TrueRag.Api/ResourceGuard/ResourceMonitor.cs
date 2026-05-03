using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrueRag.Ingestion.Admission;

namespace TrueRag.Api.ResourceGuard;

public sealed class ResourceMonitor : IResourceMonitor, IHostedService, IDisposable
{
    private readonly ResourceGuardOptions _options;
    private readonly ILogger<ResourceMonitor> _logger;
    private readonly IIngestionPressureSnapshotProvider _pressureProvider;
    private readonly CancellationTokenSource _shutdown = new();
    private PeriodicTimer? _timer;
    private Task? _runner;
    private long _activeRequests;
    private int _consecutiveOverload;
    private int _consecutiveHealthy;
    private DateTime? _overloadedSinceUtc;
    private ResourceSnapshot _current;
    private TimeSpan _prevCpu;
    private DateTime _prevSampleUtc;

    public ResourceMonitor(
        IOptions<ResourceGuardOptions> options,
        IIngestionPressureSnapshotProvider pressureProvider,
        ILogger<ResourceMonitor> logger)
    {
        _options = options.Value;
        _pressureProvider = pressureProvider;
        _logger = logger;
        _prevCpu = Process.GetCurrentProcess().TotalProcessorTime;
        _prevSampleUtc = DateTime.UtcNow;
        _current = new ResourceSnapshot(0, 0, 0, 0, 0, 0, 0, 0, NodeState.Healthy, null, DateTime.UtcNow);
    }

    public ResourceSnapshot Current => _current;

    public long IncrementActiveRequests() => Interlocked.Increment(ref _activeRequests);

    public long DecrementActiveRequests() => Interlocked.Decrement(ref _activeRequests);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return Task.CompletedTask;
        }

        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.SampleIntervalMs));
        _runner = Task.Run(SampleLoopAsync, _shutdown.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _shutdown.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        if (_runner is not null)
        {
            await _runner.WaitAsync(cancellationToken);
        }
    }

    private async Task SampleLoopAsync()
    {
        if (_timer is null)
        {
            return;
        }

        try
        {
            while (await _timer.WaitForNextTickAsync(_shutdown.Token))
            {
                Sample();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Sample()
    {
        try
        {
            var now = DateTime.UtcNow;
            var mem = MeasureMemoryPercent();
            var cpu = MeasureCpuPercent(now);
            var tpQueue = ThreadPool.PendingWorkItemCount;
            var active = Interlocked.Read(ref _activeRequests);
            var pressure = _pressureProvider.CaptureSnapshot();
            _current = Evaluate(mem, cpu, tpQueue, active, pressure, now, _current);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sample resource state.");
        }
    }

    private ResourceSnapshot Evaluate(
        double memPct,
        double cpuPct,
        long tpQueue,
        long activeReqs,
        IngestionPressureSnapshot pressure,
        DateTime sampledAtUtc,
        ResourceSnapshot current)
    {
        var tpPerCore = tpQueue / (double)Math.Max(1, Environment.ProcessorCount);
        var overloadedReasons = new List<string>();
        var degradedReasons = new List<string>();

        if (memPct >= _options.MemoryOverloadedPercent) overloadedReasons.Add("memory");
        if (cpuPct >= _options.CpuOverloadedPercent) overloadedReasons.Add("cpu");
        if (tpPerCore >= _options.ThreadPoolQueuePerCoreOverloadedThreshold) overloadedReasons.Add("threadpool");
        if (activeReqs >= _options.ActiveRequestsOverloadedThreshold) overloadedReasons.Add("active_requests");
        if (pressure.DrainCapacityRatio >= _options.DrainCapacityRatioOverloadedThreshold) overloadedReasons.Add("wal_drain_ratio");
        if (pressure.TotalLiveDepth >= _options.LiveQueueDepthOverloadedThreshold) overloadedReasons.Add("live_queue_depth");

        if (overloadedReasons.Count == 0)
        {
            if (memPct >= _options.MemoryDegradedPercent) degradedReasons.Add("memory");
            if (cpuPct >= _options.CpuDegradedPercent) degradedReasons.Add("cpu");
            if (tpPerCore >= _options.ThreadPoolQueuePerCoreDegradedThreshold) degradedReasons.Add("threadpool");
            if (activeReqs >= _options.ActiveRequestsDegradedThreshold) degradedReasons.Add("active_requests");
            if (pressure.DrainCapacityRatio >= _options.DrainCapacityRatioDegradedThreshold) degradedReasons.Add("wal_drain_ratio");
            if (pressure.TotalLiveDepth >= _options.LiveQueueDepthDegradedThreshold) degradedReasons.Add("live_queue_depth");
        }

        if (overloadedReasons.Count > 0)
        {
            _consecutiveOverload++;
            _consecutiveHealthy = 0;
        }
        else if (degradedReasons.Count > 0)
        {
            _consecutiveOverload = 0;
            _consecutiveHealthy = 0;
        }
        else
        {
            _consecutiveOverload = 0;
            _consecutiveHealthy++;
        }

        var nextState = current.State;
        var nextReason = current.DegradationReason;
        if (overloadedReasons.Count > 0)
        {
            if (current.State != NodeState.Overloaded)
            {
                _overloadedSinceUtc = sampledAtUtc;
            }

            nextState = _consecutiveOverload >= _options.ConsecutiveSamplesForOverload ? NodeState.Overloaded : NodeState.Degraded;
            nextReason = string.Join(",", overloadedReasons);
        }
        else if (current.State == NodeState.Overloaded &&
                 _overloadedSinceUtc.HasValue &&
                 sampledAtUtc - _overloadedSinceUtc.Value < TimeSpan.FromMilliseconds(_options.MinimumOverloadedDurationMs))
        {
            nextState = NodeState.Overloaded;
            nextReason = "holding_min_overload_duration";
        }
        else if (degradedReasons.Count > 0)
        {
            nextState = NodeState.Degraded;
            nextReason = string.Join(",", degradedReasons);
        }
        else if (current.State is NodeState.Degraded or NodeState.Overloaded &&
                 _consecutiveHealthy < _options.ConsecutiveSamplesForRecovery)
        {
            nextState = NodeState.Degraded;
            nextReason = "recovering";
        }
        else
        {
            nextState = NodeState.Healthy;
            nextReason = null;
            _overloadedSinceUtc = null;
        }

        return new ResourceSnapshot(
            memPct,
            cpuPct,
            tpQueue,
            activeReqs,
            pressure.DrainCapacityRatio,
            pressure.AcceptedItemsPerSec,
            pressure.DrainedItemsPerSec,
            pressure.TotalLiveDepth,
            nextState,
            nextReason,
            sampledAtUtc);
    }

    private static double MeasureMemoryPercent()
    {
        var info = GC.GetGCMemoryInfo();
        if (info.TotalAvailableMemoryBytes <= 0)
        {
            return 0;
        }

        return Math.Clamp((double)info.MemoryLoadBytes / info.TotalAvailableMemoryBytes * 100, 0, 100);
    }

    private double MeasureCpuPercent(DateTime nowUtc)
    {
        var process = Process.GetCurrentProcess();
        var currentCpu = process.TotalProcessorTime;
        var elapsedMs = (nowUtc - _prevSampleUtc).TotalMilliseconds;
        if (elapsedMs <= 0)
        {
            return 0;
        }

        var deltaCpu = (currentCpu - _prevCpu).TotalMilliseconds;
        _prevCpu = currentCpu;
        _prevSampleUtc = nowUtc;
        return Math.Clamp(deltaCpu / (Environment.ProcessorCount * elapsedMs) * 100, 0, 100);
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _shutdown.Dispose();
    }
}
