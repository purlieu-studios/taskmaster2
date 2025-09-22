using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TaskMaster.Services;

namespace TaskMaster.ViewModels;

/// <summary>
/// Enhanced base class for ViewModels that automatically logs property changes
/// This helps with debugging binding issues like the ProjectDirectory problem
/// </summary>
public abstract partial class EnhancedViewModelBase : ObservableObject
{
    private readonly string _viewModelName;

    protected EnhancedViewModelBase()
    {
        _viewModelName = GetType().Name;

        // Subscribe to our own property changes to log them
        PropertyChanged += OnPropertyChangedLogging;

        EnhancedLoggingService.LogDebug($"{_viewModelName} created", "ViewModel");
    }

    private void OnPropertyChangedLogging(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != null)
        {
            // Get the property value using reflection for logging
            try
            {
                var property = GetType().GetProperty(e.PropertyName);
                var value = property?.GetValue(this);
                var valueString = value?.ToString() ?? "null";

                // Don't log extremely verbose properties
                if (ShouldLogProperty(e.PropertyName))
                {
                    EnhancedLoggingService.LogPropertyChange(
                        e.PropertyName,
                        null, // We don't have the old value here, but the logging service will show the change
                        valueString,
                        _viewModelName);
                }
            }
            catch (Exception ex)
            {
                // Don't let logging issues break the app
                EnhancedLoggingService.LogWarning($"Failed to log property change for {e.PropertyName}: {ex.Message}", _viewModelName);
            }
        }
    }

    private static bool ShouldLogProperty(string propertyName)
    {
        // Skip logging very noisy properties
        var skipProperties = new[]
        {
            "MarkdownPreview", // Too verbose
            "IsLoading", // Too frequent
            "Progress" // Too frequent
        };

        return !skipProperties.Contains(propertyName);
    }

    /// <summary>
    /// Enhanced property setter that logs the old and new values
    /// </summary>
    protected bool SetPropertyWithLogging<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        var oldValue = field;
        field = value;

        if (propertyName != null && ShouldLogProperty(propertyName))
        {
            EnhancedLoggingService.LogPropertyChange(
                propertyName,
                oldValue,
                value,
                _viewModelName);
        }

        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Log a command execution
    /// </summary>
    protected void LogCommandExecution(string commandName, object? parameter = null)
    {
        EnhancedLoggingService.LogCommandExecution(commandName, parameter, _viewModelName);
    }

    /// <summary>
    /// Log a performance-sensitive operation
    /// </summary>
    protected void LogPerformance(string operation, TimeSpan duration, object? additionalData = null)
    {
        EnhancedLoggingService.LogPerformance(operation, duration, _viewModelName, additionalData);
    }

    /// <summary>
    /// Log a binding issue that occurred
    /// </summary>
    protected void LogBindingIssue(string bindingPath, string target, string issue)
    {
        EnhancedLoggingService.LogBindingIssue(bindingPath, target, issue, _viewModelName);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        // Special handling for critical properties that often have binding issues
        if (e.PropertyName != null && IsCriticalProperty(e.PropertyName))
        {
            var propertyValue = GetType().GetProperty(e.PropertyName)?.GetValue(this);
            EnhancedLoggingService.LogDebug(
                $"Critical property changed: {e.PropertyName} = {propertyValue}",
                $"{_viewModelName}.CriticalBinding");
        }
    }

    private static bool IsCriticalProperty(string propertyName)
    {
        // Properties that commonly have binding issues
        var criticalProperties = new[]
        {
            "SelectedProject",
            "SelectedProjectDirectory",
            "ClaudeMdPath",
            "Title",
            "Summary",
            "IsInferenceValid",
            "IsInferenceStale"
        };

        return criticalProperties.Contains(propertyName);
    }

    /// <summary>
    /// Clean up the ViewModel when it's disposed
    /// </summary>
    public void Dispose()
    {
        PropertyChanged -= OnPropertyChangedLogging;
        EnhancedLoggingService.LogDebug($"{_viewModelName} disposed", "ViewModel");
    }
}