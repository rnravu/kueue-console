using KueueConsole.Web.Models;

namespace KueueConsole.Web.Services;

/// <summary>
/// Returns hard-coded job templates. No Kubernetes calls required.
/// Templates are predefined examples users can pick from when creating a job.
/// </summary>
public class JobTemplateService
{
    private static readonly List<JobTemplateDto> _templates = new()
    {
        new JobTemplateDto
        {
            Id = "job1",
            Name = "Quick Job",
            Description = "Short sleep (2 min). Good for testing queue admission quickly.",
            Image = "busybox",
            Command = "sleep 120",
            CpuRequest = "50m",
            MemoryRequest = "64Mi"
        },
        new JobTemplateDto
        {
            Id = "job2",
            Name = "Medium Job",
            Description = "Medium sleep (3 min). Good for watching a job run in the dashboard.",
            Image = "busybox",
            Command = "sleep 180",
            CpuRequest = "100m",
            MemoryRequest = "128Mi"
        },
        new JobTemplateDto
        {
            Id = "job3",
            Name = "Long Job",
            Description = "Long sleep (5 min). Good for testing queue pending and preemption.",
            Image = "busybox",
            Command = "sleep 300",
            CpuRequest = "200m",
            MemoryRequest = "256Mi"
        }
    };

    public IReadOnlyList<JobTemplateDto> GetAll() => _templates;

    public JobTemplateDto? GetById(string id) =>
        _templates.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
