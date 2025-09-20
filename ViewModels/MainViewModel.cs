using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using TaskMaster.Models;
using TaskMaster.Services;
using TaskMaster.Views;

namespace TaskMaster.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly ClaudeService _claudeService;
    private readonly SpecFileService _specFileService;

    [ObservableProperty]
    private ObservableCollection<Project> _projects = new();

    [ObservableProperty]
    private Project? _selectedProject;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private string _claudeMdPath = string.Empty;

    [ObservableProperty]
    private string _type = "feature";

    [ObservableProperty]
    private string _scopePaths = "[]";

    [ObservableProperty]
    private string _acceptanceCriteria = "[]";

    [ObservableProperty]
    private string _testPlan = "[]";

    [ObservableProperty]
    private string _requiredDocs = "[]";

    [ObservableProperty]
    private string? _nonGoals;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _suggestedTasks;

    [ObservableProperty]
    private string? _nextSteps;

    [ObservableProperty]
    private string _markdownPreview = string.Empty;

    [ObservableProperty]
    private bool _isInferenceValid = false;

    [ObservableProperty]
    private bool _isInferenceStale = true;

    [ObservableProperty]
    private bool _isInferring = false;

    [ObservableProperty]
    private string _inferenceStatus = "Ready to infer";

    [ObservableProperty]
    private string _validationErrors = string.Empty;

    [ObservableProperty]
    private string _repoRoot = Environment.CurrentDirectory;

    [ObservableProperty]
    private string? _lastSavedSpecPath;

    [ObservableProperty]
    private int _panelRounds = 2;

    [ObservableProperty]
    private string _panelScope = "src/";

    private ClaudeInferenceResponse? _lastInference;

    public MainViewModel()
    {
        _databaseService = new DatabaseService();
        _databaseService.InitializeDatabase(); // Initialize database on startup
        _claudeService = new ClaudeService();
        _specFileService = new SpecFileService(_databaseService);

        PropertyChanged += OnPropertyChanged;
        LoadProjectsAsync();
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Title) || e.PropertyName == nameof(Summary) ||
            e.PropertyName == nameof(SelectedProject) || e.PropertyName == nameof(ClaudeMdPath))
        {
            IsInferenceStale = true;
            IsInferenceValid = false;
            ValidateInput();
            InferSpecCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName == nameof(ScopePaths) || e.PropertyName == nameof(AcceptanceCriteria) ||
            e.PropertyName == nameof(TestPlan) || e.PropertyName == nameof(RequiredDocs))
        {
            ValidateInferredFields();
        }

        if (e.PropertyName == nameof(IsInferenceValid) || e.PropertyName == nameof(IsInferring))
        {
            SaveSpecCommand.NotifyCanExecuteChanged();
            RunPanelCommand.NotifyCanExecuteChanged();
            RunUpdateCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName == nameof(IsInferring))
        {
            InferSpecCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName == nameof(LastSavedSpecPath) || e.PropertyName == nameof(ClaudeMdPath))
        {
            RunPanelCommand.NotifyCanExecuteChanged();
            RunUpdateCommand.NotifyCanExecuteChanged();
        }

        UpdateMarkdownPreview();
    }

    private async void LoadProjectsAsync()
    {
        try
        {
            var projects = await _databaseService.GetProjectsAsync();
            Projects.Clear();

            foreach (var project in projects)
            {
                Projects.Add(project);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load projects: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task CreateProjectAsync()
    {
        var dialog = new ProjectDialog();
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var project = await _databaseService.CreateProjectAsync(dialog.ProjectName, dialog.ClaudeMdPath);
                Projects.Add(project);
                SelectedProject = project;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create project: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void BrowseClaudeMdPath()
    {
        var selectedFile = FolderBrowserService.SelectClaudeMdFile();
        if (selectedFile != null)
        {
            ClaudeMdPath = selectedFile;

            // Update the selected project's ClaudeMdPath
            if (SelectedProject != null)
            {
                SelectedProject.ClaudeMdPath = ClaudeMdPath;
                UpdateProjectAsync();
            }
        }
    }

    [RelayCommand]
    private void BrowseRepoRoot()
    {
        var selectedPath = FolderBrowserService.SelectRepoRoot();
        if (selectedPath != null)
        {
            RepoRoot = selectedPath;
        }
    }

    [RelayCommand(CanExecute = nameof(CanInferSpec))]
    private async Task InferSpecAsync()
    {
        if (SelectedProject == null || string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Summary))
            return;

        IsInferring = true;
        InferenceStatus = "Checking Claude CLI availability...";

        LoggingService.LogInfo($"Starting inference for '{Title}' in project '{SelectedProject.Name}'", "MainViewModel");

        try
        {
            InferenceStatus = "Reading project context...";

            var claudeMdContent = string.Empty;
            if (!string.IsNullOrWhiteSpace(ClaudeMdPath) && File.Exists(ClaudeMdPath))
            {
                claudeMdContent = await File.ReadAllTextAsync(ClaudeMdPath);
                LoggingService.LogInfo($"Loaded CLAUDE.md content ({claudeMdContent.Length} characters)", "MainViewModel");
            }
            else if (!string.IsNullOrWhiteSpace(ClaudeMdPath))
            {
                LoggingService.LogWarning($"CLAUDE.md file not found at: {ClaudeMdPath}", "MainViewModel");
            }

            InferenceStatus = "Loading recent tasks...";
            var recentTasks = await _databaseService.GetRecentTasksAsync(SelectedProject.Id, 5);
            LoggingService.LogInfo($"Loaded {recentTasks.Count} recent tasks for context", "MainViewModel");

            var request = new ClaudeInferenceRequest
            {
                Title = Title,
                Summary = Summary,
                ProjectName = SelectedProject.Name,
                ClaudeMdContent = claudeMdContent,
                RecentTasks = recentTasks
            };

            InferenceStatus = "Calling Claude CLI...";
            _lastInference = await _claudeService.InferSpecFieldsAsync(request);

            if (_lastInference != null)
            {
                LoggingService.LogInfo("Successfully received inference response from Claude", "MainViewModel");

                InferenceStatus = "Processing response...";

                Type = _lastInference.Type;
                ScopePaths = JsonConvert.SerializeObject(_lastInference.ScopePaths, Formatting.Indented);
                AcceptanceCriteria = JsonConvert.SerializeObject(_lastInference.AcceptanceCriteria, Formatting.Indented);
                TestPlan = JsonConvert.SerializeObject(_lastInference.TestPlan, Formatting.Indented);
                RequiredDocs = JsonConvert.SerializeObject(_lastInference.RequiredDocs, Formatting.Indented);
                NonGoals = _lastInference.NonGoals;

                if (_lastInference.SuggestedTasks.Any())
                {
                    SuggestedTasks = JsonConvert.SerializeObject(_lastInference.SuggestedTasks, Formatting.Indented);
                }

                if (_lastInference.NextSteps.Any())
                {
                    NextSteps = JsonConvert.SerializeObject(_lastInference.NextSteps, Formatting.Indented);
                }

                IsInferenceValid = true;
                IsInferenceStale = false;
                InferenceStatus = "Inference completed successfully";

                LoggingService.LogInfo("Inference completed successfully", "MainViewModel");
            }
            else
            {
                var errorMsg = "Failed to get response from Claude CLI";
                InferenceStatus = errorMsg;
                LoggingService.LogError(errorMsg, null, "MainViewModel");
                ShowDetailedError("Claude CLI Error", errorMsg, "Check the logs for more details about the Claude CLI communication.");
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Claude CLI"))
        {
            var errorMsg = $"Claude CLI Error: {ex.Message}";
            InferenceStatus = errorMsg;
            LoggingService.LogError("Claude CLI not available or configured properly", ex, "MainViewModel");
            ShowDetailedError("Claude CLI Not Available", errorMsg,
                "Possible solutions:\n" +
                "1. Install Claude CLI: https://github.com/anthropics/claude-cli\n" +
                "2. Ensure Claude CLI is in your PATH\n" +
                "3. Run 'claude login' to authenticate\n" +
                "4. Check the logs for detailed error information");
        }
        catch (Exception ex)
        {
            var errorMsg = $"Unexpected error during inference: {ex.Message}";
            InferenceStatus = errorMsg;
            LoggingService.LogError("Unexpected error during inference", ex, "MainViewModel");
            ShowDetailedError("Inference Error", errorMsg, "Check the logs for detailed error information.");
        }
        finally
        {
            IsInferring = false;
        }
    }

    private bool CanInferSpec()
    {
        return SelectedProject != null &&
               !string.IsNullOrWhiteSpace(Title) &&
               !string.IsNullOrWhiteSpace(Summary) &&
               !IsInferring;
    }

    private void ValidateInput()
    {
        var errors = ValidationService.ValidateTaskSpecInput(Title, Summary, ClaudeMdPath);
        ValidationErrors = string.Join(Environment.NewLine, errors);
    }

    private void ValidateInferredFields()
    {
        if (!IsInferenceValid) return;

        var errors = ValidationService.ValidateInferredFields(ScopePaths, AcceptanceCriteria, TestPlan, RequiredDocs);

        if (errors.Any())
        {
            ValidationErrors = string.Join(Environment.NewLine, errors);
            IsInferenceValid = false;
        }
        else
        {
            ValidateInput(); // Re-validate base input
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveSpec))]
    private async Task SaveSpecAsync()
    {
        if (SelectedProject == null || !IsInferenceValid)
            return;

        try
        {
            var taskNumber = await _databaseService.GetNextTaskNumberAsync(SelectedProject.Id);
            var slug = SlugService.GenerateSlug(Title);

            var taskSpec = new TaskSpec
            {
                ProjectId = SelectedProject.Id,
                Number = taskNumber,
                Title = Title,
                Slug = slug,
                Type = Type,
                Summary = Summary,
                AcceptanceCriteria = AcceptanceCriteria,
                TestPlan = TestPlan,
                ScopePaths = ScopePaths,
                RequiredDocs = RequiredDocs,
                NonGoals = NonGoals,
                Notes = Notes,
                SuggestedTasks = SuggestedTasks,
                NextSteps = NextSteps
            };

            await _databaseService.SaveTaskSpecAsync(taskSpec);

            var filePath = await _specFileService.SaveSpecToFileAsync(taskSpec, SelectedProject, RepoRoot);
            LastSavedSpecPath = filePath;

            MessageBox.Show($"Spec saved successfully!\nFile: {filePath}\nTask: #{taskNumber}",
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            // Reset form
            ClearForm();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save spec: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanSaveSpec()
    {
        return SelectedProject != null && IsInferenceValid && !IsInferring;
    }

    [RelayCommand(CanExecute = nameof(CanRunPanel))]
    private async Task RunPanelAsync()
    {
        if (string.IsNullOrEmpty(LastSavedSpecPath))
            return;

        try
        {
            LoggingService.LogInfo("Starting panel execution", "MainViewModel");

            // Use the new PanelService instead of direct Claude commands
            var panelResult = await _claudeService.RunPanelServiceAsync(LastSavedSpecPath, RepoRoot, PanelRounds, PanelScope);

            if (panelResult.Success)
            {
                MessageBox.Show($"Panel completed successfully!\n\nDecision file: {panelResult.DecisionPath}\nSlug: {panelResult.Slug}",
                    "Panel Success", MessageBoxButton.OK, MessageBoxImage.Information);

                LoggingService.LogInfo($"Panel completed successfully for: {LastSavedSpecPath}", "MainViewModel");
            }
            else
            {
                MessageBox.Show($"Panel execution failed:\n\n{panelResult.ErrorMessage}",
                    "Panel Failed", MessageBoxButton.OK, MessageBoxImage.Error);

                LoggingService.LogError($"Panel failed: {panelResult.ErrorMessage}", null, "MainViewModel");
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Exception in RunPanelAsync", ex, "MainViewModel");
            MessageBox.Show($"Failed to run panel: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanRunPanel()
    {
        return !string.IsNullOrEmpty(LastSavedSpecPath) && File.Exists(LastSavedSpecPath) &&
               !string.IsNullOrEmpty(ClaudeMdPath) && File.Exists(ClaudeMdPath);
    }

    [RelayCommand(CanExecute = nameof(CanRunUpdate))]
    private async Task RunUpdateAsync()
    {
        if (string.IsNullOrEmpty(LastSavedSpecPath))
            return;

        try
        {
            var decisionPath = _specFileService.GetDecisionFilePath(LastSavedSpecPath);

            if (!File.Exists(decisionPath))
            {
                MessageBox.Show("Decision file not found. Please run the panel first.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var success = await _claudeService.RunUpdateAsync(LastSavedSpecPath, RepoRoot, ClaudeMdPath);

            if (success)
            {
                MessageBox.Show("Update completed successfully! Check your git repository for the new branch and PR.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Update command failed. Check Claude CLI output.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to run update: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanRunUpdate()
    {
        if (string.IsNullOrEmpty(LastSavedSpecPath) || !File.Exists(LastSavedSpecPath))
            return false;

        if (string.IsNullOrEmpty(ClaudeMdPath) || !File.Exists(ClaudeMdPath))
            return false;

        var decisionPath = _specFileService.GetDecisionFilePath(LastSavedSpecPath);
        return File.Exists(decisionPath);
    }

    [RelayCommand]
    private async Task ExportTemplateAsync()
    {
        try
        {
            LoggingService.LogInfo("Opening export template dialog", "MainViewModel");

            var templateService = new TemplateService(_databaseService);
            var viewModel = new TemplateExportViewModel(templateService, RepoRoot);
            var dialog = new Views.ExportTemplateDialog(viewModel);

            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Error opening export template dialog", ex, "MainViewModel");
            MessageBox.Show($"Failed to open export template dialog: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ExportCatalogAsync()
    {
        try
        {
            var projectsWithTasks = new List<Project>();

            foreach (var project in Projects)
            {
                var fullProject = await _databaseService.GetProjectByIdAsync(project.Id);
                if (fullProject != null)
                {
                    fullProject.Tasks = await _databaseService.GetRecentTasksAsync(project.Id, 1000);
                    projectsWithTasks.Add(fullProject);
                }
            }

            var json = await _specFileService.ExportCatalogAsync(projectsWithTasks);

            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                FileName = $"taskmaster-catalog-{DateTime.UtcNow:yyyyMMdd}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                await File.WriteAllTextAsync(dialog.FileName, json);
                MessageBox.Show("Catalog exported successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export catalog: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task SetupProjectStructureAsync()
    {
        if (SelectedProject == null)
        {
            MessageBox.Show("Please select a project first.", "No Project Selected",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var success = await ProjectTemplateService.GenerateProjectStructureAsync(RepoRoot, SelectedProject.Name);

            if (success)
            {
                MessageBox.Show($"Project structure generated successfully in:\n{RepoRoot}\n\n" +
                    "The following directories and files were created:\n" +
                    "- docs/specs/ (for task specifications)\n" +
                    "- docs/decisions/ (for design decisions)\n" +
                    "- .claude/ (slash commands)\n" +
                    "- CLAUDE.md (project instructions template)",
                    "Project Setup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Failed to generate project structure. Check the repo root path and permissions.",
                    "Setup Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to setup project structure: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ExportProjectTemplateAsync()
    {
        if (SelectedProject == null)
        {
            MessageBox.Show("Please select a project first.", "No Project Selected",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                FileName = $"{SelectedProject.Name}-template.json"
            };

            if (dialog.ShowDialog() == true)
            {
                await ProjectTemplateService.GenerateProjectExportAsync(SelectedProject, dialog.FileName);
                MessageBox.Show("Project template exported successfully!\n\n" +
                    "This file can be imported into other TaskMaster instances.",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export project template: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ImportProjectTemplateAsync()
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                Title = "Import Project Template"
            };

            if (dialog.ShowDialog() == true)
            {
                var importedProject = await ProjectTemplateService.ImportProjectAsync(dialog.FileName);
                if (importedProject != null)
                {
                    var newProject = await _databaseService.CreateProjectAsync(importedProject.Name, importedProject.ClaudeMdPath);
                    Projects.Add(newProject);
                    SelectedProject = newProject;

                    MessageBox.Show($"Project '{importedProject.Name}' imported successfully!",
                        "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to import project template: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void UpdateProjectAsync()
    {
        if (SelectedProject != null)
        {
            try
            {
                await _databaseService.UpdateProjectAsync(SelectedProject);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update project: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    partial void OnSelectedProjectChanged(Project? value)
    {
        if (value != null && !string.IsNullOrEmpty(value.ClaudeMdPath))
        {
            ClaudeMdPath = value.ClaudeMdPath;
        }
    }

    private void UpdateMarkdownPreview()
    {
        if (SelectedProject == null || !IsInferenceValid)
        {
            MarkdownPreview = "No preview available";
            return;
        }

        try
        {
            var taskSpec = new TaskSpec
            {
                Number = 999, // Placeholder
                Title = Title,
                Type = Type,
                Summary = Summary,
                AcceptanceCriteria = AcceptanceCriteria,
                TestPlan = TestPlan,
                ScopePaths = ScopePaths,
                RequiredDocs = RequiredDocs,
                NonGoals = NonGoals,
                Notes = Notes
            };

            MarkdownPreview = _specFileService.GenerateMarkdownSpecAsync(taskSpec, SelectedProject).Result;
        }
        catch
        {
            MarkdownPreview = "Error generating preview";
        }
    }

    private void ShowDetailedError(string title, string message, string solutions)
    {
        try
        {
            var errorDialog = new Views.ErrorReportDialog(title, message, solutions)
            {
                Owner = Application.Current.MainWindow
            };
            errorDialog.ShowDialog();
        }
        catch (Exception ex)
        {
            // Fallback to simple message box if error dialog fails
            MessageBox.Show($"{title}\n\n{message}\n\n{solutions}\n\nAdditional error: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ShowLogsCommand()
    {
        try
        {
            var logPath = LoggingService.GetLogFilePath();
            if (File.Exists(logPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = logPath,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("Log file not found.", "File Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open log file: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ClearForm()
    {
        Title = string.Empty;
        Summary = string.Empty;
        Type = "feature";
        ScopePaths = "[]";
        AcceptanceCriteria = "[]";
        TestPlan = "[]";
        RequiredDocs = "[]";
        NonGoals = null;
        Notes = null;
        SuggestedTasks = null;
        NextSteps = null;
        IsInferenceValid = false;
        IsInferenceStale = true;
        InferenceStatus = "Ready to infer";
        LastSavedSpecPath = null;
        _lastInference = null;
    }
}