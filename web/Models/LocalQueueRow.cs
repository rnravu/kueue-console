namespace KueueConsole.Web.Models;

public class LocalQueueRow
{
    public string Name { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string Age { get; set; } = "";
}

/// <summary>Enhanced DTO used by the Queues page.</summary>
public class LocalQueueDto
{
    public string Name { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string ClusterQueue { get; set; } = "";
    public int PendingWorkloads { get; set; }
    public int AdmittedWorkloads { get; set; }
    public string Status { get; set; } = "Unknown";
    public string Age { get; set; } = "";
}
