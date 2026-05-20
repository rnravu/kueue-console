using KueueConsole.Web.Models;

namespace KueueConsole.Web.Services;

/// <summary>
/// Builds a <see cref="JobDiagnosticsDto"/> for a workload, combining in-memory
/// state from the workload store, LocalQueue store, and ClusterQueue store.
/// No live Kubernetes calls are made — this is intentionally fast and hermetic.
/// </summary>
public class JobDiagnosticsService
{
    private readonly WorkloadStateService _workloads;
    private readonly LocalQueueStateService _localQueues;
    private readonly ClusterQueueService _clusterQueues;

    public JobDiagnosticsService(
        WorkloadStateService workloads,
        LocalQueueStateService localQueues,
        ClusterQueueService clusterQueues)
    {
        _workloads = workloads;
        _localQueues = localQueues;
        _clusterQueues = clusterQueues;
    }

    /// <summary>
    /// Returns diagnostics for the workload at <paramref name="ns"/>/<paramref name="name"/>,
    /// or <c>null</c> if the workload is not found in the in-memory store.
    /// </summary>
    public JobDiagnosticsDto? BuildDiagnostics(string ns, string name)
    {
        var workload = _workloads.GetAll()
            .FirstOrDefault(w => w.Namespace == ns && w.Name == name);

        if (workload is null) return null;

        // Resolve queue context from in-memory stores
        var localQueue = string.IsNullOrEmpty(workload.Queue)
            ? null
            : _localQueues.GetByNamespace(ns).FirstOrDefault(q => q.Name == workload.Queue);

        var cqName = !string.IsNullOrEmpty(workload.ClusterQueue)
            ? workload.ClusterQueue
            : (localQueue?.ClusterQueue ?? "");

        var clusterQueue = string.IsNullOrEmpty(cqName)
            ? null
            : _clusterQueues.GetAll().FirstOrDefault(q => q.Name == cqName);

        var rootCause = ClassifyRootCause(workload, localQueue, clusterQueue);
        var inadmissibleCond = workload.Conditions
            .FirstOrDefault(c => c.Type == "Admitted" && c.ConditionStatus == "False");

        return new JobDiagnosticsDto
        {
            Name                  = workload.Name,
            Namespace             = workload.Namespace,
            QueueName             = workload.Queue,
            Status                = workload.Status,
            Age                   = workload.Age,

            WorkloadName          = workload.Name,
            WorkloadAdmitted      = workload.Admitted,
            WorkloadFinished      = workload.Finished,
            AdmissionClusterQueue = workload.ClusterQueue,
            Conditions            = workload.Conditions,

            DiagnosticSummary     = BuildDiagnosticSummary(workload, cqName),

            LocalQueueStatus      = localQueue?.Status ?? "",
            LocalQueueName        = workload.Queue,
            ClusterQueueName      = cqName,
            ClusterQueueStatus    = clusterQueue?.Status ?? "",

            JobSuspended = workload.Status is "Pending",
            JobPhase     = workload.Status switch
            {
                "Running"   => "Running",
                "Completed" => "Complete",
                "Failed"    => "Failed",
                _           => "Suspended",
            },

            RootCause        = rootCause,
            SuggestedAction  = BuildSuggestedAction(rootCause),
            WorkloadReason   = inadmissibleCond?.Reason ?? "",
            WorkloadMessage  = inadmissibleCond?.Message ?? workload.Message,

            NominalCpu    = clusterQueue?.NominalCpu    ?? "",
            NominalMemory = clusterQueue?.NominalMemory ?? "",
            UsedCpu       = clusterQueue?.UsedCpu       ?? "",
            UsedMemory    = clusterQueue?.UsedMemory    ?? "",
        };
    }

    /// <summary>
    /// Produces a plain-English sentence explaining the workload's current state.
    /// Exposed as <c>internal static</c> so unit tests can exercise it directly.
    /// </summary>
    internal static string BuildDiagnosticSummary(WorkloadDto workload, string clusterQueueName)
    {
        return workload.Status switch
        {
            "Running" => $"Running \u2014 admitted to cluster queue '{(string.IsNullOrEmpty(clusterQueueName) ? "unknown" : clusterQueueName)}'.",

            "Completed" => "Completed \u2014 this workload finished successfully.",

            "Failed" => BuildFailedSummary(workload),

            _ => BuildPendingSummary(workload),   // Pending or Unknown
        };
    }

    private static string BuildFailedSummary(WorkloadDto workload)
    {
        var cond = workload.Conditions.FirstOrDefault(c =>
            c.Type == "Finished" && c.ConditionStatus == "True");
        var msg = cond?.Message ?? workload.Message;
        return string.IsNullOrEmpty(msg)
            ? "Failed \u2014 this workload finished with errors."
            : $"Failed \u2014 {msg}";
    }

    private static string BuildPendingSummary(WorkloadDto workload)
    {
        if (workload.Conditions.Count == 0 && string.IsNullOrEmpty(workload.Message))
            return "Pending \u2014 the workload has not been evaluated by the scheduler yet.";

        // Look for an Admitted=False condition first — it carries the most useful reason
        var inadmissible = workload.Conditions.FirstOrDefault(c =>
            c.Type == "Admitted" && c.ConditionStatus == "False");

        var rawMsg = inadmissible?.Message ?? workload.Message;

        if (string.IsNullOrEmpty(rawMsg))
            return "Pending \u2014 waiting to be admitted by the scheduler.";

        return TranslatePendingMessage(rawMsg, workload.Namespace);
    }

