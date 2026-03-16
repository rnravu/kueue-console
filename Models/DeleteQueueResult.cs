namespace KueueConsole.Web.Models;

public class DeleteQueueResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    /// <summary>True when deletion is blocked because admitted workloads are still running.</summary>
    public bool Blocked { get; set; }
    public string? BlockReason { get; set; }
    public int AdmittedWorkloads { get; set; }
    public int PendingWorkloads { get; set; }
    public string? Error { get; set; }
}
