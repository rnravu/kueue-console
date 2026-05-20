namespace KueueConsole.Web.Models;

public class OperationResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public List<string> Steps { get; set; } = new();
}
