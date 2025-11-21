namespace FlowTracer.Core;

public sealed class TracerOptions
{
    public int DashboardPort { get; set; } = 5050;
    public bool TrackHttp { get; set; } = true;
    public bool TrackDatabase { get; set; } = true;
    public bool TrackMvcActions { get; set; } = true;
    public int BufferSize { get; set; } = 500;
    public bool OpenBrowserOnStart { get; set; } = true;
    public bool DevelopmentOnly { get; set; } = true;
    public string[] IgnoreUrls { get; set; } = Array.Empty<string>();
    public bool CapturePayloads { get; set; } = true;
    public int MaxPayloadKb { get; set; } = 512;
    public bool CaptureStackTraces { get; set; } = true;
}