namespace KueueConsole.Web.Models;

/// <summary>
/// Full diagnostic snapshot for a single workload, combining state from the
/// Kueue workload, its LocalQueue, and its ClusterQueue.
/// Returned by GET /api/jobs/{namespace}/{name}[/diagnostics].
/// </summary>
public class JobDiagnosticsDto
{
    // ── Identity ──────────────────────────────────────────────────────────────
    public string Name { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string QueueName { get; set; } = "";
    public string Status { get; set; } = "";
    public string Age { get; set; } = "";

    // ── Workload ──────────────────────────────────────────────────────────────
    public string WorkloadName { get; set; } = "";
    public bool WorkloadAdmitted { get; set; }
    public bool WorkloadFinished { get; set; }
    /// <summary>ClusterQueue name from the workload's admission block.</summary>
    public string AdmissionClusterQueue { get; set; } = "";
    /// <summary>All conditions from status.conditions.</summary>
    public List<WorkloadCondition> Conditions { get; set; } = [];

    // ── Plain-English diagnosis ───────────────────────────────────────────────
    /// <summary>
    /// Human-readable explanation of the current state, suitable for display
    /// directly in the UI without requiring Kueue knowledge.
    /// </summary>
    public string DiagnosticSummary { get; set; } = "";

    // ── Queue context ─────────────────────────────────────────────────────────
    public string LocalQueueStatus { get; set; } = "";
    public string ClusterQueueName { get; set; } = "";
    public string ClusterQueueStatus { get; set; } = "";

    // ── Job-level (derived from workload status) ──────────────────────────────
    /// <summary>True when status is Pending — the Kubernetes Job will be Suspended.</summary>
    public bool JobSuspended { get; set; }
    /// <summary>Suspended | Running | Complete | Failed</summary>
    public string JobPhase { get; set; } = "";

    // ── Root cause analysis ───────────────────────────────────────────────────
    /// <summary>
    /// Classified root cause code for pending/failed workloads.
    /// Values: QUEUE_MISSING | CLUSTER_QUEUE_INACTIVE | LOCAL_QUEUE_INACTIVE |
    ///         QUOTA_EXHAUSTED | PREEMPTED | CONFIG_INVALID | NOT_YET_EVALUATED | UNKNOWN
    /// </summary>
    public string RootCause { get; set; } = "";
    /// <summary>Actionable suggestion derived from RootCause, shown in the UI.</summary>
    public string SuggestedAction { get; set; } = "";
    /// <summary>Reason field from the Admitted=False workload condition.</summary>
    public string WorkloadReason { get; set; } = "";
    /// <summary>Message field from the Admitted=False workload condition.</summary>
    public string WorkloadMessage { get; set; } = "";
    /// <summary>Name of the LocalQueue (same as QueueName, exposed for UI convenience).</summary>
    public string LocalQueueName { get; set; } = "";
    // ── Quota context (from ClusterQueueDto) ─────────────────────────────────
    public string NominalCpu { get; set; } = "";
    public string NominalMemory { get; set; } = "";
    public string UsedCpu { get; set; } = "";
    public string UsedMemory { get; set; } = "";
}
