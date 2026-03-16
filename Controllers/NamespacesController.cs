using Microsoft.AspNetCore.Mvc;
using KueueConsole.Web.Services;

namespace KueueConsole.Web.Controllers;

[ApiController]
[Route("api/namespaces")]
public class NamespacesController : ControllerBase
{
    private readonly NamespaceService _namespaces;

    public NamespacesController(NamespaceService namespaces)
    {
        _namespaces = namespaces;
    }

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _namespaces.GetNamespacesAsync());
}
