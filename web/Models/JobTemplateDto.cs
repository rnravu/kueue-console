namespace KueueConsole.Web.Models;

public class JobTemplateDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Image { get; set; } = "";
    public string Command { get; set; } = "";
    public string CpuRequest { get; set; } = "";
    public string MemoryRequest { get; set; } = "";
}
