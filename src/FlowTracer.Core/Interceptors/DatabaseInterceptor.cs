using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using FlowTracer.Core.Models;
using FlowTracer.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FlowTracer.Core.Interceptors;

/// <summary>
/// Intercepts EF Core database operations to capture query traces.
/// </summary>
public sealed class DatabaseInterceptor : DbCommandInterceptor
{
    private readonly TraceCollector _traceCollector;
    private readonly CorrelationTracker _correlationTracker;
    private readonly TracerOptions _options;
    private readonly ConcurrentDictionary<Guid, (TraceEntry Entry, Stopwatch Timer)> _activeTraces = new();

    public DatabaseInterceptor(TraceCollector traceCollector, CorrelationTracker correlationTracker, TracerOptions options)
    {
        _traceCollector = traceCollector;
        _correlationTracker = correlationTracker;
        _options = options;
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        if (_options.TrackDatabase)
        {
            StartTrace(command, eventData);
        }
        return base.ReaderExecuting(command, eventData, result);
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (_options.TrackDatabase)
        {
            StartTrace(command, eventData);
        }
        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        if (_options.TrackDatabase)
        {
            StartTrace(command, eventData);
        }
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (_options.TrackDatabase)
        {
            StartTrace(command, eventData);
        }
        return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        if (_options.TrackDatabase)
        {
            StartTrace(command, eventData);
        }
        return base.ScalarExecuting(command, eventData, result);
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        if (_options.TrackDatabase)
        {
            StartTrace(command, eventData);
        }
        return await base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        if (_options.TrackDatabase)
        {
            CompleteTrace(eventData, result.RecordsAffected);
        }
        return base.ReaderExecuted(command, eventData, result);
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        if (_options.TrackDatabase)
        {
            CompleteTrace(eventData, result.RecordsAffected);
        }
        return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        if (_options.TrackDatabase)
        {
            CompleteTrace(eventData, result);
        }
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override async ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (_options.TrackDatabase)
        {
            CompleteTrace(eventData, result);
        }
        return await base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        if (_options.TrackDatabase)
        {
            CompleteTrace(eventData, null);
        }
        return base.ScalarExecuted(command, eventData, result);
    }

    public override async ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        if (_options.TrackDatabase)
        {
            CompleteTrace(eventData, null);
        }
        return await base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    private void StartTrace(DbCommand command, CommandEventData eventData)
    {
        var queryKind = DetermineQueryKind(command.CommandText);
        var parameters = ExtractParameters(command);

        var databaseTrace = new DatabaseTrace
        {
            SqlQuery = command.CommandText,
            Parameters = parameters,
            Kind = queryKind,
            DatabaseName = eventData.Context?.Database?.GetDbConnection()?.Database ?? "unknown"
        };

        var traceEntry = new TraceEntry
        {
            Kind = TraceKind.DatabaseQuery,
            CorrelationId = _correlationTracker.GetOrCreate(),
            Location = BuildCodeLocation(),
            Database = databaseTrace
        };

        var timer = Stopwatch.StartNew();
        _activeTraces[eventData.CommandId] = (traceEntry, timer);
    }

    private void CompleteTrace(CommandExecutedEventData eventData, int? rowsAffected)
    {
        if (_activeTraces.TryRemove(eventData.CommandId, out var traceInfo))
        {
            traceInfo.Timer.Stop();
            
            // Update the trace entry with duration and rows affected
            traceInfo.Entry.DurationMs = traceInfo.Timer.ElapsedMilliseconds;
            if (traceInfo.Entry.Database != null)
            {
                traceInfo.Entry.Database.RowsAffected = rowsAffected;
            }

            _traceCollector.Record(traceInfo.Entry);
        }
    }

    private QueryKind DetermineQueryKind(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return QueryKind.Other;

        var normalized = sql.TrimStart().ToUpperInvariant();

        if (normalized.StartsWith("SELECT"))
            return QueryKind.Select;
        if (normalized.StartsWith("INSERT"))
            return QueryKind.Insert;
        if (normalized.StartsWith("UPDATE"))
            return QueryKind.Update;
        if (normalized.StartsWith("DELETE"))
            return QueryKind.Delete;

        return QueryKind.Other;
    }

    private Dictionary<string, object?> ExtractParameters(DbCommand command)
    {
        var parameters = new Dictionary<string, object?>();

        foreach (DbParameter param in command.Parameters)
        {
            parameters[param.ParameterName] = param.Value;
        }

        return parameters;
    }

    private CodeLocation BuildCodeLocation()
    {
        if (!_options.CaptureStackTraces)
        {
            return new CodeLocation();
        }

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

            // Skip FlowTracer, System, Microsoft.EntityFrameworkCore namespaces
            if (!namespaceName.StartsWith("FlowTracer") &&
                !namespaceName.StartsWith("System") &&
                !namespaceName.StartsWith("Microsoft.EntityFrameworkCore"))
            {
                return new CodeLocation
                {
                    FilePath = frame.GetFileName() ?? "unknown",
                    LineNumber = frame.GetFileLineNumber(),
                    MethodName = method?.Name ?? "unknown",
                    ClassName = declType?.Name ?? "unknown",
                    StackTrace = _options.CaptureStackTraces ? stack.ToString() : string.Empty
                };
            }
        }

        return new CodeLocation();
    }
}
