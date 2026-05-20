using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using KueueConsole.Web.Models;

namespace KueueConsole.Web.Services;

public class ClusterEventService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ConcurrentDictionary<Guid, ChannelWriter<string>> _subscribers = new();

    public Guid Subscribe(ChannelWriter<string> writer)
    {
        var id = Guid.NewGuid();
        _subscribers[id] = writer;
        return id;
    }

    public void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var writer))
        {
            try { writer.TryComplete(); } catch { }
        }
    }

    /// <summary>Broadcasts a typed SSE envelope to all connected clients.</summary>
    /// <param name="action">"snapshot" | "upsert" | "delete"</param>
    public Task BroadcastEventAsync(string type, string resource, string action, object data)
    {
        var envelope = new ClusterEventDto
        {
            Type = type,
            Resource = resource,
            Action = action,
            Data = data,
            Timestamp = DateTimeOffset.UtcNow
        };
        var json = JsonSerializer.Serialize(envelope, _jsonOptions);
        return BroadcastRawAsync(json);
    }

    public async Task BroadcastRawAsync(string message)
    {
        foreach (var kv in _subscribers)
        {
            try
            {
                await kv.Value.WriteAsync(message);
            }
            catch
            {
                // ignore individual subscriber failures
            }
        }
    }
}
