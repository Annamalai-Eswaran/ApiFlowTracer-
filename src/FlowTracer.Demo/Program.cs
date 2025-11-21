using FlowTracer.Core;

var builder = WebApplication.CreateBuilder(args);

// Add FlowTracer services
builder.Services.AddApiFlowTracer(options =>
{
    options.DashboardPort = 5050;
    options.OpenBrowserOnStart = false; // Don't auto-open browser for testing
});

// Add HttpClient for testing
builder.Services.AddHttpClient();

var app = builder.Build();

// Enable FlowTracer middleware
app.UseApiFlowTracer();

// Test endpoints
app.MapGet("/", () => "ApiFlowTracer Demo - Dashboard at http://localhost:5050");

app.MapGet("/test-http", async (IHttpClientFactory httpFactory) =>
{
    var client = httpFactory.CreateClient();
    var response = await client.GetAsync("https://jsonplaceholder.typicode.com/todos/1");
    var content = await response.Content.ReadAsStringAsync();
    return Results.Ok(new { message = "HTTP call made", data = content });
});

app.MapGet("/test-multiple", async (IHttpClientFactory httpFactory) =>
{
    var client = httpFactory.CreateClient();
    var tasks = new List<Task<HttpResponseMessage>>();
    
    for (int i = 1; i <= 5; i++)
    {
        tasks.Add(client.GetAsync($"https://jsonplaceholder.typicode.com/todos/{i}"));
    }
    
    await Task.WhenAll(tasks);
    return Results.Ok(new { message = "5 HTTP calls completed" });
});

Console.WriteLine("🚀 Demo application started");
Console.WriteLine("📍 Main app: http://localhost:5000");
Console.WriteLine("🔍 Dashboard: http://localhost:5050");
Console.WriteLine("");
Console.WriteLine("Test endpoints:");
Console.WriteLine("  GET /test-http - Make a single HTTP call");
Console.WriteLine("  GET /test-multiple - Make 5 HTTP calls");

app.Run("http://localhost:5000");
