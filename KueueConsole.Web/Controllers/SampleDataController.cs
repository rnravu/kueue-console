using Microsoft.AspNetCore.Mvc;
using KueueConsole.Web.Services;

namespace KueueConsole.Web.Controllers;

[ApiController]
[Route("api/sample-data")]
public class SampleDataController : ControllerBase
{
    private readonly SampleDataService _sampleData;

    public SampleDataController(SampleDataService sampleData) => _sampleData = sampleData;

    /// <summary>
    /// POST /api/sample-data/setup
    /// Creates demo namespace, cluster queue, local queues, and optional sample jobs.
    /// All resources are labeled kueue-console.io/managed-by=sample-data for safe cleanup.
    /// </summary>
    [HttpPost("setup")]
    public async Task<IActionResult> Setup([FromBody] SampleSetupOptions? options = null)
    {
        var createJobs = options?.CreateSampleJobs ?? true;
        var result = await _sampleData.SetupAsync(createJobs);
        return result.Success ? Ok(result) : StatusCode(207, result); // 207 Multi-Status on partial
    }

    /// <summary>
    /// POST /api/sample-data/cleanup
    /// Deletes ONLY resources labeled kueue-console.io/managed-by=sample-data.
    /// Does NOT delete the namespace unless deleteNamespace=true.
    /// </summary>
    [HttpPost("cleanup")]
    public async Task<IActionResult> Cleanup([FromBody] SampleCleanupOptions? options = null)
    {
        var deleteNs = options?.DeleteNamespace ?? false;
        var result = await _sampleData.CleanupAsync(deleteNs);
        return result.Success ? Ok(result) : StatusCode(207, result);
    }
}

public class SampleSetupOptions
{
    public bool CreateSampleJobs { get; set; } = true;
}

public class SampleCleanupOptions
{
    public bool DeleteNamespace { get; set; } = false;
}
