using System.Net;
using System.Reflection;
using FlowTracer.Core;
using FlowTracer.Core.Models;
using FlowTracer.Core.Services;
using FlowTracer.WebUI.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace FlowTracer.WebUI;

public sealed class DashboardServer : IDisposable
{
    private readonly TracerOptions _options;
    private readonly TraceCollector _collector;
    private WebApplication? _app;
    private CancellationTokenSource? _cts;

    public DashboardServer(TracerOptions options, TraceCollector collector)
    {
        _options = options;
        _collector = collector;
    }

    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        
        var port = _options.DashboardPort;
        var maxAttempts = 10;
        var attempt = 0;

        while (attempt < maxAttempts)
        {
            try
            {
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    WebRootPath = "wwwroot"
                });

                // Configure services
                builder.Services.AddSignalR();
                builder.Services.AddSingleton(_collector);
                builder.Services.AddSingleton(_options);
                builder.Services.AddCors(options =>
                {
                    options.AddDefaultPolicy(policy =>
                    {
                        policy.WithOrigins("http://localhost", "https://localhost")
                              .AllowAnyMethod()
                              .AllowAnyHeader();
                    });
                });

                // Configure Kestrel
                builder.WebHost.ConfigureKestrel(options =>
                {
                    options.Listen(IPAddress.Loopback, port);
                });

                builder.WebHost.UseUrls($"http://localhost:{port}");

                _app = builder.Build();

                // Configure middleware
                _app.UseCors();

                // Serve static files from embedded resources
                var assembly = Assembly.GetExecutingAssembly();
                var embeddedProvider = new ManifestEmbeddedFileProvider(assembly, "wwwroot");
                
                _app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = embeddedProvider,
                    RequestPath = ""
                });

                // API Endpoints
                _app.MapGet("/api/traces", (TraceCollector collector) =>
                {
                    return Results.Json(collector.GetAll());
                });

                _app.MapGet("/api/traces/{correlationId}", (string correlationId, TraceCollector collector) =>
                {
                    var traces = collector.GetByCorrelation(correlationId);
                    return traces.Count > 0 ? Results.Json(traces) : Results.NotFound();
                });

                _app.MapDelete("/api/traces", (TraceCollector collector) =>
                {
                    collector.Clear();
                    return Results.Ok(new { message = "All traces cleared" });
                });

                _app.MapGet("/api/stats", (TraceCollector collector) =>
                {
                    var traces = collector.GetAll();
                    var httpCount = traces.Count(t => t.Kind == TraceKind.HttpRequest);
                    var dbCount = traces.Count(t => t.Kind == TraceKind.DatabaseQuery);
                    var avgDuration = traces.Any() ? traces.Average(t => t.DurationMs) : 0;

                    return Results.Json(new
                    {
                        totalTraces = traces.Count,
                        httpCalls = httpCount,
                        dbQueries = dbCount,
                        avgDurationMs = Math.Round(avgDuration, 2)
                    });
                });

                // SignalR Hub
                _app.MapHub<TraceHub>("/tracehub");

                // Default route - serve index.html
                _app.MapGet("/", async (HttpContext context) =>
                {
                    context.Response.ContentType = "text/html";
                    var htmlStream = embeddedProvider.GetFileInfo("index.html").CreateReadStream();
                    await htmlStream.CopyToAsync(context.Response.Body);
                });

                // Start the server
                await _app.StartAsync(_cts.Token);

                Console.WriteLine($"✅ ApiFlowTracer Dashboard started at http://localhost:{port}");
                return;
            }
            catch (IOException ex) when (ex.Message.Contains("address already in use") || 
                                         ex.Message.Contains("EADDRINUSE"))
            {
                attempt++;
                port++;
                Console.WriteLine($"⚠️  Port {port - 1} in use, trying port {port}...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to start dashboard server: {ex.Message}");
                throw;
            }
        }

        throw new InvalidOperationException($"Could not start dashboard server after {maxAttempts} attempts");
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _app?.StopAsync().GetAwaiter().GetResult();
        _app?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _cts?.Dispose();
    }
}
