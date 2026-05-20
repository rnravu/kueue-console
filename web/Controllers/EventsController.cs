using Microsoft.AspNetCore.Mvc;
using System.Threading.Channels;
using KueueConsole.Web.Services;

namespace KueueConsole.Web.Controllers;

/// <summary>
/// Legacy SSE endpoint kept for backward compatibility.
/// New clients should use GET /api/stream.
/// </summary>
[ApiController]
[Route("events")]
public class EventsController : ControllerBase
{
    private readonly ClusterEventService _events;
    private readonly ILogger<EventsController> _logger;

    public EventsController(ClusterEventService events, ILogger<EventsController> logger)
    {
        _events = events;
        _logger = logger;
    }

    [HttpGet("watch")]
    public async Task Watch()
    {
        HttpContext.Response.Headers.Append("Cache-Control", "no-cache");
        HttpContext.Response.ContentType = "text/event-stream";
        var ct = HttpContext.RequestAborted;
        var channel = Channel.CreateUnbounded<string>();
        var id = _events.Subscribe(channel.Writer);
        try
        {
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
}
