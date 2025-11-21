using System.Threading;

namespace FlowTracer.Core.Services;

public sealed class CorrelationTracker
{
    private static readonly AsyncLocal<string> _correlationId = new();

    public string GetOrCreate()
    {
        if (string.IsNullOrEmpty(_correlationId.Value))
        {
            _correlationId.Value = Guid.NewGuid().ToString();
        }
        return _correlationId.Value;
    }

    public void Set(string correlationId)
    {
        _correlationId.Value = correlationId;
    }

    public string Get()
    {
        return _correlationId.Value ?? string.Empty;
    }

    public void Clear()
    {
        _correlationId.Value = null!;
    }
}