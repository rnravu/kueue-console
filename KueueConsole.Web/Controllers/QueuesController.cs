using Microsoft.AspNetCore.Mvc;
using KueueConsole.Web.Models;
using KueueConsole.Web.Services;

namespace KueueConsole.Web.Controllers;

[ApiController]
[Route("api/queues")]
public class QueuesController : ControllerBase
{
    private readonly ClusterQueueService _clusterQueues;
    private readonly LocalQueueStateService _localQueues;
    private readonly LocalQueueProvisionService _provision;
    private readonly QueueDeleteService _delete;
    private readonly YamlService _yaml;

    public QueuesController(
        ClusterQueueService clusterQueues,
        LocalQueueStateService localQueues,
        LocalQueueProvisionService provision,
        QueueDeleteService delete,
        YamlService yaml)
    {
        _clusterQueues = clusterQueues;
        _localQueues = localQueues;
        _provision = provision;
        _delete = delete;
        _yaml = yaml;
    }

    /// <summary>GET /api/queues/cluster</summary>
    [HttpGet("cluster")]
    public IActionResult GetClusterQueues() => Ok(_clusterQueues.GetAll());

    /// <summary>GET /api/queues/cluster/{name}/yaml — read-only resource definition.</summary>
    [HttpGet("cluster/{name}/yaml")]
    public async Task<IActionResult> GetClusterQueueYaml(string name)
    {
        var result = await _yaml.GetClusterQueueYamlAsync(name);
        if (!result.Success)
            return NotFound(new { message = result.Error });
        return Ok(result);
    }

    /// <summary>GET /api/queues/local?namespace=my-ns</summary>
    [HttpGet("local")]
    public IActionResult GetLocalQueues([FromQuery] string? @namespace = null)
    {
        var result = string.IsNullOrWhiteSpace(@namespace)
            ? _localQueues.GetAll()
            : _localQueues.GetByNamespace(@namespace);
        return Ok(result);
    }

    /// <summary>POST /api/queues — creates a new LocalQueue.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateQueue([FromBody] CreateQueueRequest req)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _provision.CreateLocalQueueAsync(req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>DELETE /api/queues/{namespace}/{name} — deletes a LocalQueue with safety checks.</summary>
    [HttpDelete("{namespace}/{name}")]
    public async Task<IActionResult> DeleteQueue(string @namespace, string name)
    {
        var result = await _delete.DeleteLocalQueueAsync(@namespace, name);
        if (result.Blocked)
            return Conflict(result);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>GET /api/queues/{namespace}/{name}/yaml — resource definition as formatted JSON.</summary>
    [HttpGet("{namespace}/{name}/yaml")]
    public async Task<IActionResult> GetLocalQueueYaml(string @namespace, string name)
    {
        var result = await _yaml.GetLocalQueueYamlAsync(@namespace, name);
        if (!result.Success)
            return NotFound(new { message = result.Error });
        return Ok(result);
    }

    /// <summary>PUT /api/queues/{namespace}/{name}/yaml — apply updated resource definition.</summary>
    [HttpPut("{namespace}/{name}/yaml")]
    public async Task<IActionResult> PutLocalQueueYaml(
        string @namespace, string name, [FromBody] YamlUpdateRequest req)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _yaml.PutLocalQueueYamlAsync(@namespace, name, req.Yaml);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

