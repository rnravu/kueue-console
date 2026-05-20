using k8s;
using KueueConsole.Web.Models;

namespace KueueConsole.Web.Services;

/// <summary>
/// Deletes a Kubernetes Job and optionally cleans up its associated Kueue Workload.
/// </summary>
public class JobDeleteService
{
    private readonly IKubernetes _client;
    private readonly WorkloadStateService _workloads;
    private readonly ILogger<JobDeleteService> _logger;

    public JobDeleteService(
        IKubernetes client,
        WorkloadStateService workloads,
        ILogger<JobDeleteService> logger)
    {
        _client = client;
        _workloads = workloads;
        _logger = logger;
    }

    public async Task<DeleteJobResult> DeleteJobAsync(string ns, string name, bool deleteWorkload)
    {
        var result = new DeleteJobResult();

        // ── 1. Delete the Kubernetes Job ──────────────────────────────────────
        try
        {
            await _client.BatchV1.DeleteNamespacedJobAsync(
                name, ns,
                body: new k8s.Models.V1DeleteOptions { PropagationPolicy = "Foreground" });

            result.JobDeleted = true;
            result.Steps.Add($"✓ Job '{name}' deleted from namespace '{ns}'");
            _logger.LogInformation("Deleted Job {Ns}/{Name}", ns, name);
        }
        catch (k8s.Autorest.HttpOperationException ex) when
            (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // The K8s Job is already gone — still try to clean up the Kueue workload
            // directly by its workload name (handles orphaned workloads shown in the UI).
            result.JobDeleted = false;
            result.Steps.Add($"– Job '{name}' was not found (already deleted or name is a workload name)");

            // If the caller passed the workload name directly (e.g. "job-mjob-1"),
            // attempt to delete it as a Kueue workload resource.
            try
            {
                await _client.CustomObjects.DeleteNamespacedCustomObjectAsync(
                    "kueue.x-k8s.io", "v1beta1", ns, "workloads", name);
                result.WorkloadDeleted = true;
                result.Steps.Add($"✓ Workload '{name}' deleted directly");
                _logger.LogInformation("Deleted orphan Workload {Ns}/{Name}", ns, name);
            }
            catch (k8s.Autorest.HttpOperationException wex) when
                (wex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                result.Steps.Add($"– Workload '{name}' also not found — already fully cleaned up");
            }
            catch (Exception wex)
            {
                result.Steps.Add($"⚠ Could not clean up workload '{name}': {wex.Message}");
                _logger.LogWarning(wex, "Failed direct workload delete fallback for {Ns}/{Name}", ns, name);
            }

            result.Success = true;
            result.Message = $"Job '{name}' was already absent.";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Job {Ns}/{Name}", ns, name);
            result.Success = false;
            result.Error = ex.Message;
            result.Message = $"Failed to delete job: {ex.Message}";
            result.Steps.Add($"✗ Error deleting job: {ex.Message}");
            return result;
        }

        // ── 2. Optionally delete the Kueue Workload ───────────────────────────
        if (deleteWorkload)
        {
            // Kueue workload name convention: "job-{jobname}"
            var workloadName = FindWorkloadName(ns, name);
            if (workloadName is not null)
            {
                try
                {
                    await _client.CustomObjects.DeleteNamespacedCustomObjectAsync(
                        "kueue.x-k8s.io", "v1beta1", ns, "workloads", workloadName);

                    result.WorkloadDeleted = true;
                    result.Steps.Add($"✓ Workload '{workloadName}' deleted");
                    _logger.LogInformation("Deleted Workload {Ns}/{WorkloadName}", ns, workloadName);
                }
                catch (k8s.Autorest.HttpOperationException ex) when
                    (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    result.Steps.Add($"– Workload '{workloadName}' was not found (already deleted)");
                }
                catch (Exception ex)
                {
                    // Non-fatal: job deleted, workload cleanup failed
                    _logger.LogWarning(ex, "Could not delete Workload {Ns}/{WorkloadName}", ns, workloadName);
                    result.Steps.Add($"⚠ Could not delete workload '{workloadName}': {ex.Message}");
                }
            }
            else
            {
                result.Steps.Add("– No associated workload found in state store");
            }
        }

        result.Success = true;
        result.Message = result.JobDeleted
            ? $"Job '{name}' successfully deleted."
            : $"Job '{name}' was already absent.";
        return result;
    }

    /// <summary>
    /// Finds the workload name for a given job using the in-memory store.
    /// Kueue names workloads "job-{jobname}" by convention.
    /// </summary>
    private string? FindWorkloadName(string ns, string jobName)
    {
        var conventionalName = $"job-{jobName}";
        var allInNs = _workloads.GetByNamespace(ns);

        return allInNs.FirstOrDefault(w =>
            w.Name == conventionalName ||
            w.Name.EndsWith($"-{jobName}", StringComparison.OrdinalIgnoreCase))?.Name;
    }
}
