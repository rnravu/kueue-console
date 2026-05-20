using System.ComponentModel.DataAnnotations;

namespace KueueConsole.Web.Models;

public class CreateJobRequest
{
    [Required(ErrorMessage = "Job name is required")]
    [MinLength(1)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Namespace is required")]
    [MinLength(1)]
    public string Namespace { get; set; } = "";

    [Required(ErrorMessage = "Queue name is required")]
    [MinLength(1)]
    public string QueueName { get; set; } = "";

    public string Image { get; set; } = "busybox";

    public string Command { get; set; } = "sleep 120";

    public string CpuRequest { get; set; } = "100m";

    public string MemoryRequest { get; set; } = "128Mi";
}
