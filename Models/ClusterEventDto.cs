namespace KueueConsole.Web.Models;

/// <summary>
/// Typed SSE envelope sent over GET /api/stream.
/// type values: workload_update | queue_update | dashboard_update
/// action values: snapshot | upsert | delete
/// </summary>
public class ClusterEventDto
{
    public string Type { get; set; } = "";
    public string Resource { get; set; } = "";
    /// <summary>"snapshot" = full list, "upsert" = single item added/modified, "delete" = item removed.</summary>
    public string Action { get; set; } = "";
    public object? Data { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