    /// <summary>
    /// Translates a raw Kueue admission message into a friendly sentence.
    /// Exposed as <c>internal static</c> for unit testing.
    /// </summary>
    internal static string TranslatePendingMessage(string message, string ns)
    {
        var lower = message.ToLowerInvariant();

        if (lower.Contains("doesn't exist") || lower.Contains("does not exist") || lower.Contains("not found"))
            return $"Pending because {message.TrimEnd('.')}. "
                 + $"Check that the LocalQueue exists in namespace '{ns}'.";

        if (lower.Contains("insufficient") || lower.Contains("quota") || lower.Contains("exceed"))
            return $"Pending because the queue does not have sufficient resource quota. ({message})";

        if (lower.Contains("inactive") || lower.Contains("not active"))
            return $"Pending because the ClusterQueue is not currently active. ({message})";

        return $"Pending: {message}";
    }

    /// <summary>
    /// Classifies the root cause of a pending/failed workload into a stable code
    /// that the UI uses to choose the appropriate action button.
    /// </summary>
    internal static string ClassifyRootCause(
        WorkloadDto workload,
        LocalQueueDto? localQueue,
        ClusterQueueDto? clusterQueue)
    {
        if (workload.Status is "Running")   return "RUNNING";
        if (workload.Status is "Completed") return "COMPLETED";

        // ── 1. Queue missing from state store ─────────────────────────────────
        if (localQueue is null && !string.IsNullOrEmpty(workload.Queue))
            return "QUEUE_MISSING";

        // ── 2. Cluster queue inactive ─────────────────────────────────────────
        if (clusterQueue is not null && clusterQueue.Status != "Active")
            return "CLUSTER_QUEUE_INACTIVE";

        // ── 3. Local queue inactive ───────────────────────────────────────────
        if (localQueue is not null && localQueue.Status != "Active")
            return "LOCAL_QUEUE_INACTIVE";

        // ── 4. Classify from workload conditions ──────────────────────────────
        var inadmissible = workload.Conditions
            .FirstOrDefault(c => c.Type == "Admitted" && c.ConditionStatus == "False");

        if (inadmissible is not null)
        {
            var msg    = (inadmissible.Message ?? "").ToLowerInvariant();
            var reason = (inadmissible.Reason  ?? "").ToLowerInvariant();

            if (msg.Contains("doesn't exist") || msg.Contains("does not exist") || msg.Contains("not found"))
                return "QUEUE_MISSING";
            if (msg.Contains("insufficient") || msg.Contains("quota") || msg.Contains("exceed"))
                return "QUOTA_EXHAUSTED";
            if (msg.Contains("inactive") || msg.Contains("not active"))
                return clusterQueue?.Status != "Active" ? "CLUSTER_QUEUE_INACTIVE" : "LOCAL_QUEUE_INACTIVE";
            if (reason.Contains("preempted"))
                return "PREEMPTED";
            if (msg.Contains("invalid") || msg.Contains("validation") || reason.Contains("invalid"))
                return "CONFIG_INVALID";
        }

        // ── 5. Also check the top-level message ───────────────────────────────
        var topMsg = (workload.Message ?? "").ToLowerInvariant();
        if (topMsg.Contains("doesn't exist") || topMsg.Contains("does not exist") || topMsg.Contains("not found"))
            return "QUEUE_MISSING";
        if (topMsg.Contains("insufficient") || topMsg.Contains("quota"))
            return "QUOTA_EXHAUSTED";

        // ── 6. No signals at all ──────────────────────────────────────────────
        if (workload.Conditions.Count == 0 && string.IsNullOrEmpty(workload.Message))
            return "NOT_YET_EVALUATED";

        return "UNKNOWN";
    }

    /// <summary>Builds a user-facing suggested action from a root cause code.</summary>
    internal static string BuildSuggestedAction(string rootCause) => rootCause switch
    {
        "QUEUE_MISSING"             => "Create the missing LocalQueue in this namespace, then the workload will be admitted automatically.",
        "CLUSTER_QUEUE_INACTIVE"    => "Check the ClusterQueue status. It may need to be reconfigured or have resources allocated.",
        "LOCAL_QUEUE_INACTIVE"      => "Check the LocalQueue status and verify it is bound to an active ClusterQueue.",
        "QUOTA_EXHAUSTED"           => "Wait for resources to become available, or increase the ClusterQueue resource quota.",
        "PREEMPTED"                 => "The workload was preempted and will be re-queued automatically. No action required.",
        "CONFIG_INVALID"            => "Review the workload or queue configuration. Use 'Edit Resource' to fix the issue.",
        "NOT_YET_EVALUATED"         => "The workload was just submitted. Wait a moment, then refresh diagnostics.",
        "RUNNING"                   => "The workload is running normally.",
        "COMPLETED"                 => "The workload has completed successfully.",
        _                           => "Check the workload conditions above for more details.",
    };
}
