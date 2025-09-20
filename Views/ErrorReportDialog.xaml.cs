using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using TaskMaster.Services;

namespace TaskMaster.Views;

public partial class ErrorReportDialog : Window
{
    public string ErrorTitle { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public string Solutions { get; set; } = "";
    public string RecentLogs { get; set; } = "";
    public string SystemInfo { get; set; } = "";

    public ErrorReportDialog(string title, string message, string solutions)
    {
        InitializeComponent();

        ErrorTitle = title;
        ErrorMessage = message;
        Solutions = solutions;

        LoadSystemInfo();
        LoadRecentLogs();

        DataContext = this;
    }

    private void LoadSystemInfo()
    {
        try
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine($"OS: {Environment.OSVersion}");
            info.AppendLine($"Machine: {Environment.MachineName}");
            info.AppendLine($"User: {Environment.UserName}");
            info.AppendLine($"Current Directory: {Environment.CurrentDirectory}");
            info.AppendLine($".NET Version: {Environment.Version}");
            info.AppendLine($"PATH: {Environment.GetEnvironmentVariable("PATH")}");

            // Check Claude CLI
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
                    if (claudeCheck.ExitCode == 0)
                    {
                        var version = claudeCheck.StandardOutput.ReadToEnd().Trim();
                        info.AppendLine($"Claude CLI: Available ({version})");
                    }
                    else
                    {
                        var error = claudeCheck.StandardError.ReadToEnd().Trim();
                        info.AppendLine($"Claude CLI: Error (Exit code {claudeCheck.ExitCode}: {error})");
                    }
                }
            }
            catch (Exception ex)
            {
                info.AppendLine($"Claude CLI: Not found or error ({ex.Message})");
            }

            SystemInfo = info.ToString();
        }
        catch (Exception ex)
        {
            SystemInfo = $"Error loading system info: {ex.Message}";
        }
    }

    private void LoadRecentLogs()
    {
        try
        {
            RecentLogs = LoggingService.GetRecentLogs(50);
        }
        catch (Exception ex)
        {
            RecentLogs = $"Error loading logs: {ex.Message}";
        }
    }

    private void RefreshLogs_Click(object sender, RoutedEventArgs e)
    {
        LoadRecentLogs();
        // Trigger property change notification if using INotifyPropertyChanged
    }

    private void CopyError_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var errorReport = $"Error: {ErrorTitle}\n\n" +
                             $"Message: {ErrorMessage}\n\n" +
                             $"Solutions:\n{Solutions}\n\n" +
                             $"System Info:\n{SystemInfo}\n\n" +
                             $"Recent Logs:\n{RecentLogs}";

            Clipboard.SetText(errorReport);
            MessageBox.Show("Error details copied to clipboard!", "Copied",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenLogFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logPath = LoggingService.GetLogFilePath();
            if (File.Exists(logPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = logPath,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("Log file not found.", "File Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open log file: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void TestClaude_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false;
                button.Content = "Testing...";
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    MessageBox.Show($"Claude CLI is working!\n\nVersion: {output.Trim()}", "Test Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Claude CLI test failed.\n\nExit Code: {process.ExitCode}\nError: {error}",
                        "Test Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            if (button != null)
            {
                button.IsEnabled = true;
                button.Content = "Test Claude CLI";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to test Claude CLI: {ex.Message}", "Test Error",
                MessageBoxButton.OK, MessageBoxImage.Error);

            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = true;
                button.Content = "Test Claude CLI";
            }
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}