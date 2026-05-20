namespace KueueConsole.Web.Models;

/// <summary>A single condition entry from a workload's status.conditions array.</summary>
public class WorkloadCondition
{
    public string Type { get; set; } = "";
    public string ConditionStatus { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Message { get; set; } = "";
    public string LastTransitionTime { get; set; } = "";
}

public class WorkloadRow
{
    public string Name { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string Queue { get; set; } = "";
    public string ReservedIn { get; set; } = "";
    public bool Admitted { get; set; }
    public bool Finished { get; set; }
    public string Age { get; set; } = "";
}

/// <summary>Enhanced DTO used by the Jobs / Dashboard pages.</summary>
public class WorkloadDto
{
    public string Name { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string Queue { get; set; } = "";
    public string ClusterQueue { get; set; } = "";
    /// <summary>The owning Kubernetes Job name (from ownerReferences). Used for delete.</summary>
    public string JobName { get; set; } = "";
    /// <summary>Running | Pending | Failed | Completed | Unknown</summary>
    public string Status { get; set; } = "Unknown";
    public string Message { get; set; } = "";
    public string Age { get; set; } = "";
    public DateTimeOffset? CreatedAt { get; set; }
    public bool Admitted { get; set; }
    public bool Finished { get; set; }
    /// <summary>All conditions from status.conditions, populated for diagnostics.</summary>
    public List<WorkloadCondition> Conditions { get; set; } = [];
}