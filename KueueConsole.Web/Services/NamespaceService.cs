using k8s;
using k8s.Models;
using System.Text.Json;
using KueueConsole.Web.Models;

namespace KueueConsole.Web.Services;

public class NamespaceService
{
    private readonly IKubernetes _client;
    private readonly ILogger<NamespaceService> _logger;

    public NamespaceService(IKubernetes client, ILogger<NamespaceService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<List<NamespaceDto>> GetNamespacesAsync()
    {
        try
        {
            var list = await _client.CoreV1.ListNamespaceAsync();
            var result = new List<NamespaceDto>();

            foreach (var ns in list.Items)
            {
                var name = ns.Metadata?.Name ?? "";
                var phase = ns.Status?.Phase ?? "";
                var age = ns.Metadata?.CreationTimestamp.HasValue == true
                    ? KubeHelpers.ToAge(ns.Metadata.CreationTimestamp.Value)
                    : "";

                result.Add(new NamespaceDto { Name = name, Status = phase, Age = age });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list namespaces");
            return new List<NamespaceDto>();
        }
    }
}
