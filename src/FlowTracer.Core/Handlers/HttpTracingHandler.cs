using System.Diagnostics;
using FlowTracer.Core.Models;
using FlowTracer.Core.Services;

namespace FlowTracer.Core.Handlers;

public sealed class HttpTracingHandler : DelegatingHandler
{
    private readonly TraceCollector _traceCollector;
    private readonly CorrelationTracker _correlationTracker;
    private readonly TracerOptions _settings;

    public HttpTracingHandler(TraceCollector traceCollector, CorrelationTracker correlationTracker, TracerOptions settings)
    {
        _traceCollector = traceCollector;
        _correlationTracker = correlationTracker;
        _settings = settings;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        if (!_settings.TrackHttp) 
            return await base.SendAsync(request, token);

        var requestUrl = request.RequestUri?.AbsoluteUri ?? "unknown";
        if (IsUrlIgnored(requestUrl))
            return await base.SendAsync(request, token);

        var timer = Stopwatch.StartNew();
        var httpInfo = CreateHttpTraceInfo(request);

        if (_settings.CapturePayloads && request.Content != null)
        {
            var requestBody = await ExtractContent(request.Content, token);
            httpInfo = new HttpTrace
            {
                Method = httpInfo.Method,
                Url = httpInfo.Url,
                RequestHeaders = httpInfo.RequestHeaders,
                QueryParams = httpInfo.QueryParams,
                RequestBody = requestBody
            };
        }

        var traceRecord = new TraceEntry
        {
            Kind = TraceKind.HttpRequest,
            CorrelationId = _correlationTracker.GetOrCreate(),
            Location = BuildCodeLocation(),
            Http = httpInfo
        };

        HttpResponseMessage responseMessage;
        try
        {
            responseMessage = await base.SendAsync(request, token);
            httpInfo.StatusCode = (int)responseMessage.StatusCode;

            if (_settings.CapturePayloads && responseMessage.Content != null)
            {
                httpInfo.ResponseBody = await ExtractContent(responseMessage.Content, token);
            }
        }
        catch (Exception error)
        {
            traceRecord.Metadata["Exception"] = error.Message;
            throw;
        }
        finally
        {
            timer.Stop();
            traceRecord.DurationMs = timer.ElapsedMilliseconds;
            _traceCollector.Record(traceRecord);
        }

        return responseMessage;
    }

    private bool IsUrlIgnored(string url)
    {
        foreach (var pattern in _settings.IgnoreUrls)
        {
            if (url.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private HttpTrace CreateHttpTraceInfo(HttpRequestMessage request)
    {
        var headers = new Dictionary<string, string>();
        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(", ", header.Value);
        }

        var queryData = new Dictionary<string, string>();
        if (request.RequestUri?.Query != null)
        {
            var parsedQuery = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
            foreach (string key in parsedQuery)
            {
                queryData[key] = parsedQuery[key] ?? string.Empty;
            }
        }

        return new HttpTrace
        {
            Method = request.Method.Method,
            Url = request.RequestUri?.AbsoluteUri ?? string.Empty,
            RequestHeaders = headers,
            QueryParams = queryData
        };
    }

    private async Task<string> ExtractContent(HttpContent content, CancellationToken token)
    {
        try
        {
            var text = await content.ReadAsStringAsync(token);
            var maxSize = _settings.MaxPayloadKb * 1024;
            
            if (text.Length > maxSize)
            {
                return text.Substring(0, maxSize) + " ...CONTENT_TRUNCATED";
            }
            
            return text;
        }
        catch
        {
            return "CONTENT_READ_ERROR";
        }
    }

    private CodeLocation BuildCodeLocation()
    {
        var stack = new StackTrace(true);
        var frames = stack.GetFrames();
        
        if (frames == null)
        {
            return new CodeLocation();
        }

        foreach (var frame in frames)
        {
            var method = frame.GetMethod();
            var declType = method?.DeclaringType;
            var namespaceName = declType?.Namespace ?? string.Empty;

            if (!namespaceName.StartsWith("FlowTracer") && !namespaceName.StartsWith("System"))
            {
                return new CodeLocation
                {
                    FilePath = frame.GetFileName() ?? "unknown",
                    LineNumber = frame.GetFileLineNumber(),
                    MethodName = method?.Name ?? "unknown",
                    ClassName = declType?.Name ?? "unknown",
                    StackTrace = _settings.CaptureStackTraces ? stack.ToString() : string.Empty
                };
            }
        }

        return new CodeLocation();
    }
}