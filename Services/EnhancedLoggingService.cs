using Serilog;
using Serilog.Context;
using Serilog.Events;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace TaskMaster.Services;

/// <summary>
/// Enhanced logging service with structured logging, multiple targets, and export capabilities
/// </summary>
public static class EnhancedLoggingService
{
    private static ILogger? _logger;
    private static readonly ConcurrentQueue<LogEntry> _memoryBuffer = new();
    private static readonly int MaxMemoryEntries = 10000;

    static EnhancedLoggingService()
    {
        InitializeLogger();
    }

    private static void InitializeLogger()
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskMaster", "Logs");

        Directory.CreateDirectory(logDirectory);

        var logFilePath = Path.Combine(logDirectory, $"taskmaster-{DateTime.Now:yyyyMMdd}.log");
        var jsonLogFilePath = Path.Combine(logDirectory, $"taskmaster-{DateTime.Now:yyyyMMdd}.json");

        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Application", "TaskMaster")
            .WriteTo.File(logFilePath,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: 30,
                shared: true)
            .WriteTo.File(new Serilog.Formatting.Json.JsonFormatter(), jsonLogFilePath,
                retainedFileCountLimit: 30,
                shared: true)
            .WriteTo.Debug(outputTemplate: "[TaskMaster] {Timestamp:HH:mm:ss} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}")
            .CreateLogger();

        Log.Logger = _logger;

