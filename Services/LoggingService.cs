using System.Diagnostics;
using System.Text;

namespace TaskMaster.Services;

public static class LoggingService
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TaskMaster", "Logs");

    private static readonly string LogFilePath = Path.Combine(LogDirectory,
        $"taskmaster-{DateTime.Now:yyyyMMdd}.log");

    static LoggingService()
    {
        Directory.CreateDirectory(LogDirectory);
    }

    public static void LogInfo(string message, string? context = null)
    {
        LogMessage("INFO", message, context);
    }

    public static void LogWarning(string message, string? context = null)
    {
        LogMessage("WARN", message, context);
    }

    public static void LogError(string message, Exception? exception = null, string? context = null)
    {
        var fullMessage = message;
        if (exception != null)
        {
            fullMessage += $"\nException: {exception.GetType().Name}: {exception.Message}";
            fullMessage += $"\nStack Trace: {exception.StackTrace}";

            if (exception.InnerException != null)
            {
                fullMessage += $"\nInner Exception: {exception.InnerException.GetType().Name}: {exception.InnerException.Message}";
            }
        }

        LogMessage("ERROR", fullMessage, context);
    }

    public static void LogClaudeCommand(string command, List<string> args, string? workingDirectory = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Claude CLI Command Execution:");
        sb.AppendLine($"Command: {command}");
        sb.AppendLine($"Arguments: {string.Join(" ", args)}");
        sb.AppendLine($"Working Directory: {workingDirectory ?? "Current Directory"}");
        sb.AppendLine($"PATH Environment: {Environment.GetEnvironmentVariable("PATH")}");

        LogInfo(sb.ToString(), "ClaudeService");
    }

    public static void LogClaudeResponse(int exitCode, string stdout, string stderr, TimeSpan duration)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Claude CLI Response:");
        sb.AppendLine($"Exit Code: {exitCode}");
        sb.AppendLine($"Duration: {duration.TotalMilliseconds}ms");
        sb.AppendLine($"STDOUT Length: {stdout?.Length ?? 0}");
        sb.AppendLine($"STDERR Length: {stderr?.Length ?? 0}");

        if (!string.IsNullOrEmpty(stdout))
        {
            sb.AppendLine($"STDOUT:\n{stdout}");
        }

        if (!string.IsNullOrEmpty(stderr))
        {
            sb.AppendLine($"STDERR:\n{stderr}");
        }

        if (exitCode == 0)
        {
            LogInfo(sb.ToString(), "ClaudeService");
        }
        else
        {
            LogError(sb.ToString(), null, "ClaudeService");
        }
    }

    public static void LogSystemInfo()
    {
        var sb = new StringBuilder();
        sb.AppendLine("System Information:");
        sb.AppendLine($"OS: {Environment.OSVersion}");
        sb.AppendLine($"Machine Name: {Environment.MachineName}");
        sb.AppendLine($"User: {Environment.UserName}");
        sb.AppendLine($"Current Directory: {Environment.CurrentDirectory}");
        sb.AppendLine($"Application Directory: {AppDomain.CurrentDomain.BaseDirectory}");
        sb.AppendLine($".NET Version: {Environment.Version}");

        // Check if Claude CLI is available
        try
        {
            var claudeCheck = Process.Start(new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (claudeCheck != null)
            {
                claudeCheck.WaitForExit(5000);
                sb.AppendLine($"Claude CLI Available: {claudeCheck.ExitCode == 0}");
                if (claudeCheck.ExitCode == 0)
                {
                    sb.AppendLine($"Claude Version: {claudeCheck.StandardOutput.ReadToEnd().Trim()}");
                }
                else
                {
                    sb.AppendLine($"Claude Error: {claudeCheck.StandardError.ReadToEnd().Trim()}");
                }
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Claude CLI Check Failed: {ex.Message}");
        }

        LogInfo(sb.ToString(), "System");
    }

    private static void LogMessage(string level, string message, string? context = null)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var contextStr = context != null ? $"[{context}] " : "";
            var logEntry = $"{timestamp} {level} {contextStr}{message}";

            // Write to file
            File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);

            // Also write to debug output for development
            Debug.WriteLine($"[TaskMaster] {logEntry}");
        }
        catch
        {
            // Swallow logging errors to prevent cascading failures
        }
    }

    public static string GetLogFilePath() => LogFilePath;

    public static string GetRecentLogs(int lineCount = 100)
    {
        try
        {
            if (!File.Exists(LogFilePath))
                return "No log file found.";

            var lines = File.ReadAllLines(LogFilePath);
            var recentLines = lines.TakeLast(lineCount);
            return string.Join(Environment.NewLine, recentLines);
        }
        catch (Exception ex)
        {
            return $"Error reading log file: {ex.Message}";
        }
    }

    public static void ClearOldLogs(int daysToKeep = 7)
    {
        try
        {
            var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
            var logFiles = Directory.GetFiles(LogDirectory, "taskmaster-*.log");

            foreach (var file in logFiles)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.CreationTime < cutoffDate)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // Swallow cleanup errors
        }
    }
}