using Microsoft.AspNetCore.Mvc;
using KueueConsole.Web.Services;

namespace KueueConsole.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkloadsController : ControllerBase
{
    private readonly WorkloadStateService _workloads;

    public WorkloadsController(WorkloadStateService workloads) { _workloads = workloads; }

    /// <summary>GET /api/workloads?namespace=my-ns</summary>
    [HttpGet]
    public IActionResult Get([FromQuery] string? @namespace = null)
    {
        var result = string.IsNullOrWhiteSpace(@namespace)
            ? _workloads.GetAll()
            : _workloads.GetByNamespace(@namespace);
        return Ok(result);
    }
}
