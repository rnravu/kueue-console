namespace KueueConsole.Web.Models;

public class DeleteJobRequest
{
    /// <summary>Also delete the associated Kueue Workload if found (default true for pending jobs).</summary>
    public bool DeleteWorkload { get; set; } = true;
}

public class DeleteJobResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public bool JobDeleted { get; set; }
    public bool WorkloadDeleted { get; set; }
    public string? Error { get; set; }
    public List<string> Steps { get; set; } = [];
}
