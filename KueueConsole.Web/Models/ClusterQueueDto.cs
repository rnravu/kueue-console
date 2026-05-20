namespace KueueConsole.Web.Models;

public class ClusterQueueDto
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "Unknown";
    public int PendingWorkloads { get; set; }
    public int AdmittedWorkloads { get; set; }
    public string NominalCpu { get; set; } = "";
    public string NominalMemory { get; set; } = "";
    public string UsedCpu { get; set; } = "";
    public string UsedMemory { get; set; } = "";
    public string Age { get; set; } = "";
}
