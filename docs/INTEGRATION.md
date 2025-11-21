# ApiFlowTracer Integration Guide

## Quick Start - Just 2 Lines!

Add ApiFlowTracer to your ASP.NET Core application in **Program.cs**:

```csharp
using FlowTracer.Core;

var builder = WebApplication.CreateBuilder(args);

// Your existing service registrations
builder.Services.AddControllers();

// Add FlowTracer - Line 1
builder.Services.AddApiFlowTracer();

var app = builder.Build();

// Your existing middleware
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Enable FlowTracer - Line 2
app.UseApiFlowTracer();

app.Run();
```

That's it! Everything else happens automatically.

## What Gets Tracked Automatically

### ✅ All HTTP Calls (Zero Configuration)

**Every** `HttpClient` instance automatically traces requests:

```csharp
// Named HttpClient - automatically tracked
builder.Services.AddHttpClient("MyApi", client => {
    client.BaseAddress = new Uri("https://api.example.com");
});

// Typed HttpClient - automatically tracked
builder.Services.AddHttpClient<IMyService, MyService>();

// Direct usage - automatically tracked
var client = new HttpClient();
await client.GetAsync("https://api.example.com/data");
```

**No per-client configuration needed!** The handler is injected into ALL HttpClient instances.

### ✅ All Database Queries (Zero Configuration)

**Every** Entity Framework Core query is automatically tracked:

```csharp
// Your DbContext works as normal - no changes needed
public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    
    // No special configuration needed!
}

// All queries are automatically traced
var users = await context.Users.Where(u => u.IsActive).ToListAsync();
var user = await context.Users.FindAsync(userId);
await context.SaveChangesAsync();
```

**No DbContext modifications needed!** The interceptor is automatically registered.

## Expected Console Output

When you start your application, you'll see:

```
🔍 ApiFlowTracer: Starting...
✅ ApiFlowTracer: Dashboard available at http://localhost:5050
📊 ApiFlowTracer: Tracking HTTP calls and database queries
🌐 ApiFlowTracer: Opening browser to http://localhost:5050
```

Your browser will automatically open to the dashboard (in development mode).

## Configuration Options

Customize behavior with optional configuration:

```csharp
builder.Services.AddApiFlowTracer(options =>
{
    // Dashboard settings
    options.DashboardPort = 5050;              // Default: 5050
    options.OpenBrowserOnStart = true;          // Default: true
    options.DevelopmentOnly = true;             // Default: true
    
    // What to track
    options.TrackHttp = true;                   // Default: true
    options.TrackDatabase = true;               // Default: true
    options.TrackMvcActions = true;             // Default: true
    
    // Buffer and capture settings
    options.BufferSize = 500;                   // Default: 500 traces
    options.CapturePayloads = true;             // Default: true
    options.MaxPayloadKb = 512;                 // Default: 512 KB
    options.CaptureStackTraces = true;          // Default: true
    
    // Filtering
    options.IgnoreUrls = new[] { 
        "/health", 
        "/metrics",
        "/_blazor"
    };
});
```

### Configuration Details

| Option | Description | Default |
|--------|-------------|---------|
| `DashboardPort` | Port for the dashboard web UI | 5050 |
| `OpenBrowserOnStart` | Auto-open browser on startup | true |
| `DevelopmentOnly` | Disable in production environment | true |
| `TrackHttp` | Enable HTTP call tracking | true |
| `TrackDatabase` | Enable database query tracking | true |
| `TrackMvcActions` | Enable MVC action tracking | true |
| `BufferSize` | Max number of traces to keep in memory | 500 |
| `CapturePayloads` | Capture request/response bodies | true |
| `MaxPayloadKb` | Max payload size to capture (KB) | 512 |
| `CaptureStackTraces` | Include stack traces in traces | true |
| `IgnoreUrls` | URL patterns to exclude from tracing | [] |

## Dashboard Features

Access the dashboard at `http://localhost:5050` (or your configured port):

### Real-Time Trace Feed

See all operations as they happen:

```
🌐 HTTP GET → https://api.example.com/users/123
   📂 UserController.cs:42 in GetUserAsync()
   ⏱️ 145ms | Status: 200 OK
   
🗄️ SELECT * FROM Users WHERE Id = @p0
   📂 UserRepository.cs:28 in FindByIdAsync()
   ⏱️ 8ms | Rows: 1
   Parameters: @p0 = 123
```

### Correlation Tracking

All operations in a single request flow share a correlation ID, making it easy to trace the complete flow through your application.

### Performance Metrics

Each trace includes:
- Execution duration
- Timestamp
- Status codes (HTTP)
- Rows affected (Database)
- Request/response payloads
- Stack traces showing exact code locations

## Troubleshooting

### Dashboard not opening

**Check the port:**
```csharp
builder.Services.AddApiFlowTracer(options => {
    options.DashboardPort = 5051; // Try a different port
});
```

