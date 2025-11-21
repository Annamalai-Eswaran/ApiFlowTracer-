using FlowTracer.Core.Models;
using FlowTracer.Core.Services;
using Microsoft.AspNetCore.SignalR;

namespace FlowTracer.WebUI.Hubs;

public sealed class TraceHub : Hub
{
    private readonly TraceCollector _collector;

    public TraceHub(TraceCollector collector)
    {
        _collector = collector;
        
        // Subscribe to trace events when hub is created
        _collector.TraceRecorded += OnTraceRecorded;
    }

    private void OnTraceRecorded(object? sender, TraceEntry trace)
    {
        // Broadcast to all connected clients
        Clients.All.SendAsync("ReceiveTrace", trace).ConfigureAwait(false);
    }

    public override Task OnConnectedAsync()
    {
        Console.WriteLine($"🔌 Dashboard client connected: {Context.ConnectionId}");
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        Console.WriteLine($"🔌 Dashboard client disconnected: {Context.ConnectionId}");
        return base.OnDisconnectedAsync(exception);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _collector.TraceRecorded -= OnTraceRecorded;
        }
        base.Dispose(disposing);
    }
}
