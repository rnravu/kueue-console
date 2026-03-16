using k8s;
using k8s.Models;
using System.Text.Json;
using KueueConsole.Web.Models;

namespace KueueConsole.Web.Services;

/// <summary>
/// Creates and cleans up demo/sample Kubernetes resources.
///
/// Safety: cleanup ONLY deletes resources that carry the label
///   kueue-console.io/managed-by: sample-data
/// and are in the demo namespace or are the specifically named sample ClusterQueue/Flavor.
/// Resources created by users are never touched.
/// </summary>
public class SampleDataService
{
    // ── Constants ────────────────────────────────────────────────────────────────
    public const string DemoNamespace     = "demo";
    public const string SampleLabelKey    = "kueue-console.io/managed-by";
    public const string SampleLabelValue  = "sample-data";
    public const string ClusterQueueName  = "cluster-queue";
    public const string ResourceFlavorName = "default-flavor";

    private static readonly string[] LocalQueueNames  = { "user-queue", "test-queue", "batch-queue" };

    // ── Sample jobs seeded during Setup ─────────────────────────────────────────
    private static readonly (string name, string cpu, string memory, string command)[] SampleJobs =
    {
        ("sample-job1", "50m",  "64Mi",  "sleep 120"),
        ("sample-job2", "100m", "128Mi", "sleep 180"),
        ("sample-job3", "200m", "256Mi", "sleep 300"),
    };

    private static readonly Dictionary<string, string> SampleLabels = new()
    {
        [SampleLabelKey] = SampleLabelValue
    };

    private readonly IKubernetes _client;
    private readonly ILogger<SampleDataService> _logger;

    public SampleDataService(IKubernetes client, ILogger<SampleDataService> logger)
    {
        _client = client;
        _logger = logger;
    }

    // ── Setup ────────────────────────────────────────────────────────────────────

    public async Task<OperationResultDto> SetupAsync(bool createSampleJobs = true)
    {
        var result = new OperationResultDto { Success = true };

        await EnsureNamespaceAsync(result);
        await EnsureResourceFlavorAsync(result);
        await EnsureClusterQueueAsync(result);
        foreach (var lq in LocalQueueNames)
            await EnsureLocalQueueAsync(lq, result);

        if (createSampleJobs)
        {
            foreach (var (name, cpu, memory, command) in SampleJobs)
                await EnsureSampleJobAsync(name, cpu, memory, command, result);
        }

        result.Message = result.Success
            ? "Sample data setup complete."
            : "Setup completed with some errors — see steps above.";
        return result;
    }

    private async Task EnsureNamespaceAsync(OperationResultDto result)
    {
        try
        {
            var ns = new V1Namespace
            {
                Metadata = new V1ObjectMeta
                {
                    Name = DemoNamespace,
                    Labels = new Dictionary<string, string>(SampleLabels)
                }
            };
            await _client.CoreV1.CreateNamespaceAsync(ns);
            result.Steps.Add($"✓ Created namespace '{DemoNamespace}'");
        }
        catch (k8s.Autorest.HttpOperationException ex) when (IsConflict(ex))
        {
            result.Steps.Add($"– Namespace '{DemoNamespace}' already exists");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Steps.Add($"✗ Failed to create namespace: {ex.Message}");
            _logger.LogError(ex, "Failed to create namespace {Ns}", DemoNamespace);
        }
    }

    private async Task EnsureResourceFlavorAsync(OperationResultDto result)
    {
        try
        {
            var flavor = new
            {
                apiVersion = "kueue.x-k8s.io/v1beta1",
                kind = "ResourceFlavor",
                metadata = new { name = ResourceFlavorName, labels = SampleLabels }
            };
            await _client.CustomObjects.CreateClusterCustomObjectAsync(
                flavor, "kueue.x-k8s.io", "v1beta1", "resourceflavors");
            result.Steps.Add($"✓ Created ResourceFlavor '{ResourceFlavorName}'");
        }
        catch (k8s.Autorest.HttpOperationException ex) when (IsConflict(ex))
        {
            result.Steps.Add($"– ResourceFlavor '{ResourceFlavorName}' already exists");
        }
        catch (Exception ex)
        {
            // ResourceFlavor may already exist from a prior install — non-fatal
            result.Steps.Add($"– ResourceFlavor skipped: {ex.Message}");
            _logger.LogWarning(ex, "Could not create ResourceFlavor {Name}", ResourceFlavorName);
        }
    }