**Manually open the URL:**
If the browser doesn't open automatically, navigate to `http://localhost:5050` manually.

### HttpClient calls not being tracked

**Ensure you're using IHttpClientFactory:**
```csharp
// ✅ Good - will be tracked
builder.Services.AddHttpClient<MyService>();

// ✅ Also good - will be tracked
public class MyController : ControllerBase
{
    private readonly IHttpClientFactory _factory;
    
    public MyController(IHttpClientFactory factory)
    {
        _factory = factory;
    }
    
    public async Task<IActionResult> Get()
    {
        var client = _factory.CreateClient();
        // This will be tracked
    }
}

// ⚠️ Direct new HttpClient() might not be tracked in some scenarios
// Use IHttpClientFactory for best results
```

### Database queries not being tracked

**Ensure you're using Entity Framework Core 8.0+:**
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
```

**Check the tracking option:**
```csharp
builder.Services.AddApiFlowTracer(options => {
    options.TrackDatabase = true; // Make sure this is true
});
```

### Disabled in production

By default, ApiFlowTracer only runs in Development mode:

```csharp
// To enable in all environments (not recommended)
builder.Services.AddApiFlowTracer(options => {
    options.DevelopmentOnly = false;
});
```

**Warning:** Running in production may impact performance and expose sensitive data. Use with caution.

### Too much data being captured

**Reduce buffer size:**
```csharp
builder.Services.AddApiFlowTracer(options => {
    options.BufferSize = 100; // Keep fewer traces in memory
});
```

**Disable payload capture:**
```csharp
builder.Services.AddApiFlowTracer(options => {
    options.CapturePayloads = false; // Don't capture bodies
});
```

**Filter out specific URLs:**
```csharp
builder.Services.AddApiFlowTracer(options => {
    options.IgnoreUrls = new[] { "/health", "/metrics", "/swagger" };
});
```

## Advanced Usage

### Accessing Traces Programmatically

Inject `TraceCollector` to access traces in your code:

```csharp
public class MyController : ControllerBase
{
    private readonly TraceCollector _collector;
    
    public MyController(TraceCollector collector)
    {
        _collector = collector;
    }
    
    [HttpGet("traces")]
    public IActionResult GetTraces()
    {
        var allTraces = _collector.GetAll();
        return Ok(allTraces);
    }
    
    [HttpGet("traces/{correlationId}")]
    public IActionResult GetByCorrelation(string correlationId)
    {
        var traces = _collector.GetByCorrelation(correlationId);
        return Ok(traces);
    }
}
```

### Custom Correlation IDs

Use `CorrelationTracker` to set custom correlation IDs:

```csharp
public class MyService
{
    private readonly CorrelationTracker _tracker;
    
    public MyService(CorrelationTracker tracker)
    {
        _tracker = tracker;
    }
    
    public async Task ProcessOrder(string orderId)
    {
        // Set custom correlation ID
        _tracker.Set($"order-{orderId}");
        
        // All subsequent traces will use this correlation ID
        await CallApiAsync();
        await SaveToDatabaseAsync();
    }
}
```

## Performance Impact

ApiFlowTracer is designed to have minimal performance impact:

- **HTTP tracing:** ~2-5ms overhead per request
- **Database tracing:** ~1-2ms overhead per query
- **Memory usage:** Configurable buffer size (default 500 traces)
- **Thread-safe:** Uses concurrent collections for minimal lock contention

The `DevelopmentOnly` setting ensures zero impact in production environments.

## Security Considerations

### Sensitive Data

Be careful with sensitive data in traces:

```csharp
builder.Services.AddApiFlowTracer(options => {
    // Disable payload capture for sensitive endpoints
    options.CapturePayloads = false;
    
    // Or filter specific URLs
    options.IgnoreUrls = new[] { "/api/auth", "/api/payment" };
});
```

### Production Use

ApiFlowTracer is designed for development and debugging:

- Disabled by default in production (`DevelopmentOnly = true`)
- All data stored in memory (not persisted)
- Dashboard only accessible locally
- No external dependencies or data transmission

## Integration with Existing Tools

ApiFlowTracer works alongside:

- **Application Insights:** Different purposes - AI for production monitoring, FlowTracer for development debugging
- **Serilog/NLog:** FlowTracer complements logging with visual trace flows
- **EF Core logging:** FlowTracer provides richer context and UI
- **Swagger:** Use both - FlowTracer shows actual runtime behavior

## Next Steps

- Explore the dashboard at `http://localhost:5050`
- Try different configuration options
- Check the correlation flows in complex scenarios
- Review the captured payloads and stack traces
- Share feedback and report issues on GitHub

## Getting Help

- **GitHub Issues:** https://github.com/Annamalai-Eswaran/ApiFlowTracer-/issues
- **Documentation:** https://github.com/Annamalai-Eswaran/ApiFlowTracer-
- **Examples:** Check the demo project in the repository

Happy debugging! 🐛🔨
