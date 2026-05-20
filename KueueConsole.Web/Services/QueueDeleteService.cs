using k8s;
using KueueConsole.Web.Models;

namespace KueueConsole.Web.Services;

/// <summary>
/// Checks safety rules and deletes a LocalQueue.
/// Deletion is blocked when admitted workloads are still running against the queue.
/// </summary>
public class QueueDeleteService
{
    private readonly IKubernetes _client;
    private readonly LocalQueueStateService _localQueues;
    private readonly ILogger<QueueDeleteService> _logger;

    public QueueDeleteService(
        IKubernetes client,
        LocalQueueStateService localQueues,
        ILogger<QueueDeleteService> logger)
    {
        _client = client;
        _localQueues = localQueues;
        _logger = logger;
    }

    public async Task<DeleteQueueResult> DeleteLocalQueueAsync(string ns, string name)
    {
        var result = new DeleteQueueResult();

        // ── Safety check using in-memory state ────────────────────────────────
        var queue = _localQueues.GetByNamespace(ns).FirstOrDefault(q => q.Name == name);
        if (queue is not null)
        {
            result.AdmittedWorkloads = queue.AdmittedWorkloads;
            result.PendingWorkloads = queue.PendingWorkloads;

            if (queue.AdmittedWorkloads > 0)
            {
                result.Success = false;
                result.Blocked = true;
                result.BlockReason =
                    $"Cannot delete LocalQueue '{name}': " +
                    $"{queue.AdmittedWorkloads} workload(s) are currently admitted and running. " +
                    $"Wait for them to finish or drain the queue first.";
                result.Message = result.BlockReason;
                return result;
            }
        }

        // ── Perform deletion ──────────────────────────────────────────────────
        try
        {
            await _client.CustomObjects.DeleteNamespacedCustomObjectAsync(
                "kueue.x-k8s.io", "v1beta1", ns, "localqueues", name);

            result.Success = true;
            result.Message = $"LocalQueue '{name}' deleted from namespace '{ns}'.";

            if (result.PendingWorkloads > 0)
                result.Message +=
                    $" Warning: {result.PendingWorkloads} pending workload(s) " +
                    "were waiting on this queue and may now be stuck.";

            _logger.LogInformation("Deleted LocalQueue {Ns}/{Name}", ns, name);
        }
        catch (k8s.Autorest.HttpOperationException ex) when
            (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            result.Success = false;
            result.Message = $"LocalQueue '{name}' not found in namespace '{ns}'.";
            result.Error = "NotFound";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete LocalQueue {Ns}/{Name}", ns, name);
            result.Success = false;
            result.Message = $"Failed to delete queue: {ex.Message}";
            result.Error = ex.Message;
        }

        return result;
    }
}