    private async Task EnsureClusterQueueAsync(OperationResultDto result)
    {
        try
        {
            var cq = new
            {
                apiVersion = "kueue.x-k8s.io/v1beta1",
                kind = "ClusterQueue",
                metadata = new { name = ClusterQueueName, labels = SampleLabels },
                spec = new
                {
                    namespaceSelector = new { },   // empty = admit all namespaces
                    resourceGroups = new[]
                    {
                        new
                        {
                            coveredResources = new[] { "cpu", "memory" },
                            flavors = new[]
                            {
                                new
                                {
                                    name = ResourceFlavorName,
                                    resources = new[]
                                    {
                                        new { name = "cpu",    nominalQuota = "2" },
                                        new { name = "memory", nominalQuota = "4Gi" }
                                    }
                                }
                            }
                        }
                    }
                }
            };
            await _client.CustomObjects.CreateClusterCustomObjectAsync(
                cq, "kueue.x-k8s.io", "v1beta1", "clusterqueues");
            result.Steps.Add($"✓ Created ClusterQueue '{ClusterQueueName}'");
        }
        catch (k8s.Autorest.HttpOperationException ex) when (IsConflict(ex))
        {
            result.Steps.Add($"– ClusterQueue '{ClusterQueueName}' already exists");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Steps.Add($"✗ Failed to create ClusterQueue: {ex.Message}");
            _logger.LogError(ex, "Failed to create ClusterQueue {Name}", ClusterQueueName);
        }
    }

    private async Task EnsureLocalQueueAsync(string name, OperationResultDto result)
    {
        try
        {
            var lq = LocalQueueProvisionService.BuildManifest(
                name, DemoNamespace, ClusterQueueName, SampleLabels);
            await _client.CustomObjects.CreateNamespacedCustomObjectAsync(
                lq, "kueue.x-k8s.io", "v1beta1", DemoNamespace, "localqueues");
            result.Steps.Add($"✓ Created LocalQueue '{name}'");
        }
        catch (k8s.Autorest.HttpOperationException ex) when (IsConflict(ex))
        {
            result.Steps.Add($"– LocalQueue '{name}' already exists");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Steps.Add($"✗ Failed to create LocalQueue '{name}': {ex.Message}");
            _logger.LogError(ex, "Failed to create LocalQueue {Name}", name);
        }
    }

    private async Task EnsureSampleJobAsync(string name, string cpu, string memory, string command, OperationResultDto result)
    {
        try
        {
            var argv = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var jobLabels = new Dictionary<string, string>(SampleLabels);
            var job = JobProvisionService.BuildJob(
                name, DemoNamespace, LocalQueueNames[0], "busybox", argv, cpu, memory, jobLabels);
            await _client.BatchV1.CreateNamespacedJobAsync(job, DemoNamespace);
            result.Steps.Add($"✓ Created sample Job '{name}' ({command}, cpu={cpu}, mem={memory})");
        }
        catch (k8s.Autorest.HttpOperationException ex) when (IsConflict(ex))
        {
            result.Steps.Add($"– Sample Job '{name}' already exists");
        }
        catch (Exception ex)
        {
            // Sample jobs failing is non-fatal
            result.Steps.Add($"– Sample Job '{name}' skipped: {ex.Message}");
            _logger.LogWarning(ex, "Could not create sample job {Name}", name);
        }
    }

    // ── Cleanup ──────────────────────────────────────────────────────────────────

    public async Task<OperationResultDto> CleanupAsync(bool deleteNamespace = false)
    {
        var result = new OperationResultDto { Success = true };

        // 1. Delete sample Jobs first (they reference queues)
        await DeleteSampleJobsAsync(result);

        // 2. Delete LocalQueues (they reference ClusterQueue)
        await DeleteSampleLocalQueuesAsync(result);

        // 3. Delete ClusterQueue
        await DeleteSampleClusterQueueAsync(result);

        // 4. Delete ResourceFlavor
        await DeleteSampleResourceFlavorAsync(result);

        // 5. Optionally delete the namespace
        if (deleteNamespace)
            await DeleteNamespaceAsync(result);

        result.Message = result.Success
            ? "Sample data cleanup complete."
            : "Cleanup completed with some errors — see steps above.";
        return result;
    }

