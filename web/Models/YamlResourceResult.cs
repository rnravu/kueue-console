using System.ComponentModel.DataAnnotations;

namespace KueueConsole.Web.Models;

/// <summary>Result of GET .../yaml — returns the resource definition as formatted JSON.</summary>
public class YamlResourceResult
{
    public bool Success { get; set; }
    /// <summary>The resource definition in pretty-printed JSON format (valid YAML subset).</summary>
    public string Yaml { get; set; } = "";
    public string? Error { get; set; }
}

/// <summary>Request body for PUT .../yaml — accepts the resource definition as JSON.</summary>
public class YamlUpdateRequest
{
    [Required(ErrorMessage = "yaml is required")]
    public string Yaml { get; set; } = "";
}

/// <summary>Result of PUT .../yaml.</summary>
public class YamlUpdateResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? Error { get; set; }
}
