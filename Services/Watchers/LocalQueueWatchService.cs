using k8s;
using k8s.Models;
using System.Text.Json;
using KueueConsole.Web.Models;

namespace KueueConsole.Web.Services.Watchers;

/// <summary>
/// Kubernetes watch-based background service for LocalQueues (all namespaces).
/// </summary>
public class LocalQueueWatchService : BackgroundService
{
    private readonly IKubernetes _client;
    private readonly LocalQueueStateService _store;
    private readonly ClusterEventService _events;
    private readonly DashboardAggregatorService _dashboard;
    private readonly ILogger<LocalQueueWatchService> _logger;

    public LocalQueueWatchService(
        IKubernetes client,
        LocalQueueStateService store,
        ClusterEventService events,
        DashboardAggregatorService dashboard,
        ILogger<LocalQueueWatchService> logger)
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
                await _events.BroadcastEventAsync("queue_update", "localqueues", "snapshot", _store.GetAll());
                await _events.BroadcastEventAsync("dashboard_update", "dashboard", "snapshot", _dashboard.GetSummary());

                // Watch across all namespaces
                var response = _client.CustomObjects.ListClusterCustomObjectWithHttpMessagesAsync(
                    "kueue.x-k8s.io", "v1beta1", "localqueues",
                    watch: true,
                    cancellationToken: stoppingToken);

#pragma warning disable CS0618
                await foreach (var (type, item) in response.WatchAsync<object, object>(
                    onError: ex => _logger.LogWarning(ex, "LocalQueue watch error"),
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
                            await _events.BroadcastEventAsync("queue_update", "localqueues", "delete", new { name, @namespace = ns });
                        }
                        else
                        {
                            var dto = LocalQueueStateService.ParseItem(root);
                            _store.Apply(dto);
                            await _events.BroadcastEventAsync("queue_update", "localqueues", "upsert", dto);
                        }

                        await _events.BroadcastEventAsync("dashboard_update", "dashboard", "snapshot", _dashboard.GetSummary());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing LocalQueue watch event");
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LocalQueue watch stream failed; reconnecting in 5s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
