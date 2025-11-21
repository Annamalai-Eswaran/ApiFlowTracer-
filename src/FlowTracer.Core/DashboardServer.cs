using System.Net;
using System.Text.Json;
using FlowTracer.Core.Models;
using FlowTracer.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlowTracer.Core;

/// <summary>
/// Self-hosted Kestrel web server for the ApiFlowTracer dashboard.
/// </summary>
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

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = Array.Empty<string>()
        });

        // Minimal services
        builder.Services.AddSingleton(_collector);
        builder.Services.AddSingleton(_options);

        // Configure CORS for local development
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        // Configure Kestrel to listen on the specified port
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, _options.DashboardPort);
        });

        // Suppress startup messages
        builder.Logging.ClearProviders();

        _app = builder.Build();

        // Enable CORS
        _app.UseCors();

        // API Endpoints
        _app.MapGet("/api/traces", () =>
        {
            return Results.Json(_collector.GetAll());
        });

        _app.MapGet("/api/traces/{correlationId}", (string correlationId) =>
        {
            var traces = _collector.GetByCorrelation(correlationId);
            return traces.Count > 0 ? Results.Json(traces) : Results.NotFound();
        });

        _app.MapDelete("/api/traces", () =>
        {
            _collector.Clear();
            return Results.Ok(new { message = "All traces cleared" });
        });

        _app.MapGet("/api/stats", () =>
        {
            var traces = _collector.GetAll();
            return Results.Json(new
            {
                total = traces.Count,
                http = traces.Count(t => t.Kind == TraceKind.HttpRequest),
                database = traces.Count(t => t.Kind == TraceKind.DatabaseQuery),
                avgDuration = traces.Any() ? Math.Round(traces.Average(t => t.DurationMs), 2) : 0
            });
        });

        // Dashboard UI
        _app.MapGet("/", async (HttpContext context) =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(GetDashboardHtml());
        });

        // Start the server
        await _app.StartAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        if (_app != null)
        {
            await _app.StopAsync();
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        if (_app != null)
        {
            try
            {
                _app.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // Ignore disposal errors
            }
        }
        _cts?.Dispose();
    }

    private static string GetDashboardHtml()
    {
        return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>🔍 ApiFlowTracer Dashboard</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }

        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
            background: #f5f7fa;
            color: #2c3e50;
            line-height: 1.6;
        }

        .header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 2rem;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }

        .header h1 {
            font-size: 2rem;
            font-weight: 600;
            margin-bottom: 0.5rem;
        }

        .header p {
            opacity: 0.9;
            font-size: 0.95rem;
        }

        .container {
            max-width: 1400px;
            margin: 0 auto;
            padding: 2rem;
        }

        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 1.5rem;
            margin-bottom: 2rem;
        }

        .stat-card {
            background: white;
            padding: 1.5rem;
            border-radius: 12px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.08);
            transition: transform 0.2s, box-shadow 0.2s;
        }

        .stat-card:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(0,0,0,0.12);
        }

        .stat-label {
            font-size: 0.875rem;
            color: #64748b;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            margin-bottom: 0.5rem;
        }

        .stat-value {
            font-size: 2rem;
            font-weight: 700;
            color: #1e293b;
        }

        .controls {
            background: white;
            padding: 1.5rem;
            border-radius: 12px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.08);
            margin-bottom: 2rem;
            display: flex;
            gap: 1rem;
            flex-wrap: wrap;
            align-items: center;
        }

        .search-box {
            flex: 1;
            min-width: 250px;
            padding: 0.75rem 1rem;
            border: 2px solid #e2e8f0;
            border-radius: 8px;
            font-size: 0.95rem;
            transition: border-color 0.2s;
        }

        .search-box:focus {
            outline: none;
            border-color: #667eea;
        }

        .btn {
            padding: 0.75rem 1.5rem;
            border: none;
            border-radius: 8px;
            font-size: 0.95rem;
            font-weight: 500;
            cursor: pointer;
            transition: all 0.2s;
            white-space: nowrap;
        }

        .btn-primary {
            background: #667eea;
            color: white;
        }

        .btn-primary:hover {
            background: #5568d3;
            transform: translateY(-1px);
        }

        .btn-danger {
            background: #f44336;
            color: white;
        }

        .btn-danger:hover {
            background: #da190b;
            transform: translateY(-1px);
        }

        .traces-container {
            display: flex;
            flex-direction: column;
            gap: 1rem;
        }

        .trace-card {
            background: white;
            border-radius: 12px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.08);
            padding: 1.5rem;
            border-left: 4px solid #667eea;
            transition: transform 0.2s, box-shadow 0.2s;
        }

        .trace-card:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        }

        .trace-card.http {
            border-left-color: #2196F3;
        }

        .trace-card.database {
            border-left-color: #FF9800;
        }

        .trace-header {
            display: flex;
            align-items: center;
            gap: 1rem;
            margin-bottom: 1rem;
            flex-wrap: wrap;
        }

        .sequence-badge {
            background: #e2e8f0;
            color: #475569;
            padding: 0.25rem 0.75rem;
            border-radius: 20px;
            font-size: 0.875rem;
            font-weight: 600;
        }

        .method-badge {
            padding: 0.25rem 0.75rem;
            border-radius: 6px;
            font-size: 0.875rem;
            font-weight: 600;
            text-transform: uppercase;
        }

        .method-GET {
            background: #dbeafe;
            color: #1e40af;
        }

        .method-POST {
            background: #dcfce7;
            color: #166534;
        }

        .method-PUT {
            background: #fed7aa;
            color: #9a3412;
        }

        .method-DELETE {
            background: #fee2e2;
            color: #991b1b;
        }

        .status-badge {
            padding: 0.25rem 0.75rem;
            border-radius: 6px;
            font-size: 0.875rem;
            font-weight: 600;
        }

        .status-success {
            background: #dcfce7;
            color: #166534;
        }

        .status-error {
            background: #fee2e2;
            color: #991b1b;
        }

        .duration-badge {
            padding: 0.25rem 0.75rem;
            border-radius: 6px;
            font-size: 0.875rem;
            font-weight: 600;
        }

        .duration-fast {
            background: #dcfce7;
            color: #166534;
        }

        .duration-medium {
            background: #fef3c7;
            color: #92400e;
        }

        .duration-slow {
            background: #fee2e2;
            color: #991b1b;
        }

        .trace-content {
            margin-top: 1rem;
        }

        .trace-url {
            font-size: 0.95rem;
            color: #1e293b;
            word-break: break-all;
            margin-bottom: 0.5rem;
            font-weight: 500;
        }

        .trace-sql {
            font-family: 'Courier New', monospace;
            font-size: 0.875rem;
            color: #1e293b;
            background: #f8fafc;
            padding: 0.75rem;
            border-radius: 6px;
            overflow-x: auto;
            margin-bottom: 0.5rem;
        }

        .trace-location {
            font-size: 0.875rem;
            color: #64748b;
            margin-top: 0.5rem;
        }

        .empty-state {
            text-align: center;
            padding: 4rem 2rem;
            color: #64748b;
        }

        .empty-state-icon {
            font-size: 4rem;
            margin-bottom: 1rem;
        }

        .empty-state h3 {
            font-size: 1.5rem;
            margin-bottom: 0.5rem;
            color: #475569;
        }

        @keyframes fadeIn {
            from {
                opacity: 0;
                transform: translateY(10px);
            }
            to {
                opacity: 1;
                transform: translateY(0);
            }
        }

        .trace-card {
            animation: fadeIn 0.3s ease-out;
        }
    </style>
