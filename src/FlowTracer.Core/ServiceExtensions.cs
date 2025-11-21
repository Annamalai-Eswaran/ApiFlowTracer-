using System.Diagnostics;
using FlowTracer.Core.Handlers;
using FlowTracer.Core.Interceptors;
using FlowTracer.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace FlowTracer.Core;

/// <summary>
/// Extension methods for zero-config FlowTracer integration.
/// </summary>
public static class ServiceExtensions
{
    /// <summary>
    /// Adds ApiFlowTracer services with automatic HttpClient and database tracing.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApiFlowTracer(
        this IServiceCollection services,
        Action<TracerOptions>? configure = null)
    {
        // Create and configure options
        var options = new TracerOptions();
        configure?.Invoke(options);

        // Register all services as singletons
        services.AddSingleton(options);
        services.AddSingleton<TraceCollector>();
        services.AddSingleton<CorrelationTracker>();
        services.AddSingleton<DatabaseInterceptor>();
        
        // Register HttpTracingHandler as transient (created per HttpClient)
        services.AddTransient<HttpTracingHandler>();

        // CRITICAL: Auto-register HttpTracingHandler for ALL HttpClient instances
        services.ConfigureAll<HttpClientFactoryOptions>(httpClientOptions =>
        {
            httpClientOptions.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                // Create handler instance with DI
                var handler = ActivatorUtilities.CreateInstance<HttpTracingHandler>(builder.Services);

                // Set the inner handler
                handler.InnerHandler = builder.PrimaryHandler;
                
                // Replace primary handler with our tracing handler
                builder.PrimaryHandler = handler;
            });
        });

        return services;
    }

    /// <summary>
    /// Enables ApiFlowTracer middleware with automatic dashboard startup.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseApiFlowTracer(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<TracerOptions>();
        var collector = app.ApplicationServices.GetRequiredService<TraceCollector>();
        var env = app.ApplicationServices.GetService<IHostEnvironment>();

        // Check if should run in production
        if (options.DevelopmentOnly && env != null && env.IsProduction())
        {
            Console.WriteLine("🔍 ApiFlowTracer: Disabled in Production mode");
            return app;
        }

        Console.WriteLine("🔍 ApiFlowTracer: Starting dashboard...");

        // Start the real dashboard server
        Task.Run(async () =>
        {
            try
            {
                var server = new DashboardServer(options, collector);
                await server.StartAsync();
                Console.WriteLine($"✅ ApiFlowTracer: Dashboard running at http://localhost:{options.DashboardPort}");
                
                if (options.OpenBrowserOnStart)
                {
                    await Task.Delay(1000); // Wait for server to be ready
                    OpenBrowser($"http://localhost:{options.DashboardPort}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  ApiFlowTracer: Failed to start dashboard - {ex.Message}");
            }
        });

        return app;
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(psi);
            Console.WriteLine($"🌐 ApiFlowTracer: Opening browser to {url}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  ApiFlowTracer: Could not open browser - {ex.Message}");
        }
    }
}
