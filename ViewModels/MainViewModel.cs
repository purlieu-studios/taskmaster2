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
    private readonly PanelService _panelService;

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

    // Catalog Export Settings
    [ObservableProperty]
    private bool _autoExportCatalogOnSave = true;

    [ObservableProperty]
    private bool _enablePerProjectCatalogs = false;

    [ObservableProperty]
    private string _catalogExportPath = "catalog";

    // New UI Properties
    [ObservableProperty]
    private string _typeFilter = "All";

    [ObservableProperty]
    private string _statusFilter = "All";

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private bool _isTaskDetailPanelOpen = false;

    [ObservableProperty]
    private TaskDetailViewModel? _selectedTaskDetail;

    private ClaudeInferenceResponse? _lastInference;

    public MainViewModel()
    {
        _databaseService = new DatabaseService();
        _databaseService.InitializeDatabase(); // Initialize database on startup
        _claudeService = new ClaudeService();
        _specFileService = new SpecFileService(_databaseService);
        _panelService = new PanelService(_claudeService, RepoRoot);

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

                // Update Summary with expanded version if available
                if (!string.IsNullOrWhiteSpace(_lastInference.ExpandedSummary))
                {
                    Summary = _lastInference.ExpandedSummary;
                }

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
                LoggingService.LogInfo("IsInferenceValid set to true after inference", "MainViewModel");

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

        LoggingService.LogInfo($"Validating inferred fields:", "MainViewModel");
        LoggingService.LogInfo($"  ScopePaths: {ScopePaths}", "MainViewModel");
        LoggingService.LogInfo($"  AcceptanceCriteria: {AcceptanceCriteria}", "MainViewModel");
        LoggingService.LogInfo($"  TestPlan: {TestPlan}", "MainViewModel");
        LoggingService.LogInfo($"  RequiredDocs: {RequiredDocs}", "MainViewModel");

        var errors = ValidationService.ValidateInferredFields(ScopePaths, AcceptanceCriteria, TestPlan, RequiredDocs);

        if (errors.Any())
        {
            LoggingService.LogInfo($"Validation errors found: {string.Join(", ", errors)}", "MainViewModel");
            ValidationErrors = string.Join(Environment.NewLine, errors);
            IsInferenceValid = false;
            LoggingService.LogInfo("IsInferenceValid set to false due to validation errors", "MainViewModel");
        }
        else
        {
            LoggingService.LogInfo("All inferred fields are valid", "MainViewModel");
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
            LoggingService.LogInfo($"Starting SaveSpecAsync - Project: {SelectedProject.Name} (ID: {SelectedProject.Id})", "MainViewModel");
            LoggingService.LogInfo($"Title: {Title}, Type: {Type}", "MainViewModel");

            var taskNumber = await _databaseService.GetNextTaskNumberAsync(SelectedProject.Id);
            LoggingService.LogInfo($"Got task number: {taskNumber}", "MainViewModel");

            var slug = SlugService.GenerateSlug(Title);
            LoggingService.LogInfo($"Generated slug: {slug}", "MainViewModel");

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

            LoggingService.LogInfo($"Created TaskSpec object for task #{taskNumber}", "MainViewModel");

            var savedTaskSpec = await _databaseService.SaveTaskSpecAsync(taskSpec);
            LoggingService.LogInfo($"SaveTaskSpecAsync completed - Task ID: {savedTaskSpec.Id}", "MainViewModel");

            var filePath = await _specFileService.SaveSpecToFileAsync(taskSpec, SelectedProject, RepoRoot);
            LastSavedSpecPath = filePath;

            // Auto-export catalog to Git repo if enabled and configured
            if (AutoExportCatalogOnSave && !string.IsNullOrWhiteSpace(RepoRoot) && Directory.Exists(RepoRoot))
            {
                try
                {
                    LoggingService.LogInfo("Auto-exporting catalog to Git repo", "MainViewModel");

                    // Get all projects with their tasks for catalog export
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

                    await _specFileService.SaveCatalogToRepoAsync(projectsWithTasks, RepoRoot, CatalogExportPath, EnablePerProjectCatalogs);
                    LoggingService.LogInfo("Catalog auto-exported successfully", "MainViewModel");
                }
                catch (Exception catalogEx)
                {
                    LoggingService.LogError("Failed to auto-export catalog", catalogEx, "MainViewModel");
                    // Don't fail the entire save operation for catalog export issues
                }
            }
            else if (!AutoExportCatalogOnSave)
            {
                LoggingService.LogInfo("Catalog auto-export is disabled", "MainViewModel");
            }

            LoggingService.LogInfo($"Spec file saved to: {filePath}", "MainViewModel");
            LoggingService.LogInfo($"LastSavedSpecPath set to: {LastSavedSpecPath}", "MainViewModel");

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
        var canSave = SelectedProject != null && IsInferenceValid && !IsInferring;
        LoggingService.LogInfo($"CanSaveSpec: {canSave} (SelectedProject: {SelectedProject != null}, IsInferenceValid: {IsInferenceValid}, IsInferring: {IsInferring})", "MainViewModel");
        return canSave;
    }

    [RelayCommand(CanExecute = nameof(CanRunPanel))]
    private async Task RunPanelAsync()
    {
        if (string.IsNullOrEmpty(LastSavedSpecPath))
            return;

        // Run prerequisite validation first
        if (!await ValidatePrerequisitesAsync())
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

        // Run prerequisite validation first
        if (!await ValidatePrerequisitesAsync())
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
                    "- catalog/ (Git-tracked catalog JSON snapshots)\n" +
                    "- .claude/ (slash commands)\n" +
                    "- CLAUDE.md (project instructions template)\n" +
                    "- catalog/README.md (catalog documentation)",
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

    [RelayCommand]
    private async Task ImportCatalogAsync()
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                Title = "Import TaskMaster Catalog",
                InitialDirectory = !string.IsNullOrWhiteSpace(RepoRoot) ? Path.Combine(RepoRoot, "catalog") : null
            };

            if (dialog.ShowDialog() == true)
            {
                LoggingService.LogInfo($"Importing catalog from: {dialog.FileName}", "MainViewModel");

                var catalogJson = await File.ReadAllTextAsync(dialog.FileName);
                var success = await _databaseService.ImportCatalogAsync(catalogJson);

                if (success)
                {
                    // Refresh the projects list
                    LoadProjectsAsync();

                    MessageBox.Show("Catalog imported successfully!\n\nProjects and tasks have been synchronized with your local database.",
                        "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                    LoggingService.LogInfo("Catalog import completed successfully", "MainViewModel");
                }
                else
                {
                    MessageBox.Show("Failed to import catalog. Please check the file format and try again.",
                        "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);

                    LoggingService.LogError("Catalog import failed", null, "MainViewModel");
                }
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Exception during catalog import", ex, "MainViewModel");
            MessageBox.Show($"Failed to import catalog: {ex.Message}", "Error",
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

    private async Task<bool> ValidatePrerequisitesAsync()
    {
        try
        {
            LoggingService.LogInfo("Running prerequisite validation", "MainViewModel");

            if (string.IsNullOrWhiteSpace(RepoRoot))
            {
                MessageBox.Show("Repository root is required for panel/update operations.",
                    "Missing Repository Root", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(ClaudeMdPath))
            {
                MessageBox.Show("CLAUDE.md path is required for panel/update operations.",
                    "Missing CLAUDE.md", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Run GuardrailsService preflight validation
            var preflightResult = await GuardrailsService.ValidatePreflightChecksAsync(
                LastSavedSpecPath ?? "", ClaudeMdPath, RepoRoot);

            if (!preflightResult.IsValid)
            {
                var errorMessage = "Prerequisites validation failed:\n\n" +
                    string.Join("\n", preflightResult.Errors);

                if (preflightResult.Warnings.Any())
                {
                    errorMessage += "\n\nWarnings:\n" + string.Join("\n", preflightResult.Warnings);
                }

                var result = MessageBox.Show(
                    errorMessage + "\n\nDo you want to continue anyway?",
                    "Prerequisites Check Failed",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    return false;
                }
            }

            LoggingService.LogInfo("Prerequisites validation completed", "MainViewModel");
            return true;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Error during prerequisites validation", ex, "MainViewModel");
            MessageBox.Show($"Prerequisites validation failed: {ex.Message}",
                "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    // New Commands for the redesigned UI
    [RelayCommand]
    private async Task NewTaskAsync()
    {
        try
        {
            var dialog = new NewTaskDialog();
            dialog.Projects.Clear();
            foreach (var project in Projects)
            {
                dialog.Projects.Add(project);
            }
            dialog.SelectedProject = SelectedProject;

            if (dialog.ShowDialog() == true && dialog.CreatedTask != null)
            {
                var newTask = dialog.CreatedTask;

                // Get the next task number for this project
                var taskNumber = await _databaseService.GetNextTaskNumberAsync(newTask.ProjectId);
                newTask.Number = taskNumber;

                // Save the task immediately
                var savedTask = await _databaseService.SaveTaskSpecAsync(newTask);

                LoggingService.LogInfo($"Created new task #{savedTask.Number} - {savedTask.Title}", "MainViewModel");

                // Open the task in the detail panel
                await OpenTaskDetailAsync(savedTask);

                // Refresh the task list if the project matches current selection
                if (SelectedProject?.Id == savedTask.ProjectId)
                {
                    // Notify task list to refresh
                    OnPropertyChanged(nameof(LastSavedSpecPath));
                }
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to create new task", ex, "MainViewModel");
            MessageBox.Show($"Failed to create new task: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ImportTemplateAsync()
    {
        try
        {
            MessageBox.Show("Import template functionality will be implemented later.", "Not Implemented",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to import template", ex, "MainViewModel");
            MessageBox.Show($"Failed to import template: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task OpenTaskDetailAsync(TaskSpec task)
    {
        try
        {
            if (SelectedTaskDetail == null)
            {
                SelectedTaskDetail = new TaskDetailViewModel(_databaseService, _claudeService, _panelService, _specFileService);
                SelectedTaskDetail.TaskSaved += OnTaskSaved;
            }

            SelectedTaskDetail.LoadTask(task);
            IsTaskDetailPanelOpen = true;

            LoggingService.LogInfo($"Opened task #{task.Number} in detail panel", "MainViewModel");
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to open task detail for #{task.Number}", ex, "MainViewModel");
        }
    }

    public void CloseTaskDetail()
    {
        IsTaskDetailPanelOpen = false;
        LoggingService.LogInfo("Closed task detail panel", "MainViewModel");
    }

    private void OnTaskSaved(object? sender, TaskSpec savedTask)
    {
        // Refresh the task list when a task is saved
        OnPropertyChanged(nameof(LastSavedSpecPath));
        LoggingService.LogInfo($"Task #{savedTask.Number} saved, refreshing task list", "MainViewModel");
    }
}