        // Log system startup
        LogSystemInfo();
    }

    public static void LogTrace(string message, string? context = null, object? data = null)
        => LogWithData(LogEventLevel.Verbose, message, context, data);

    public static void LogDebug(string message, string? context = null, object? data = null)
        => LogWithData(LogEventLevel.Debug, message, context, data);

    public static void LogInfo(string message, string? context = null, object? data = null)
        => LogWithData(LogEventLevel.Information, message, context, data);

    public static void LogWarning(string message, string? context = null, object? data = null)
        => LogWithData(LogEventLevel.Warning, message, context, data);

    public static void LogError(string message, Exception? exception = null, string? context = null, object? data = null)
        => LogWithData(LogEventLevel.Error, message, context, data, exception);

    public static void LogCritical(string message, Exception? exception = null, string? context = null, object? data = null)
        => LogWithData(LogEventLevel.Fatal, message, context, data, exception);

    /// <summary>
    /// Log property changes - critical for debugging binding issues
    /// </summary>
    public static void LogPropertyChange(string propertyName, object? oldValue, object? newValue, string? context = null)
    {
        var data = new
        {
            PropertyName = propertyName,
            OldValue = oldValue?.ToString(),
            NewValue = newValue?.ToString(),
            Changed = !Equals(oldValue, newValue)
        };

        LogWithData(LogEventLevel.Debug,
            $"Property changed: {propertyName} = {oldValue} → {newValue}",
            context ?? "PropertyChange",
            data);
    }

    /// <summary>
    /// Log command execution - helps track user actions
    /// </summary>
    public static void LogCommandExecution(string commandName, object? parameter = null, string? context = null)
    {
        var data = new
        {
            CommandName = commandName,
            Parameter = parameter?.ToString(),
            Timestamp = DateTime.UtcNow
        };

        LogWithData(LogEventLevel.Information,
            $"Command executed: {commandName}" + (parameter != null ? $" with parameter: {parameter}" : ""),
            context ?? "Command",
            data);
    }

    /// <summary>
    /// Log performance metrics
    /// </summary>
    public static void LogPerformance(string operation, TimeSpan duration, string? context = null, object? additionalData = null)
    {
        var data = new
        {
            Operation = operation,
            DurationMs = duration.TotalMilliseconds,
            AdditionalData = additionalData
        };

        var level = duration.TotalMilliseconds > 1000 ? LogEventLevel.Warning : LogEventLevel.Debug;
        LogWithData(level,
            $"Performance: {operation} took {duration.TotalMilliseconds:F2}ms",
            context ?? "Performance",
            data);
    }

    /// <summary>
    /// Log WPF binding issues
    /// </summary>
    public static void LogBindingIssue(string bindingPath, string target, string issue, string? context = null)
    {
        var data = new
        {
            BindingPath = bindingPath,
            Target = target,
            Issue = issue
        };

        LogWithData(LogEventLevel.Warning,
            $"Binding issue: {bindingPath} → {target}: {issue}",
            context ?? "Binding",
            data);
    }

    /// <summary>
    /// Log database operations
    /// </summary>
    public static void LogDatabaseOperation(string operation, string? query = null, TimeSpan? duration = null, int? affectedRows = null, string? context = null)
    {
        var data = new
        {
            Operation = operation,
            Query = query,
            DurationMs = duration?.TotalMilliseconds,
            AffectedRows = affectedRows
        };

        LogWithData(LogEventLevel.Debug,
            $"Database: {operation}" + (duration.HasValue ? $" ({duration.Value.TotalMilliseconds:F2}ms)" : ""),
            context ?? "Database",
            data);
    }

    private static void LogWithData(LogEventLevel level, string message, string? context, object? data, Exception? exception = null)
    {
        using (context != null ? LogContext.PushProperty("SourceContext", context) : null)
        {
            // Add structured data if provided
            if (data != null)
            {
                using (LogContext.PushProperty("Data", data, destructureObjects: true))
                {
                    _logger?.Write(level, exception, message);
                }
            }
            else
            {
                _logger?.Write(level, exception, message);
            }
        }

        // Add to memory buffer for real-time viewing
        AddToMemoryBuffer(new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level.ToString(),
            Context = context,
            Message = message,
            Data = data,
            Exception = exception
        });
    }

    private static void AddToMemoryBuffer(LogEntry entry)
    {
        _memoryBuffer.Enqueue(entry);

        // Keep buffer size under control
        while (_memoryBuffer.Count > MaxMemoryEntries)
        {
            _memoryBuffer.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Get recent log entries from memory buffer
    /// </summary>
    public static List<LogEntry> GetRecentLogEntries(int count = 100)
    {
        return _memoryBuffer.TakeLast(count).ToList();
    }

    /// <summary>
    /// Get filtered log entries
    /// </summary>
    public static List<LogEntry> GetFilteredLogEntries(
        string? levelFilter = null,
        string? contextFilter = null,
        string? searchText = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var entries = _memoryBuffer.AsEnumerable();

        if (!string.IsNullOrEmpty(levelFilter))
            entries = entries.Where(e => e.Level.Equals(levelFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(contextFilter))
            entries = entries.Where(e => e.Context?.Contains(contextFilter, StringComparison.OrdinalIgnoreCase) == true);

        if (!string.IsNullOrEmpty(searchText))
            entries = entries.Where(e => e.Message.Contains(searchText, StringComparison.OrdinalIgnoreCase));

        if (fromDate.HasValue)
            entries = entries.Where(e => e.Timestamp >= fromDate.Value);

        if (toDate.HasValue)
            entries = entries.Where(e => e.Timestamp <= toDate.Value);

        return entries.ToList();
    }

    /// <summary>
    /// Export debug package with logs and system info
    /// </summary>
    public static async Task<string> ExportDebugPackageAsync(string? exportPath = null)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var packageName = $"TaskMaster-DebugPackage-{timestamp}.zip";
        var fullPath = exportPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), packageName);

        using var archive = System.IO.Compression.ZipFile.Open(fullPath, System.IO.Compression.ZipArchiveMode.Create);

        // Add log files
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskMaster", "Logs");

        if (Directory.Exists(logDirectory))
        {
            foreach (var logFile in Directory.GetFiles(logDirectory, "*.log"))
            {
                archive.CreateEntryFromFile(logFile, $"logs/{Path.GetFileName(logFile)}");
            }

            foreach (var jsonFile in Directory.GetFiles(logDirectory, "*.json"))
            {
                archive.CreateEntryFromFile(jsonFile, $"logs/{Path.GetFileName(jsonFile)}");
            }
        }

        // Add system information
        var systemInfo = await GetSystemInfoAsync();
        var systemInfoEntry = archive.CreateEntry("system-info.json");
        using (var stream = systemInfoEntry.Open())
        using (var writer = new StreamWriter(stream))
        {
            await writer.WriteAsync(JsonSerializer.Serialize(systemInfo, new JsonSerializerOptions { WriteIndented = true }));
        }

        // Add memory buffer (recent logs)
        var recentLogs = GetRecentLogEntries(1000);
        var recentLogsEntry = archive.CreateEntry("recent-logs.json");
        using (var stream = recentLogsEntry.Open())
        using (var writer = new StreamWriter(stream))
        {
            await writer.WriteAsync(JsonSerializer.Serialize(recentLogs, new JsonSerializerOptions { WriteIndented = true }));
        }

        // Add database file if exists
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskMaster", "tasks.db");

        if (File.Exists(dbPath))
        {
            archive.CreateEntryFromFile(dbPath, "database/tasks.db");
        }

        return fullPath;
    }

    private static async Task<object> GetSystemInfoAsync()
    {
        var systemInfo = new
        {
            Timestamp = DateTime.UtcNow,
            Environment = new
            {
                OS = Environment.OSVersion.ToString(),
                MachineName = Environment.MachineName,
                UserName = SanitizePath(Environment.UserName),
                CurrentDirectory = SanitizePath(Environment.CurrentDirectory),
                AppDirectory = SanitizePath(AppDomain.CurrentDomain.BaseDirectory),
                DotNetVersion = Environment.Version.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                WorkingSet = Environment.WorkingSet,
                Is64BitOS = Environment.Is64BitOperatingSystem,
                Is64BitProcess = Environment.Is64BitProcess
            },
            Application = new
            {
                Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
                Title = "TaskMaster - WPF TaskSpec Generator",
                StartTime = Process.GetCurrentProcess().StartTime,
                MemoryUsage = GC.GetTotalMemory(false)
            },
            ClaudeCliInfo = await GetClaudeCliInfoAsync()
        };

        return systemInfo;
    }

    private static async Task<object> GetClaudeCliInfoAsync()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "claude",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return new
            {
                Available = process.ExitCode == 0,
                Version = process.ExitCode == 0 ? stdout.Trim() : null,
                Error = process.ExitCode != 0 ? stderr.Trim() : null,
                ExitCode = process.ExitCode
            };
        }
        catch (Exception ex)
        {
            return new
            {
                Available = false,
                Error = ex.Message
            };
        }
    }

    private static string SanitizePath(string path)
    {
        // Replace username with [USERNAME] for privacy
        var username = Environment.UserName;
        return path.Replace(username, "[USERNAME]", StringComparison.OrdinalIgnoreCase);
    }

    public static void LogSystemInfo()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== TaskMaster Enhanced Logging System Started ===");
        sb.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Application: TaskMaster - WPF TaskSpec Generator");
        sb.AppendLine($"Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
        sb.AppendLine($"OS: {Environment.OSVersion}");
        sb.AppendLine($"Machine: {Environment.MachineName}");
        sb.AppendLine($"User: {SanitizePath(Environment.UserName)}");
        sb.AppendLine($".NET Version: {Environment.Version}");
        sb.AppendLine($"Working Directory: {SanitizePath(Environment.CurrentDirectory)}");
        sb.AppendLine($"Memory Buffer Size: {MaxMemoryEntries} entries");
        sb.AppendLine("=== System Ready ===");

        LogInfo(sb.ToString(), "System");
    }

    /// <summary>
    /// Legacy compatibility - forward to new logging methods
    /// </summary>
    public static string GetLogFilePath()
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskMaster", "Logs");
        return Path.Combine(logDirectory, $"taskmaster-{DateTime.Now:yyyyMMdd}.log");
    }

    /// <summary>
    /// Legacy compatibility - get recent logs as text
    /// </summary>
    public static string GetRecentLogs(int lineCount = 100)
    {
        var entries = GetRecentLogEntries(lineCount);
        var sb = new StringBuilder();

        foreach (var entry in entries)
        {
            sb.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Level}] [{entry.Context}] {entry.Message}");
            if (entry.Exception != null)
            {
                sb.AppendLine($"  Exception: {entry.Exception}");
            }
        }

        return sb.ToString();
    }

    public static void Shutdown()
    {
        LogInfo("Enhanced logging service shutting down", "System");
        Log.CloseAndFlush();
    }
}

/// <summary>
/// Represents a log entry in memory
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "";
    public string? Context { get; set; }
    public string Message { get; set; } = "";
    public object? Data { get; set; }
    public Exception? Exception { get; set; }
}