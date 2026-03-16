using k8s;
using k8s.Models;
using System.Text.Json;
using KueueConsole.Web.Models;

namespace KueueConsole.Web.Services.Watchers;

/// <summary>
/// Kubernetes watch-based background service for Workloads (all namespaces).
/// Replaces the previous polling-based WorkloadWatcherBackgroundService.
/// </summary>
public class WorkloadWatchService : BackgroundService
{
    private readonly IKubernetes _client;
    private readonly WorkloadStateService _store;
    private readonly ClusterEventService _events;
    private readonly DashboardAggregatorService _dashboard;
    private readonly ILogger<WorkloadWatchService> _logger;

    public WorkloadWatchService(
        IKubernetes client,
        WorkloadStateService store,
        ClusterEventService events,
        DashboardAggregatorService dashboard,
        ILogger<WorkloadWatchService> logger)
    {
        _client = client;
        _store = store;
        _events = events;
        _dashboard = dashboard;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Clear stale state before reloading so deleted items are never
                // left behind across reconnects.
                _store.Clear();

                // LoadInitialAsync now returns the list's resourceVersion.
                // We pass that to the watch so the API server streams only changes
                // that occurred AFTER the snapshot — no stale ADDED replays.
                var resourceVersion = await _store.LoadInitialAsync();

                // Push a full snapshot to all SSE clients so they sync immediately
                // on every (re)connect without waiting for the next watch event.
                await _events.BroadcastEventAsync("workload_update", "workloads", "snapshot", _store.GetAll());
                await _events.BroadcastEventAsync("dashboard_update", "dashboard", "snapshot", _dashboard.GetSummary());

                var response = _client.CustomObjects.ListClusterCustomObjectWithHttpMessagesAsync(
                    "kueue.x-k8s.io", "v1beta1", "workloads",
                    watch: true,
                    // Starting from the list's resourceVersion means the API server
                    // will only send Modified/Deleted events for changes that happened
                    // AFTER our snapshot — never ADDED events for already-known objects.
                    resourceVersion: string.IsNullOrEmpty(resourceVersion) ? null : resourceVersion,
                    cancellationToken: stoppingToken);

#pragma warning disable CS0618
                await foreach (var (type, item) in response.WatchAsync<object, object>(
                    onError: ex => _logger.LogWarning(ex, "Workload watch error"),
                    cancellationToken: stoppingToken))
#pragma warning restore CS0618
                {
                    try
                    {
                        var json = JsonSerializer.Serialize(item);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        if (type == WatchEventType.Deleted)
                        {
                            var meta = root.GetProperty("metadata");
                            var ns = KubeHelpers.GetString(meta, "namespace");
                            var name = KubeHelpers.GetString(meta, "name");
                            _store.Remove(ns, name);
                            await _events.BroadcastEventAsync("workload_update", "workloads", "delete", new { name, @namespace = ns });
                        }
                        else if (type == WatchEventType.Modified || type == WatchEventType.Added)
                        {
                            // Process both Modified AND Added events.
                            // Because the watch is anchored to the list's resourceVersion,
                            // Added events here are genuinely new objects created AFTER the
                            // snapshot — not stale cache replays of already-known objects.
                            var dto = WorkloadStateService.ParseItem(root);
                            _store.Apply(dto);
                            await _events.BroadcastEventAsync("workload_update", "workloads", "upsert", dto);
                        }

                        await _events.BroadcastEventAsync("dashboard_update", "dashboard", "snapshot", _dashboard.GetSummary());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing Workload watch event");
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Workload watch stream failed; reconnecting in 5s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
