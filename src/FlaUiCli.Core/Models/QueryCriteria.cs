namespace FlaUiCli.Core.Models;

public class QueryCriteria
{
    public string? AutomationId { get; set; }
    public string? Name { get; set; }
    public string? ControlType { get; set; }
    public string? ClassName { get; set; }
    public string? ParentId { get; set; }
    public bool FirstOnly { get; set; }
}
