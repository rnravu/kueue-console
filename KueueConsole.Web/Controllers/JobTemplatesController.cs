using Microsoft.AspNetCore.Mvc;
using KueueConsole.Web.Services;

namespace KueueConsole.Web.Controllers;

[ApiController]
[Route("api/job-templates")]
public class JobTemplatesController : ControllerBase
{
    private readonly JobTemplateService _templates;

    public JobTemplatesController(JobTemplateService templates) => _templates = templates;

    /// <summary>GET /api/job-templates — returns all static job templates.</summary>
    [HttpGet]
    public IActionResult Get() => Ok(_templates.GetAll());
}
