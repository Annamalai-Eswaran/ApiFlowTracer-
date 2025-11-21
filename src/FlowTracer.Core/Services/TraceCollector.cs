using System.Collections.Concurrent;
using FlowTracer.Core.Models;

namespace FlowTracer.Core.Services;

public sealed class TraceCollector
{
    private readonly ConcurrentQueue<TraceEntry> _traces = new();
    private readonly TracerOptions _options;
    private int _sequenceCounter;

    public event EventHandler<TraceEntry>? TraceRecorded;

    public TraceCollector(TracerOptions options)
    {
        _options = options;
    }

    public void Record(TraceEntry trace)
    {
        trace.SequenceNumber = Interlocked.Increment(ref _sequenceCounter);
        _traces.Enqueue(trace);

        while (_traces.Count > _options.BufferSize)
        {
            _traces.TryDequeue(out _);
        }

        TraceRecorded?.Invoke(this, trace);
    }

    public IReadOnlyList<TraceEntry> GetAll()
    {
        return _traces.ToList();
    }

    public IReadOnlyList<TraceEntry> GetByCorrelation(string correlationId)
    {
        return _traces.Where(t => t.CorrelationId == correlationId).ToList();
    }

    public void Clear()
    {
        while (_traces.TryDequeue(out _)) { }
        Interlocked.Exchange(ref _sequenceCounter, 0);
    }
}