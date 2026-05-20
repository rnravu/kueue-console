using k8s;
using System.Text.Json;
using KueueConsole.Web.Models;

namespace KueueConsole.Web.Services;

/// <summary>
/// Fetches and applies Kubernetes resource definitions (as formatted JSON) for Jobs,
/// LocalQueues, and ClusterQueues.  JSON is returned because it is a valid YAML subset
/// and the Kubernetes API natively speaks JSON.
/// </summary>
public class YamlService
{
    private readonly IKubernetes _client;
    private readonly ILogger<YamlService> _logger;

    private static readonly JsonSerializerOptions _prettyOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public YamlService(IKubernetes client, ILogger<YamlService> logger)
    {
        _client = client;
        _logger = logger;
    }

    // ── Jobs ─────────────────────────────────────────────────────────────────

    public async Task<YamlResourceResult> GetJobYamlAsync(string ns, string name)
    {
        try
        {
            var job = await _client.BatchV1.ReadNamespacedJobAsync(name, ns);
            return new YamlResourceResult { Success = true, Yaml = Serialize(job) };
        }
        catch (k8s.Autorest.HttpOperationException ex) when
            (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new YamlResourceResult { Success = false, Error = $"Job '{ns}/{name}' not found." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetJobYaml {Ns}/{Name}", ns, name);
            return new YamlResourceResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<YamlUpdateResult> PutJobYamlAsync(string ns, string name, string json)
    {
        try
        {
            var obj = JsonSerializer.Deserialize<k8s.Models.V1Job>(json)
                ?? throw new ArgumentException("Could not deserialize Job from provided JSON.");
            await _client.BatchV1.ReplaceNamespacedJobAsync(obj, name, ns);
            return new YamlUpdateResult { Success = true, Message = $"Job '{ns}/{name}' updated." };
        }
        catch (JsonException jex)
        {
            return new YamlUpdateResult { Success = false, Error = $"Invalid JSON: {jex.Message}" };
        }
        catch (k8s.Autorest.HttpOperationException ex)
        {
            return new YamlUpdateResult { Success = false, Error = $"Kubernetes API error: {ex.Response.Content}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PutJobYaml {Ns}/{Name}", ns, name);
            return new YamlUpdateResult { Success = false, Error = ex.Message };
        }
    }

    // ── LocalQueues ───────────────────────────────────────────────────────────

    public async Task<YamlResourceResult> GetLocalQueueYamlAsync(string ns, string name)
    {
        try
        {
            var raw = await _client.CustomObjects.GetNamespacedCustomObjectAsync(
                "kueue.x-k8s.io", "v1beta1", ns, "localqueues", name);
            return new YamlResourceResult { Success = true, Yaml = Serialize(raw) };
        }
        catch (k8s.Autorest.HttpOperationException ex) when
            (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new YamlResourceResult { Success = false, Error = $"LocalQueue '{ns}/{name}' not found." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetLocalQueueYaml {Ns}/{Name}", ns, name);
            return new YamlResourceResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<YamlUpdateResult> PutLocalQueueYamlAsync(string ns, string name, string json)
    {
        try
        {
            using var check = JsonDocument.Parse(json);  // validate JSON first
            var obj = JsonSerializer.Deserialize<object>(json)!;
            await _client.CustomObjects.ReplaceNamespacedCustomObjectAsync(
                obj, "kueue.x-k8s.io", "v1beta1", ns, "localqueues", name);
            return new YamlUpdateResult { Success = true, Message = $"LocalQueue '{ns}/{name}' updated." };
        }
        catch (JsonException jex)
        {
            return new YamlUpdateResult { Success = false, Error = $"Invalid JSON: {jex.Message}" };
        }
        catch (k8s.Autorest.HttpOperationException ex)
        {
            return new YamlUpdateResult { Success = false, Error = $"Kubernetes API error: {ex.Response.Content}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PutLocalQueueYaml {Ns}/{Name}", ns, name);
            return new YamlUpdateResult { Success = false, Error = ex.Message };
        }
    }

    // ── ClusterQueues (read-only) ─────────────────────────────────────────────

    public async Task<YamlResourceResult> GetClusterQueueYamlAsync(string name)
    {
        try
        {
            var raw = await _client.CustomObjects.GetClusterCustomObjectAsync(
                "kueue.x-k8s.io", "v1beta1", "clusterqueues", name);
            return new YamlResourceResult { Success = true, Yaml = Serialize(raw) };
        }
        catch (k8s.Autorest.HttpOperationException ex) when
            (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new YamlResourceResult { Success = false, Error = $"ClusterQueue '{name}' not found." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetClusterQueueYaml {Name}", name);
            return new YamlResourceResult { Success = false, Error = ex.Message };
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string Serialize(object obj) =>
        JsonSerializer.Serialize(obj, _prettyOptions);
}