    private async Task DeleteSampleJobsAsync(OperationResultDto result)
    {
        try
        {
            var labelSelector = $"{SampleLabelKey}={SampleLabelValue}";
            var jobs = await _client.BatchV1.ListNamespacedJobAsync(
                DemoNamespace, labelSelector: labelSelector);

            if (jobs.Items.Count == 0)
            {
                result.Steps.Add("– No sample Jobs found to delete");
                return;
            }

            foreach (var job in jobs.Items)
            {
                var jobName = job.Metadata.Name;
                await _client.BatchV1.DeleteNamespacedJobAsync(jobName, DemoNamespace,
                    new V1DeleteOptions { PropagationPolicy = "Foreground" });
                result.Steps.Add($"✓ Deleted Job '{jobName}'");
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Steps.Add($"✗ Failed to delete sample Jobs: {ex.Message}");
            _logger.LogError(ex, "Failed to delete sample jobs");
        }
    }

    private async Task DeleteSampleLocalQueuesAsync(OperationResultDto result)
    {
        try
        {
            var labelSelector = $"{SampleLabelKey}={SampleLabelValue}";
            var lqs = await _client.CustomObjects.ListNamespacedCustomObjectAsync(
                "kueue.x-k8s.io", "v1beta1", DemoNamespace, "localqueues",
                labelSelector: labelSelector);

            var json = JsonSerializer.Serialize(lqs);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
            {
                result.Steps.Add("– No sample LocalQueues found to delete");
                return;
            }

            foreach (var lq in items.EnumerateArray())
            {
                var lqName = KubeHelpers.GetString(lq.GetProperty("metadata"), "name");
                await _client.CustomObjects.DeleteNamespacedCustomObjectAsync(
                    "kueue.x-k8s.io", "v1beta1", DemoNamespace, "localqueues", lqName);
                result.Steps.Add($"✓ Deleted LocalQueue '{lqName}'");
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Steps.Add($"✗ Failed to delete sample LocalQueues: {ex.Message}");
            _logger.LogError(ex, "Failed to delete sample LocalQueues");
        }
    }

    private async Task DeleteSampleClusterQueueAsync(OperationResultDto result)
    {
        try
        {
            // Verify it has the sample label before deleting
            var cq = await _client.CustomObjects.GetClusterCustomObjectAsync(
                "kueue.x-k8s.io", "v1beta1", "clusterqueues", ClusterQueueName);

            var json = JsonSerializer.Serialize(cq);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!HasSampleLabel(root))
            {
                result.Steps.Add($"– ClusterQueue '{ClusterQueueName}' was not created by sample data — skipped");
                return;
            }

            await _client.CustomObjects.DeleteClusterCustomObjectAsync(
                "kueue.x-k8s.io", "v1beta1", "clusterqueues", ClusterQueueName);
            result.Steps.Add($"✓ Deleted ClusterQueue '{ClusterQueueName}'");
        }
        catch (k8s.Autorest.HttpOperationException ex) when (IsNotFound(ex))
        {
            result.Steps.Add($"– ClusterQueue '{ClusterQueueName}' not found");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Steps.Add($"✗ Failed to delete ClusterQueue: {ex.Message}");
            _logger.LogError(ex, "Failed to delete ClusterQueue {Name}", ClusterQueueName);
        }
    }

    private async Task DeleteSampleResourceFlavorAsync(OperationResultDto result)
    {
        try
        {
            var rf = await _client.CustomObjects.GetClusterCustomObjectAsync(
                "kueue.x-k8s.io", "v1beta1", "resourceflavors", ResourceFlavorName);

            var json = JsonSerializer.Serialize(rf);
            using var doc = JsonDocument.Parse(json);

            if (!HasSampleLabel(doc.RootElement))
            {
                result.Steps.Add($"– ResourceFlavor '{ResourceFlavorName}' was not created by sample data — skipped");
                return;
            }

            await _client.CustomObjects.DeleteClusterCustomObjectAsync(
                "kueue.x-k8s.io", "v1beta1", "resourceflavors", ResourceFlavorName);
            result.Steps.Add($"✓ Deleted ResourceFlavor '{ResourceFlavorName}'");
        }
        catch (k8s.Autorest.HttpOperationException ex) when (IsNotFound(ex))
        {
            result.Steps.Add($"– ResourceFlavor '{ResourceFlavorName}' not found");
        }
        catch (Exception ex)
        {
            // Non-fatal: flavor might be shared
            result.Steps.Add($"– ResourceFlavor not deleted: {ex.Message}");
            _logger.LogWarning(ex, "Could not delete ResourceFlavor {Name}", ResourceFlavorName);
        }
    }

    private async Task DeleteNamespaceAsync(OperationResultDto result)
    {
        try
        {
            var ns = await _client.CoreV1.ReadNamespaceAsync(DemoNamespace);
            var nsLabels = ns.Metadata?.Labels;

            if (nsLabels == null || !nsLabels.TryGetValue(SampleLabelKey, out var v) || v != SampleLabelValue)
            {
                result.Steps.Add($"– Namespace '{DemoNamespace}' was not created by sample data — skipped");
                return;
            }

            await _client.CoreV1.DeleteNamespaceAsync(DemoNamespace);
            result.Steps.Add($"✓ Deleted namespace '{DemoNamespace}'");
        }
        catch (k8s.Autorest.HttpOperationException ex) when (IsNotFound(ex))
        {
            result.Steps.Add($"– Namespace '{DemoNamespace}' not found");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Steps.Add($"✗ Failed to delete namespace: {ex.Message}");
            _logger.LogError(ex, "Failed to delete namespace {Ns}", DemoNamespace);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static bool HasSampleLabel(JsonElement element)
    {
        if (!element.TryGetProperty("metadata", out var meta)) return false;
        if (!meta.TryGetProperty("labels", out var labels)) return false;
        if (!labels.TryGetProperty(SampleLabelKey, out var val)) return false;
        return val.GetString() == SampleLabelValue;
    }

    private static bool IsConflict(k8s.Autorest.HttpOperationException ex) =>
        ex.Response.StatusCode == System.Net.HttpStatusCode.Conflict;

    private static bool IsNotFound(k8s.Autorest.HttpOperationException ex) =>
        ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound;
}
