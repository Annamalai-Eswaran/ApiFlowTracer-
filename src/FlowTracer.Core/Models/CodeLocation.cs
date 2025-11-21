namespace FlowTracer.Core.Models;

public sealed class CodeLocation
{
    public string FilePath { get; init; } = string.Empty;
    public int LineNumber { get; init; }
    public string MethodName { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public string StackTrace { get; init; } = string.Empty;
}