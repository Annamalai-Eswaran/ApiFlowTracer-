namespace FlowTracer.Core.Models;

public sealed class DatabaseTrace
{
    public string SqlQuery { get; init; } = string.Empty;
    public Dictionary<string, object?> Parameters { get; init; } = new();
    public QueryKind Kind { get; init; }
    public int? RowsAffected { get; set; }
    public string DatabaseName { get; init; } = string.Empty;
}

public enum QueryKind
{
    Select,
    Insert,
    Update,
    Delete,
    Other
}