</head>
<body>
    <div class=""header"">
        <h1>🔍 ApiFlowTracer Dashboard</h1>
        <p>Real-time API and Database Activity Monitor</p>
    </div>

    <div class=""container"">
        <div class=""stats-grid"">
            <div class=""stat-card"">
                <div class=""stat-label"">Total Traces</div>
                <div class=""stat-value"" id=""totalTraces"">0</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-label"">HTTP Calls</div>
                <div class=""stat-value"" id=""httpCalls"">0</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-label"">DB Queries</div>
                <div class=""stat-value"" id=""dbQueries"">0</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-label"">Avg Duration</div>
                <div class=""stat-value"" id=""avgDuration"">0ms</div>
            </div>
        </div>

        <div class=""controls"">
            <input type=""text"" id=""searchBox"" class=""search-box"" placeholder=""🔍 Search traces..."">
            <button class=""btn btn-primary"" id=""refreshBtn"">🔄 Refresh</button>
            <button class=""btn btn-danger"" id=""clearBtn"">🗑️ Clear All</button>
        </div>

        <div id=""tracesContainer"" class=""traces-container""></div>
        <div id=""emptyState"" class=""empty-state"" style=""display: none;"">
            <div class=""empty-state-icon"">📭</div>
            <h3>No Traces Yet</h3>
            <p>Waiting for HTTP calls and database queries to appear...</p>
        </div>
    </div>

    <script>
        let allTraces = [];
        let autoRefreshInterval;

        // Fetch and update statistics
        async function updateStats() {
            try {
                const response = await fetch('/api/stats');
                const stats = await response.json();
                document.getElementById('totalTraces').textContent = stats.total;
                document.getElementById('httpCalls').textContent = stats.http;
                document.getElementById('dbQueries').textContent = stats.database;
                document.getElementById('avgDuration').textContent = stats.avgDuration.toFixed(2) + 'ms';
            } catch (error) {
                console.error('Failed to fetch stats:', error);
            }
        }

        // Fetch traces
        async function fetchTraces() {
            try {
                const response = await fetch('/api/traces');
                allTraces = await response.json();
                renderTraces(allTraces);
                updateStats();
            } catch (error) {
                console.error('Failed to fetch traces:', error);
            }
        }

        // Render traces
        function renderTraces(traces) {
            const container = document.getElementById('tracesContainer');
            const emptyState = document.getElementById('emptyState');

            if (traces.length === 0) {
                container.innerHTML = '';
                emptyState.style.display = 'block';
                return;
            }

            emptyState.style.display = 'none';

            // Sort by sequence number (newest first)
            const sortedTraces = [...traces].sort((a, b) => b.sequenceNumber - a.sequenceNumber);

            container.innerHTML = sortedTraces.map(trace => {
                const isHttp = trace.kind === 0; // HttpRequest
                const isDatabase = trace.kind === 1; // DatabaseQuery
                
                let content = '';
                let badges = '';

                if (isHttp && trace.http) {
                    const methodClass = `method-${trace.http.method}`;
                    badges += `<span class=""method-badge ${methodClass}"">${escapeHtml(trace.http.method)}</span>`;
                    
                    const statusCode = trace.http.statusCode || 0;
                    const statusClass = statusCode >= 200 && statusCode < 300 ? 'status-success' : 'status-error';
                    if (statusCode > 0) {
                        badges += `<span class=""status-badge ${statusClass}"">${statusCode}</span>`;
                    }
                    
                    content = `<div class=""trace-url"">${escapeHtml(trace.http.url)}</div>`;
                } else if (isDatabase && trace.database) {
                    badges += `<span class=""method-badge method-POST"">SQL</span>`;
                    content = `<div class=""trace-sql"">${escapeHtml(trace.database.sqlQuery)}</div>`;
                }

                const durationClass = trace.durationMs < 100 ? 'duration-fast' : 
                                     trace.durationMs < 500 ? 'duration-medium' : 'duration-slow';
                badges += `<span class=""duration-badge ${durationClass}"">${trace.durationMs}ms</span>`;

                const cardClass = isHttp ? 'http' : 'database';
                const location = trace.location && trace.location.filePath 
                    ? `${trace.location.filePath}:${trace.location.lineNumber}` 
                    : '';

                return `
                    <div class=""trace-card ${cardClass}"">
                        <div class=""trace-header"">
                            <span class=""sequence-badge"">#${trace.sequenceNumber}</span>
                            ${badges}
                        </div>
                        <div class=""trace-content"">
                            ${content}
                            ${location ? `<div class=""trace-location"">📍 ${escapeHtml(location)}</div>` : ''}
                        </div>
                    </div>
                `;
            }).join('');
        }

        // Search/filter traces
        function filterTraces() {
            const searchTerm = document.getElementById('searchBox').value.toLowerCase();
            
            if (!searchTerm) {
                renderTraces(allTraces);
                return;
            }

            const filtered = allTraces.filter(trace => {
                const searchableText = JSON.stringify(trace).toLowerCase();
                return searchableText.includes(searchTerm);
            });

            renderTraces(filtered);
        }

        // Clear all traces
        async function clearTraces() {
            if (!confirm('Are you sure you want to clear all traces?')) {
                return;
            }

            try {
                await fetch('/api/traces', { method: 'DELETE' });
                allTraces = [];
                renderTraces(allTraces);
                updateStats();
            } catch (error) {
                console.error('Failed to clear traces:', error);
                alert('Failed to clear traces. Please try again.');
            }
        }

        // HTML escape for security
        function escapeHtml(text) {
            const map = {
                '&': '&amp;',
                '<': '&lt;',
                '>': '&gt;',
                '""': '&quot;',
                ""'"": '&#039;'
            };
            return String(text).replace(/[&<>""']/g, m => map[m]);
        }

        // Event listeners
        document.getElementById('searchBox').addEventListener('input', filterTraces);
        document.getElementById('refreshBtn').addEventListener('click', fetchTraces);
        document.getElementById('clearBtn').addEventListener('click', clearTraces);

        // Auto-refresh every 3 seconds
        autoRefreshInterval = setInterval(fetchTraces, 3000);

        // Initial load
        fetchTraces();
    </script>
</body>
</html>";
    }
}
