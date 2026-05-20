using k8s;
using k8s.Models;
using KueueConsole.Web.Models;

namespace KueueConsole.Web.Services;

/// <summary>
/// Creates a Kubernetes Job and sets the Kueue queue-name label so Kueue admits it.
/// </summary>
public class JobProvisionService
{
    private readonly IKubernetes _client;
    private readonly ILogger<JobProvisionService> _logger;

    public JobProvisionService(IKubernetes client, ILogger<JobProvisionService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<OperationResultDto> CreateJobAsync(CreateJobRequest req)
    {
        var result = new OperationResultDto();

        var name = req.Name.Trim().ToLowerInvariant();
        var ns = req.Namespace.Trim().ToLowerInvariant();
        var queue = req.QueueName.Trim();
        var image = string.IsNullOrWhiteSpace(req.Image) ? "busybox" : req.Image.Trim();
        var command = string.IsNullOrWhiteSpace(req.Command) ? "sleep 120" : req.Command.Trim();
        var cpu = string.IsNullOrWhiteSpace(req.CpuRequest) ? "100m" : req.CpuRequest.Trim();
        var memory = string.IsNullOrWhiteSpace(req.MemoryRequest) ? "128Mi" : req.MemoryRequest.Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(ns) || string.IsNullOrWhiteSpace(queue))
        {
            result.Success = false;
            result.Message = "Name, namespace, and queue name are all required.";
            return result;
        }

        // Split command into argv so the container runs it directly (no shell injection)
        var argv = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var job = BuildJob(name, ns, queue, image, argv, cpu, memory, extraLabels: null);

        try
        {
            await _client.BatchV1.CreateNamespacedJobAsync(job, ns);

            result.Success = true;
            result.Message = $"Job '{name}' submitted to queue '{queue}' in namespace '{ns}'.";
            result.Steps.Add($"✓ Created Job '{name}' → Queue '{queue}'");
            result.Steps.Add($"  Image: {image}  Command: {command}");
            result.Steps.Add($"  Resources: cpu={cpu}  memory={memory}");
            _logger.LogInformation("Created Job {Name} in {Namespace} queue {Queue}", name, ns, queue);
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            result.Success = false;
            result.Message = $"Job '{name}' already exists in namespace '{ns}'.";
            result.Steps.Add($"– Job '{name}' already exists");
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            result.Success = false;
            result.Message = $"Namespace '{ns}' not found.";
            result.Steps.Add($"✗ Namespace not found: {ex.Message}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Failed to create Job: {ex.Message}";
            result.Steps.Add($"✗ Error: {ex.Message}");
            _logger.LogError(ex, "Failed to create Job {Name}", name);
        }

        return result;
    }

    internal static V1Job BuildJob(
        string name, string ns, string queueName,
        string image, string[] argv,
        string cpu, string memory,
        Dictionary<string, string>? extraLabels)
    {
        var labels = new Dictionary<string, string>(extraLabels ?? new())
        {
            // Kueue admission label — MUST be present for Kueue to manage this job
            ["kueue.x-k8s.io/queue-name"] = queueName
        };

        return new V1Job
        {
            ApiVersion = "batch/v1",
            Kind = "Job",
            Metadata = new V1ObjectMeta
            {
                Name = name,
                NamespaceProperty = ns,
                Labels = labels
            },
            Spec = new V1JobSpec
            {
                // Kueue requires Suspend=true on job creation so it can manage admission
                Suspend = true,
                Template = new V1PodTemplateSpec
                {
                    Spec = new V1PodSpec
                    {
                        RestartPolicy = "Never",
                        Containers = new List<V1Container>
                        {
                            new V1Container
                            {
                                Name = name,
                                Image = image,
                                Command = argv,
                                Resources = new V1ResourceRequirements
                                {
                                    Requests = new Dictionary<string, ResourceQuantity>
                                    {
                                        ["cpu"]    = new ResourceQuantity(cpu),
                                        ["memory"] = new ResourceQuantity(memory)
                                    },
                                    Limits = new Dictionary<string, ResourceQuantity>
                                    {
                                        ["cpu"]    = new ResourceQuantity(cpu),
                                        ["memory"] = new ResourceQuantity(memory)
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }
}
