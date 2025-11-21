using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlowTracer.Core.Models;
using FlowTracer.Core.Services;

namespace FlowTracer.Core.Handlers;

public sealed class HttpTracingHandler : DelegatingHandler
{
    private readonly TraceCollector _collector;
    private readonly CorrelationTracker _correlationTracker;
    private readonly TracerOptions _options;

    public HttpTracingHandler(TraceCollector collector, CorrelationTracker correlationTracker, TracerOptions options)
    {
        _collector = collector;
        _correlationTracker = correlationTracker;
        _options = options;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!_options.TrackHttp) return await base.SendAsync(request, cancellationToken);
        
        var url = request.RequestUri?.ToString() ?? string.Empty;
        if (_options.IgnoreUrls.Any(p => url.Contains(p, StringComparison.OrdinalIgnoreCase)))
            return await base.SendAsync(request, cancellationToken);

        var sw = Stopwatch.StartNew();
        var httpTrace = new HttpTrace
        {
            Method = request.Method.ToString(),
            Url = url,
            RequestHeaders = request.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
        };

        if (_options.CapturePayloads && request.Content != null)
            httpTrace = httpTrace with { RequestBody = await SafeReadContent(request.Content, cancellationToken) };

        if (request.RequestUri?.Query != null)
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
            foreach (string key in queryParams)
                httpTrace.QueryParams[key] = queryParams[key] ?? string.Empty;
        }

        var trace = new TraceEntry
        {
            Kind = TraceKind.HttpRequest,
            CorrelationId = _correlationTracker.GetOrCreate(),
            Location = GetCodeLocation(),
            Http = httpTrace
        };

        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            httpTrace = httpTrace with { StatusCode = (int)response.StatusCode };
            if (_options.CapturePayloads && response.Content != null)
                httpTrace = httpTrace with { ResponseBody = await SafeReadContent(response.Content, cancellationToken) };
            trace = trace with { Http = httpTrace };
            return response;
        }
        catch (Exception ex)
        {
            trace.Metadata["Error"] = ex.Message;
            throw;
        }
        finally
        {
            sw.Stop();
            trace = trace with { DurationMs = sw.ElapsedMilliseconds };
            _collector.Record(trace);
        }
    }

    private async Task<string> SafeReadContent(HttpContent content, CancellationToken ct)
    {
        try
        {
            var body = await content.ReadAsStringAsync(ct);
            var max = _options.MaxPayloadKb * 1024;
            return body.Length > max ? body[..max] + "...[truncated]" : body;
        }
        catch { return "[unreadable]"; }
    }

    private CodeLocation GetCodeLocation([CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string member = "")
    {
        var st = new StackTrace(true);
        var frame = st.GetFrames()?.FirstOrDefault(f => !(f.GetMethod()?.DeclaringType?.Namespace?.StartsWith("FlowTracer") ?? false));
        return new CodeLocation
        {
            FilePath = frame?.GetFileName() ?? file,
            LineNumber = frame?.GetFileLineNumber() ?? line,
            MethodName = frame?.GetMethod()?.Name ?? member,
            ClassName = frame?.GetMethod()?.DeclaringType?.Name ?? "",
            StackTrace = _options.CaptureStackTraces ? st.ToString() : ""
        };
    }
}