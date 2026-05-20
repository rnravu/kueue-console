namespace KueueConsole.Web.Models;

public class DashboardSummaryDto
{
    public int TotalClusterQueues { get; set; }
    public int TotalLocalQueues { get; set; }
    public int ActiveWorkloads { get; set; }
    public int PendingWorkloads { get; set; }
    public int FailedWorkloads { get; set; }
    public int CompletedWorkloads { get; set; }
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}
