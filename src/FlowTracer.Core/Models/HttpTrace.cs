namespace FlowTracer.Core.Models;

public sealed class HttpTrace
{
    public string Method { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public Dictionary<string, string> RequestHeaders { get; init; } = new();
    public string RequestBody { get; init; } = string.Empty;
    public Dictionary<string, string> QueryParams { get; init; } = new();
    public int StatusCode { get; set; }
    public Dictionary<string, string> ResponseHeaders { get; init; } = new();
    public string ResponseBody { get; set; } = string.Empty;
}