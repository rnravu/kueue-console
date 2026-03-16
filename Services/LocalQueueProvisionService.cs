using k8s;
using System.Text.Json;
using KueueConsole.Web.Models;

namespace KueueConsole.Web.Services;

/// <summary>
/// Creates a LocalQueue custom resource in the Kubernetes cluster.
/// </summary>
public class LocalQueueProvisionService
{
    private readonly IKubernetes _client;
    private readonly ILogger<LocalQueueProvisionService> _logger;

    public LocalQueueProvisionService(IKubernetes client, ILogger<LocalQueueProvisionService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<OperationResultDto> CreateLocalQueueAsync(CreateQueueRequest req)
    {
        var result = new OperationResultDto();

        // Input sanitization — names are validated by [Required] but still guard here
        var name = req.Name.Trim().ToLowerInvariant();
        var ns = req.Namespace.Trim().ToLowerInvariant();
        var cq = req.ClusterQueue.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(ns) || string.IsNullOrWhiteSpace(cq))
        {
            result.Success = false;
            result.Message = "Name, namespace, and cluster queue are all required.";
            return result;
        }

        var manifest = BuildManifest(name, ns, cq, labels: null);

        try
        {
            await _client.CustomObjects.CreateNamespacedCustomObjectAsync(
                manifest, "kueue.x-k8s.io", "v1beta1", ns, "localqueues");

            result.Success = true;
            result.Message = $"LocalQueue '{name}' created in namespace '{ns}'.";
            result.Steps.Add($"✓ Created LocalQueue '{name}' → ClusterQueue '{cq}'");
            _logger.LogInformation("Created LocalQueue {Name} in {Namespace}", name, ns);
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            result.Success = false;
            result.Message = $"LocalQueue '{name}' already exists in namespace '{ns}'.";
            result.Steps.Add($"– LocalQueue '{name}' already exists");
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            result.Success = false;
            result.Message = $"Namespace '{ns}' or ClusterQueue '{cq}' not found.";
            result.Steps.Add($"✗ Namespace or ClusterQueue not found: {ex.Message}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Failed to create LocalQueue: {ex.Message}";
            result.Steps.Add($"✗ Error: {ex.Message}");
            _logger.LogError(ex, "Failed to create LocalQueue {Name}", name);
        }

        return result;
    }

    internal static object BuildManifest(string name, string ns, string clusterQueue, Dictionary<string, string>? labels)
    {
        var allLabels = new Dictionary<string, string>(labels ?? new());

        return new
        {
            apiVersion = "kueue.x-k8s.io/v1beta1",
            kind = "LocalQueue",
            metadata = new
            {
                name,
                @namespace = ns,
                labels = allLabels
            },
            spec = new
            {
                clusterQueue
            }
        };
    }
}
