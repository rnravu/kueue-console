using k8s;
using k8s.Models;
using System.Text.Json;
using KueueConsole.Web.Models;

namespace KueueConsole.Web.Services.Watchers;

/// <summary>
/// Kubernetes watch-based background service for ClusterQueues.
/// On ADDED/MODIFIED → updates ClusterQueueService state and broadcasts queue_update.
/// On DELETED → removes from state and broadcasts.
/// Reconnects automatically on stream error.
/// </summary>
public class ClusterQueueWatchService : BackgroundService
{
    private readonly IKubernetes _client;
    private readonly ClusterQueueService _store;
    private readonly ClusterEventService _events;
    private readonly DashboardAggregatorService _dashboard;
    private readonly ILogger<ClusterQueueWatchService> _logger;

    public ClusterQueueWatchService(
        IKubernetes client,
        ClusterQueueService store,
        ClusterEventService events,
        DashboardAggregatorService dashboard,
        ILogger<ClusterQueueWatchService> logger)
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
                await _store.LoadInitialAsync();

                // Push a full snapshot to all SSE clients so they sync immediately
                // on every (re)connect without waiting for the next watch event.
                await _events.BroadcastEventAsync("queue_update", "clusterqueues", "snapshot", _store.GetAll());
                await _events.BroadcastEventAsync("dashboard_update", "dashboard", "snapshot", _dashboard.GetSummary());

                var response = _client.CustomObjects.ListClusterCustomObjectWithHttpMessagesAsync(
                    "kueue.x-k8s.io", "v1beta1", "clusterqueues",
                    watch: true,
                    cancellationToken: stoppingToken);

#pragma warning disable CS0618
                await foreach (var (type, item) in response.WatchAsync<object, object>(
                    onError: ex => _logger.LogWarning(ex, "ClusterQueue watch error"),
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
                            var name = KubeHelpers.GetString(root.GetProperty("metadata"), "name");
                            _store.Remove(name);
                            await _events.BroadcastEventAsync("queue_update", "clusterqueues", "delete", new { name });
                        }
                        else
                        {
                            var dto = ClusterQueueService.ParseItem(root);
                            _store.Apply(dto);
                            await _events.BroadcastEventAsync("queue_update", "clusterqueues", "upsert", dto);
                        }

                        await _events.BroadcastEventAsync("dashboard_update", "dashboard", "snapshot", _dashboard.GetSummary());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing ClusterQueue watch event");
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ClusterQueue watch stream failed; reconnecting in 5s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
