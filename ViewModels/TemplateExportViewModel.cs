using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using TaskMaster.Services;

namespace TaskMaster.ViewModels;

public partial class TemplateExportViewModel : ObservableObject
{
    private readonly TemplateService _templateService;
    private readonly string _projectPath;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _templateName = "My TaskMaster Template";

    [ObservableProperty]
    private string _outputPath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _filesToInclude = new();

    [ObservableProperty]
    private bool _isExporting = false;

    [ObservableProperty]
    private string _progressStage = string.Empty;

    [ObservableProperty]
    private int _progressPercent = 0;

    [ObservableProperty]
    private string _currentFile = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private Brush _statusColor = Brushes.Black;

    [ObservableProperty]
    private bool _canExport = false;

    [ObservableProperty]
    private bool _canRefresh = true;

    public string FileCountText => $"Files to include: {FilesToInclude.Count}";

    public TemplateExportViewModel(TemplateService templateService, string projectPath)
    {
        _templateService = templateService;
        _projectPath = projectPath;

        // Set default output path
        var defaultFileName = $"{TemplateName.Replace(" ", "-")}-{DateTime.Now:yyyyMMdd}.zip";
        OutputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), defaultFileName);

        // Load initial preview
        _ = RefreshPreviewAsync();
    }

    [RelayCommand]
    private void BrowseOutputPath()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Template As",
            Filter = "Zip files (*.zip)|*.zip|All files (*.*)|*.*",
            DefaultExt = ".zip",
            FileName = Path.GetFileName(OutputPath)
        };

        if (dialog.ShowDialog() == true)
        {
            OutputPath = dialog.FileName;
        }
    }

    [RelayCommand]
    private async Task RefreshPreviewAsync()
    {
        try
        {
            CanRefresh = false;
            StatusMessage = "Loading file preview...";
            StatusColor = Brushes.Blue;

            var files = await _templateService.PreviewFilesAsync(_projectPath);

            FilesToInclude.Clear();
            foreach (var file in files.OrderBy(f => f))
            {
                FilesToInclude.Add(file);
            }

            OnPropertyChanged(nameof(FileCountText));

            StatusMessage = "Preview loaded successfully";
            StatusColor = Brushes.Green;
            CanExport = !string.IsNullOrWhiteSpace(TemplateName) && !string.IsNullOrWhiteSpace(OutputPath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading preview: {ex.Message}";
            StatusColor = Brushes.Red;
            LoggingService.LogError("Error refreshing export preview", ex, "TemplateExportViewModel");
        }
        finally
        {
            CanRefresh = true;
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (string.IsNullOrWhiteSpace(TemplateName) || string.IsNullOrWhiteSpace(OutputPath))
        {
            StatusMessage = "Please provide template name and output path";
            StatusColor = Brushes.Red;
            return;
        }

        try
        {
            IsExporting = true;
            CanExport = false;
            CanRefresh = false;
            StatusMessage = "Exporting template...";
            StatusColor = Brushes.Blue;

            _cancellationTokenSource = new CancellationTokenSource();

            var progress = new Progress<ExportProgress>(OnProgressChanged);

            var resultPath = await _templateService.ExportProjectTemplateAsync(
                _projectPath,
                OutputPath,
                TemplateName,
                progress,
                _cancellationTokenSource.Token);

            StatusMessage = $"Export completed successfully: {resultPath}";
            StatusColor = Brushes.Green;

            // Ask user if they want to open the output folder
            var result = MessageBox.Show(
                $"Template exported successfully!\n\nPath: {resultPath}\n\nWould you like to open the output folder?",
                "Export Complete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                var folderPath = Path.GetDirectoryName(resultPath);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", folderPath);
                }
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Export cancelled by user";
            StatusColor = Brushes.Orange;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            StatusColor = Brushes.Red;
            LoggingService.LogError("Template export failed", ex, "TemplateExportViewModel");

            MessageBox.Show(
                $"Export failed: {ex.Message}",
                "Export Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsExporting = false;
            CanExport = true;
            CanRefresh = true;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            // Reset progress
            ProgressStage = string.Empty;
            ProgressPercent = 0;
            CurrentFile = string.Empty;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsExporting)
        {
            _cancellationTokenSource?.Cancel();
        }
        else
        {
            // Close dialog
            Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(w => w.DataContext == this)?.Close();
        }
    }

    private void OnProgressChanged(ExportProgress progress)
    {
        ProgressStage = progress.Stage;
        ProgressPercent = progress.PercentComplete;
        CurrentFile = progress.CurrentFile;
    }

    partial void OnTemplateNameChanged(string value)
    {
        // Update output path when template name changes
        if (!string.IsNullOrWhiteSpace(value))
        {
            var fileName = $"{value.Replace(" ", "-")}-{DateTime.Now:yyyyMMdd}.zip";
            var directory = Path.GetDirectoryName(OutputPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            OutputPath = Path.Combine(directory, fileName);
        }

        CanExport = !string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(OutputPath);
    }

    partial void OnOutputPathChanged(string value)
    {
        CanExport = !string.IsNullOrWhiteSpace(TemplateName) && !string.IsNullOrWhiteSpace(value);
    }
}