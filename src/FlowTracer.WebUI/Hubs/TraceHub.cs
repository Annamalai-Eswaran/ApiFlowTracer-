using FlowTracer.Core.Models;
using FlowTracer.Core.Services;
using Microsoft.AspNetCore.SignalR;

namespace FlowTracer.WebUI.Hubs;

public sealed class TraceHub : Hub
{
    private static readonly object _lockObj = new();
    private static EventHandler<TraceEntry>? _eventHandler;
    private static IHubContext<TraceHub>? _hubContext;

    private readonly TraceCollector _collector;

    public TraceHub(TraceCollector collector, IHubContext<TraceHub> hubContext)
    {
        _collector = collector;
        
        // Initialize event handler only once using thread-safe pattern
        lock (_lockObj)
        {
            if (_eventHandler == null && _hubContext == null)
            {
                _hubContext = hubContext;
                _eventHandler = async (sender, trace) =>
                {
                    try
                    {
                        await _hubContext.Clients.All.SendAsync("ReceiveTrace", trace);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️  Failed to broadcast trace: {ex.Message}");
                    }
                };
                _collector.TraceRecorded += _eventHandler;
            }
        }
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
}
