using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TaskMaster.Services;

namespace TaskMaster.Views;

public partial class LogViewerWindow : Window
{
    private readonly ObservableCollection<LogEntry> _displayedEntries = new();
    private readonly List<LogEntry> _allEntries = new();
    private readonly DispatcherTimer _refreshTimer;
    private bool _autoScrollToBottom = true;

    public LogViewerWindow()
    {
        InitializeComponent();

        LogEntriesListView.ItemsSource = _displayedEntries;

        // Setup auto-refresh timer
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;

        // Load initial data
        LoadLogEntries();
        PopulateContextFilter();
        UpdateLogCount();

        // Start auto-refresh if enabled
        if (AutoRefreshCheckBox.IsChecked == true)
        {
            _refreshTimer.Start();
        }

        // Log that the viewer was opened
        EnhancedLoggingService.LogInfo("Log Viewer window opened", "LogViewer");
    }

    private void LoadLogEntries()
    {
        try
        {
            _allEntries.Clear();
            _allEntries.AddRange(EnhancedLoggingService.GetRecentLogEntries(1000));
            ApplyFilters();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading log entries: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ApplyFilters()
    {
        var filteredEntries = _allEntries.AsEnumerable();

        // Filter by log levels
        var enabledLevels = new List<string>();
        if (TraceCheckBox.IsChecked == true) enabledLevels.Add("Verbose");
        if (DebugCheckBox.IsChecked == true) enabledLevels.Add("Debug");
        if (InfoCheckBox.IsChecked == true) enabledLevels.Add("Information");
        if (WarningCheckBox.IsChecked == true) enabledLevels.Add("Warning");
        if (ErrorCheckBox.IsChecked == true) enabledLevels.Add("Error");
        if (CriticalCheckBox.IsChecked == true) enabledLevels.Add("Fatal");

        if (enabledLevels.Count > 0)
        {
            filteredEntries = filteredEntries.Where(e => enabledLevels.Contains(e.Level));
        }

        // Filter by context
        if (ContextComboBox.SelectedItem is ComboBoxItem contextItem &&
            contextItem.Content.ToString() != "All Contexts")
        {
            var selectedContext = contextItem.Content.ToString();
            filteredEntries = filteredEntries.Where(e => e.Context == selectedContext);
        }

        // Filter by search text
        var searchText = SearchTextBox.Text?.Trim();
        if (!string.IsNullOrEmpty(searchText))
        {
            filteredEntries = filteredEntries.Where(e =>
                e.Message.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                (e.Context?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true));
        }

        // Update the displayed entries
        _displayedEntries.Clear();
        foreach (var entry in filteredEntries.OrderBy(e => e.Timestamp))
        {
            _displayedEntries.Add(entry);
        }

        UpdateLogCount();

        // Auto-scroll to bottom if enabled
        if (_autoScrollToBottom && _displayedEntries.Count > 0)
        {
            LogEntriesListView.ScrollIntoView(_displayedEntries.Last());
        }
    }

    private void PopulateContextFilter()
    {
        var contexts = _allEntries
            .Where(e => !string.IsNullOrEmpty(e.Context))
            .Select(e => e.Context!)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        ContextComboBox.Items.Clear();
        ContextComboBox.Items.Add(new ComboBoxItem { Content = "All Contexts" });

        foreach (var context in contexts)
        {
            ContextComboBox.Items.Add(new ComboBoxItem { Content = context });
        }

        ContextComboBox.SelectedIndex = 0;
    }

    private void UpdateLogCount()
    {
        LogCountText.Text = $"({_displayedEntries.Count} of {_allEntries.Count} entries)";
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        LoadLogEntries();
    }

    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        ApplyFilters();
    }

    private void SearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void AutoRefreshChanged(object sender, RoutedEventArgs e)
    {
        if (AutoRefreshCheckBox.IsChecked == true)
        {
            _refreshTimer.Start();
            EnhancedLoggingService.LogDebug("Auto-refresh enabled", "LogViewer");
        }
        else
        {
            _refreshTimer.Stop();
            EnhancedLoggingService.LogDebug("Auto-refresh disabled", "LogViewer");
        }
    }

    private void LogEntrySelected(object sender, SelectionChangedEventArgs e)
    {
        if (LogEntriesListView.SelectedItem is LogEntry selectedEntry)
        {
            DisplayEntryDetails(selectedEntry);
        }
    }

    private void DisplayEntryDetails(LogEntry entry)
    {
        var details = new StringBuilder();
        details.AppendLine($"Timestamp: {entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC");
        details.AppendLine($"Level: {entry.Level}");
        details.AppendLine($"Context: {entry.Context ?? "None"}");
        details.AppendLine();
        details.AppendLine("Message:");
        details.AppendLine(entry.Message);

        if (entry.Data != null)
        {
            details.AppendLine();
            details.AppendLine("Data:");
            try
            {
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                var jsonData = JsonSerializer.Serialize(entry.Data, jsonOptions);
                details.AppendLine(jsonData);
            }
            catch
            {
                details.AppendLine(entry.Data.ToString());
            }
        }

        if (entry.Exception != null)
        {
            details.AppendLine();
            details.AppendLine("Exception:");
            details.AppendLine($"Type: {entry.Exception.GetType().Name}");
            details.AppendLine($"Message: {entry.Exception.Message}");
            details.AppendLine();
            details.AppendLine("Stack Trace:");
            details.AppendLine(entry.Exception.StackTrace);

            if (entry.Exception.InnerException != null)
            {
                details.AppendLine();
                details.AppendLine("Inner Exception:");
                details.AppendLine($"Type: {entry.Exception.InnerException.GetType().Name}");
                details.AppendLine($"Message: {entry.Exception.InnerException.Message}");
            }
        }

        DetailsTextBox.Text = details.ToString();
    }

    private void RefreshClicked(object sender, RoutedEventArgs e)
    {
        LoadLogEntries();
        PopulateContextFilter();
        EnhancedLoggingService.LogInfo("Log entries refreshed manually", "LogViewer");
    }

    private void CopyEntryClicked(object sender, RoutedEventArgs e)
    {
        if (LogEntriesListView.SelectedItem is LogEntry selectedEntry)
        {
            try
            {
                var entryText = $"{selectedEntry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{selectedEntry.Level}] [{selectedEntry.Context}] {selectedEntry.Message}";
                if (selectedEntry.Exception != null)
                {
                    entryText += $"\nException: {selectedEntry.Exception}";
                }

                Clipboard.SetText(entryText);
                MessageBox.Show("Log entry copied to clipboard!", "Copied",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                EnhancedLoggingService.LogDebug("Single log entry copied to clipboard", "LogViewer");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        else
        {
            MessageBox.Show("Please select a log entry to copy.", "No Selection",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void CopyAllClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var allText = new StringBuilder();
            foreach (var entry in _displayedEntries)
            {
                allText.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Level}] [{entry.Context}] {entry.Message}");
                if (entry.Exception != null)
                {
                    allText.AppendLine($"  Exception: {entry.Exception.Message}");
                }
            }

            Clipboard.SetText(allText.ToString());
            MessageBox.Show($"All {_displayedEntries.Count} displayed log entries copied to clipboard!", "Copied",
                MessageBoxButton.OK, MessageBoxImage.Information);

            EnhancedLoggingService.LogInfo($"All {_displayedEntries.Count} displayed log entries copied to clipboard", "LogViewer");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ExportLogsClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|Text files (*.txt)|*.txt|CSV files (*.csv)|*.csv",
                DefaultExt = "json",
                FileName = $"TaskMaster-Logs-{DateTime.Now:yyyyMMdd-HHmmss}"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var extension = Path.GetExtension(saveDialog.FileName).ToLower();

                switch (extension)
                {
                    case ".json":
                        await ExportAsJsonAsync(saveDialog.FileName);
                        break;
                    case ".txt":
                        await ExportAsTextAsync(saveDialog.FileName);
                        break;
                    case ".csv":
                        await ExportAsCsvAsync(saveDialog.FileName);
                        break;
                }

                MessageBox.Show($"Logs exported successfully to:\n{saveDialog.FileName}", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                EnhancedLoggingService.LogInfo($"Logs exported to {saveDialog.FileName}", "LogViewer");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export logs: {ex.Message}", "Export Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ExportAsJsonAsync(string filePath)
    {
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(_displayedEntries, jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    private async Task ExportAsTextAsync(string filePath)
    {
        var text = new StringBuilder();
        text.AppendLine($"TaskMaster Log Export - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        text.AppendLine($"Total Entries: {_displayedEntries.Count}");
        text.AppendLine(new string('=', 80));
        text.AppendLine();

        foreach (var entry in _displayedEntries)
        {
            text.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Level}] [{entry.Context}] {entry.Message}");
            if (entry.Exception != null)
            {
                text.AppendLine($"  Exception: {entry.Exception}");
            }
            text.AppendLine();
        }

        await File.WriteAllTextAsync(filePath, text.ToString());
    }

    private async Task ExportAsCsvAsync(string filePath)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Timestamp,Level,Context,Message,HasException");

        foreach (var entry in _displayedEntries)
        {
            var message = entry.Message.Replace("\"", "\"\"").Replace("\r\n", " ").Replace("\n", " ");
            csv.AppendLine($"\"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\",\"{entry.Level}\",\"{entry.Context}\",\"{message}\",{entry.Exception != null}");
        }

        await File.WriteAllTextAsync(filePath, csv.ToString());
    }

    private async void ExportDebugPackageClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false;
                button.Content = "🔄 Creating Package...";
            }

            var packagePath = await EnhancedLoggingService.ExportDebugPackageAsync();

            MessageBox.Show($"Debug package created successfully!\n\nPath: {packagePath}\n\nThis package contains:\n• All log files\n• System information\n• Database snapshot\n• Recent log entries\n\nYou can now share this file for debugging assistance.",
                "Debug Package Created", MessageBoxButton.OK, MessageBoxImage.Information);

            // Offer to open the folder
            var result = MessageBox.Show("Would you like to open the folder containing the debug package?",
                "Open Folder?", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{packagePath}\"");
            }

            EnhancedLoggingService.LogInfo($"Debug package created: {packagePath}", "LogViewer");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create debug package: {ex.Message}", "Export Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = true;
                button.Content = "📦 Debug Package";
            }
        }
    }

    private void ClearClicked(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Are you sure you want to clear all displayed log entries?\n\nThis will only clear the display, not the actual log files.",
            "Confirm Clear", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _displayedEntries.Clear();
            DetailsTextBox.Clear();
            UpdateLogCount();

            EnhancedLoggingService.LogInfo("Log viewer display cleared", "LogViewer");
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _refreshTimer.Stop();
        EnhancedLoggingService.LogInfo("Log Viewer window closed", "LogViewer");
        base.OnClosing(e);
    }
}