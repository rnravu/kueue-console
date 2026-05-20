using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Threading.Channels;
using KueueConsole.Web.Models;
using KueueConsole.Web.Services;

namespace KueueConsole.Web.Controllers;

/// <summary>
/// Primary SSE endpoint: GET /api/stream
/// Sends typed ClusterEventDto JSON envelopes for all resource changes.
/// Immediately pushes current cluster state to every new subscriber so the UI
/// never needs to wait for the next watch event to get populated.
/// </summary>
[Route("api/stream")]
public class StreamController : ControllerBase
{
    private readonly ClusterEventService _events;
    private readonly WorkloadStateService _workloads;
    private readonly ClusterQueueService _clusterQueues;
    private readonly LocalQueueStateService _localQueues;
    private readonly DashboardAggregatorService _dashboard;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public StreamController(
        ClusterEventService events,
        WorkloadStateService workloads,
        ClusterQueueService clusterQueues,
        LocalQueueStateService localQueues,
        DashboardAggregatorService dashboard)
    {
        _events = events;
        _workloads = workloads;
        _clusterQueues = clusterQueues;
        _localQueues = localQueues;
        _dashboard = dashboard;
    }

    [HttpGet]
    public async Task Stream()
    {
        HttpContext.Response.Headers.Append("Cache-Control", "no-cache");
        HttpContext.Response.Headers.Append("X-Accel-Buffering", "no");
        HttpContext.Response.ContentType = "text/event-stream";

        var ct = HttpContext.RequestAborted;
        var channel = Channel.CreateUnbounded<string>();

        // Subscribe FIRST so no future events are missed
        var id = _events.Subscribe(channel.Writer);
        try
        {
            // Send full current state snapshot immediately so the UI populates
            // even if the browser connected before/during the initial watch load.
            await SendSnapshotAsync(ct);

            await foreach (var msg in channel.Reader.ReadAllAsync(ct))
            {
                await HttpContext.Response.WriteAsync($"data: {msg}\n\n", ct);
                await HttpContext.Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _events.Unsubscribe(id);
        }
    }

    private async Task SendSnapshotAsync(CancellationToken ct)
    {
        await WriteEventAsync("dashboard_update", "dashboard", "snapshot", _dashboard.GetSummary(), ct);
        await WriteEventAsync("queue_update", "clusterqueues", "snapshot", _clusterQueues.GetAll(), ct);
        await WriteEventAsync("queue_update", "localqueues", "snapshot", _localQueues.GetAll(), ct);
        await WriteEventAsync("workload_update", "workloads", "snapshot", _workloads.GetAll(), ct);
    }

    private async Task WriteEventAsync(string type, string resource, string action, object data, CancellationToken ct)
    {
        var envelope = new ClusterEventDto { Type = type, Resource = resource, Action = action, Data = data };
        var json = JsonSerializer.Serialize(envelope, _jsonOptions);
        await HttpContext.Response.WriteAsync($"data: {json}\n\n", ct);
        await HttpContext.Response.Body.FlushAsync(ct);
    }
}
