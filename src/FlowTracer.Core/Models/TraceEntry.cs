namespace FlowTracer.Core.Models;

public sealed class TraceEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string CorrelationId { get; init; } = string.Empty;
    public string ParentId { get; init; } = string.Empty;
    public int SequenceNumber { get; set; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public TraceKind Kind { get; init; }
    public CodeLocation Location { get; init; } = new();
    public HttpTrace? Http { get; init; }
    public DatabaseTrace? Database { get; init; }
    public long DurationMs { get; set; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}

public enum TraceKind
{
    HttpRequest,
    DatabaseQuery,
    MvcAction
}