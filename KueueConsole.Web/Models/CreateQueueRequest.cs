using System.ComponentModel.DataAnnotations;

namespace KueueConsole.Web.Models;

public class CreateQueueRequest
{
    [Required(ErrorMessage = "Queue name is required")]
    [MinLength(1)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Namespace is required")]
    [MinLength(1)]
    public string Namespace { get; set; } = "";

    [Required(ErrorMessage = "Cluster queue name is required")]
    [MinLength(1)]
    public string ClusterQueue { get; set; } = "";
}
