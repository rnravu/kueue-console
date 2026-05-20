using Microsoft.AspNetCore.Mvc;
using KueueConsole.Web.Services;

namespace KueueConsole.Web.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly DashboardAggregatorService _aggregator;

    public DashboardController(DashboardAggregatorService aggregator)
    {
        _aggregator = aggregator;
    }

    [HttpGet]
    public IActionResult Get() => Ok(_aggregator.GetSummary());
}
