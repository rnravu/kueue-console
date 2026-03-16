using Microsoft.AspNetCore.Mvc;
using KueueConsole.Web.Models;
using KueueConsole.Web.Services;

namespace KueueConsole.Web.Controllers;

[ApiController]
[Route("api/jobs")]
public class JobsController : ControllerBase
{
    private readonly WorkloadStateService _workloads;
    private readonly JobProvisionService _provision;
    private readonly JobDiagnosticsService _diagnostics;
    private readonly JobDeleteService _delete;
    private readonly YamlService _yaml;

    public JobsController(
        WorkloadStateService workloads,
        JobProvisionService provision,
        JobDiagnosticsService diagnostics,
        JobDeleteService delete,
        YamlService yaml)
    {
        _workloads = workloads;
        _provision = provision;
        _diagnostics = diagnostics;
        _delete = delete;
        _yaml = yaml;
    }

    /// <summary>
    /// GET /api/jobs?namespace=my-ns&amp;status=Running
    /// status values: Running | Pending | Failed | Completed
    /// </summary>
    [HttpGet]
    public IActionResult Get(
        [FromQuery] string? @namespace = null,
        [FromQuery] string? status = null)
    {
        var all = string.IsNullOrWhiteSpace(@namespace)
            ? _workloads.GetAll()
            : _workloads.GetByNamespace(@namespace);

        if (!string.IsNullOrWhiteSpace(status))
            all = all.Where(w => w.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();

        return Ok(all);
    }

    /// <summary>POST /api/jobs — creates a new Kubernetes Job with the Kueue queue label.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateJob([FromBody] CreateJobRequest req)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _provision.CreateJobAsync(req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>GET /api/jobs/{namespace}/{name} — diagnostics for a single workload.</summary>
    [HttpGet("{namespace}/{name}")]
    public IActionResult GetDiagnostics(string @namespace, string name)
    {
        var result = _diagnostics.BuildDiagnostics(@namespace, name);
        if (result is null)
            return NotFound(new { message = $"Workload '{@namespace}/{name}' not found." });
        return Ok(result);
    }

    /// <summary>GET /api/jobs/{namespace}/{name}/diagnostics — alias for GetDiagnostics.</summary>
    [HttpGet("{namespace}/{name}/diagnostics")]
    public IActionResult GetDiagnosticsAlias(string @namespace, string name) =>
        GetDiagnostics(@namespace, name);

    /// <summary>GET /api/jobs/{namespace}/{name}/yaml — resource definition as formatted JSON.</summary>
    [HttpGet("{namespace}/{name}/yaml")]
    public async Task<IActionResult> GetYaml(string @namespace, string name)
    {
        var result = await _yaml.GetJobYamlAsync(@namespace, name);
        if (!result.Success)
            return NotFound(new { message = result.Error });
        return Ok(result);
    }

    /// <summary>PUT /api/jobs/{namespace}/{name}/yaml — apply updated resource definition.</summary>
    [HttpPut("{namespace}/{name}/yaml")]
    public async Task<IActionResult> PutYaml(
        string @namespace, string name, [FromBody] YamlUpdateRequest req)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _yaml.PutJobYamlAsync(@namespace, name, req.Yaml);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>DELETE /api/jobs/{namespace}/{name} — deletes the job and optionally its workload.</summary>
    [HttpDelete("{namespace}/{name}")]
    public async Task<IActionResult> DeleteJob(
        string @namespace, string name, [FromBody] DeleteJobRequest? req = null)
    {
        var deleteWorkload = req?.DeleteWorkload ?? true;
        var result = await _delete.DeleteJobAsync(@namespace, name, deleteWorkload);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
