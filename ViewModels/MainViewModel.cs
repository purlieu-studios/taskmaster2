using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using TaskMaster.Models;
using TaskMaster.Services;
using TaskMaster.Views;

namespace TaskMaster.ViewModels;

public partial class MainViewModel : EnhancedViewModelBase
{
    private readonly DatabaseService _databaseService;
    private readonly ClaudeService _claudeService;
    private readonly SpecFileService _specFileService;
    private readonly PanelService _panelService;
    private readonly TaskDecompositionService _taskDecompositionService;
    private readonly ProjectContextService _projectContextService;

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
    private bool _isNewTaskPopupVisible = false;

    [ObservableProperty]
    private NewTaskViewModel? _newTaskViewModel;

    [ObservableProperty]
    private string _panelScope = "src/";

    // Task Decomposition Properties
    [ObservableProperty]
    private TaskDecompositionSuggestion? _decompositionSuggestion;

    [ObservableProperty]
    private bool _showDecompositionSuggestion = false;

    [ObservableProperty]
    private bool _isAnalyzingComplexity = false;

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

    // Project Settings Panel Properties
    [ObservableProperty]
    private bool _isProjectSettingsPanelOpen = false;

    [ObservableProperty]
    private int _filesAnalyzedCount = 0;

    [ObservableProperty]
    private DateTime? _lastAnalysisDate;

    // Proxy property for project directory binding
    public string? SelectedProjectDirectory
    {
        get => SelectedProject?.ProjectDirectory;
        set
        {
            if (SelectedProject != null && SelectedProject.ProjectDirectory != value)
            {
                var oldValue = SelectedProject.ProjectDirectory;
                SelectedProject.ProjectDirectory = value;

                // Enhanced logging for this critical binding property
                EnhancedLoggingService.LogPropertyChange(
                    nameof(SelectedProjectDirectory),
                    oldValue,
                    value,
                    "MainViewModel.ProxyBinding");

                OnPropertyChanged(nameof(SelectedProjectDirectory));
            }
        }
    }

    private ClaudeInferenceResponse? _lastInference;
    private Project? _subscribedProject; // Track project for PropertyChanged subscription

    public MainViewModel()
    {
        _databaseService = new DatabaseService();
        _databaseService.InitializeDatabase(); // Initialize database on startup
        _claudeService = new ClaudeService();
        _specFileService = new SpecFileService(_databaseService);
        _panelService = new PanelService(_claudeService, RepoRoot);
        _taskDecompositionService = new TaskDecompositionService(_claudeService, _databaseService);
        _projectContextService = new ProjectContextService(_databaseService);

        PropertyChanged += OnPropertyChanged;
        LoadProjectsAsync();
        _ = CalculateExistingTaskComplexityAsync(); // Calculate complexity for existing tasks
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

            // Auto-select the first project if any exist and none is currently selected
            if (Projects.Any() && SelectedProject == null)
            {
                SelectedProject = Projects.First();
                LoggingService.LogInfo($"Auto-selected first project: {SelectedProject.Name}", "MainViewModel");
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
                var project = await _databaseService.CreateProjectAsync(dialog.ProjectName, dialog.ProjectDirectory);
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
                _ = UpdateProjectAsync(); // Fire and forget - update in background
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
            _lastInference = await _claudeService.InferSpecFieldsAsync(request, CancellationToken.None);

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

            // Calculate complexity score
            var complexityScore = _taskDecompositionService.CalculateComplexityScore(taskSpec);
            taskSpec.ComplexityScore = complexityScore;
            LoggingService.LogInfo($"Calculated complexity score: {complexityScore}", "MainViewModel");

            var savedTaskSpec = await _databaseService.SaveTaskSpecAsync(taskSpec);
            LoggingService.LogInfo($"SaveTaskSpecAsync completed - Task ID: {savedTaskSpec.Id}", "MainViewModel");

            // Check if task should be decomposed
            await CheckAndOfferDecompositionAsync(savedTaskSpec);

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

    private async Task UpdateProjectAsync()
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
        // Unsubscribe from old project
        if (_subscribedProject != null)
        {
            _subscribedProject.PropertyChanged -= OnProjectPropertyChanged;
        }

        // Subscribe to new project
        if (value != null)
        {
            value.PropertyChanged += OnProjectPropertyChanged;

            if (!string.IsNullOrEmpty(value.ClaudeMdPath))
            {
                ClaudeMdPath = value.ClaudeMdPath;
            }
        }

        _subscribedProject = value;

        // Notify proxy property when SelectedProject changes
        OnPropertyChanged(nameof(SelectedProjectDirectory));
    }

    private void OnProjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Project.ProjectDirectory))
        {
            // Re-notify the proxy property when the underlying Project's ProjectDirectory changes
            OnPropertyChanged(nameof(SelectedProjectDirectory));
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
        LogCommandExecution(nameof(ShowLogsCommand));

        try
        {
            var logViewerWindow = new LogViewerWindow
            {
                Owner = Application.Current.MainWindow
            };

            EnhancedLoggingService.LogInfo("Opening enhanced log viewer window", "MainViewModel");
            logViewerWindow.Show();
        }
        catch (Exception ex)
        {
            EnhancedLoggingService.LogError("Failed to open log viewer window", ex, "MainViewModel");
            MessageBox.Show($"Failed to open log viewer: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private async Task ExportDebugPackageCommand()
    {
        LogCommandExecution(nameof(ExportDebugPackageCommand));

        try
        {
            var packagePath = await EnhancedLoggingService.ExportDebugPackageAsync();

            MessageBox.Show($"Debug package created successfully!\n\nPath: {packagePath}\n\nThis package contains all logs, system information, and database snapshot for debugging assistance.",
                "Debug Package Created", MessageBoxButton.OK, MessageBoxImage.Information);

            // Offer to open the folder
            var result = MessageBox.Show("Would you like to open the folder containing the debug package?",
                "Open Folder?", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{packagePath}\"");
            }

            EnhancedLoggingService.LogInfo($"Debug package exported: {packagePath}", "MainViewModel");
        }
        catch (Exception ex)
        {
            EnhancedLoggingService.LogError("Failed to export debug package", ex, "MainViewModel");
            MessageBox.Show($"Failed to export debug package: {ex.Message}", "Export Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
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
    private void ShowNewTaskPopup()
    {
        if (NewTaskViewModel == null)
        {
            NewTaskViewModel = new NewTaskViewModel();
        }

        // Initialize popup with current projects
        NewTaskViewModel.Projects.Clear();
        foreach (var project in Projects)
        {
            NewTaskViewModel.Projects.Add(project);
        }
        NewTaskViewModel.SelectedProject = SelectedProject;
        NewTaskViewModel.Reset();

        IsNewTaskPopupVisible = true;
    }

    [RelayCommand]
    private void CloseNewTaskPopup()
    {
        IsNewTaskPopupVisible = false;
    }

    [RelayCommand]
    private async Task CreateTaskFromPopupAsync()
    {
        if (NewTaskViewModel == null || !NewTaskViewModel.CanCreateTask)
            return;

        try
        {
            TaskSpec newTask;

            // Check if AI enhancement is enabled
            if (NewTaskViewModel.UseAIEnhancement)
            {
                // Set loading state and get cancellation token
                NewTaskViewModel.IsGenerating = true;
                var cancellationToken = NewTaskViewModel.GetCancellationToken();

                try
                {
                    LoggingService.LogInfo("Starting AI-enhanced task creation", "MainViewModel");

                    // Create basic task spec for AI enhancement
                    var basicTask = NewTaskViewModel.CreateTaskSpec();

                    // Use ClaudeService to enhance the task with AI-generated content
                    var enhancedTask = await _claudeService.EnhanceTaskSpecAsync(
                        basicTask.Title,
                        basicTask.Summary,
                        basicTask.Type,
                        SelectedProject?.Name ?? "Unknown Project",
                        cancellationToken
                    );

                    // Check if operation was cancelled
                    if (cancellationToken.IsCancellationRequested)
                    {
                        LoggingService.LogInfo("AI enhancement was cancelled", "MainViewModel");
                        return;
                    }

                    // Merge enhanced content with basic task
                    newTask = basicTask;
                    newTask.AcceptanceCriteria = enhancedTask.AcceptanceCriteria ?? "[]";
                    newTask.TestPlan = enhancedTask.TestPlan ?? "[]";
                    newTask.ScopePaths = enhancedTask.ScopePaths ?? "[]";
                    newTask.RequiredDocs = enhancedTask.RequiredDocs ?? "[]";
                    newTask.NonGoals = enhancedTask.NonGoals ?? "[]";
                    newTask.Notes = enhancedTask.Notes ?? "[]";

                    LoggingService.LogInfo("AI enhancement completed successfully", "MainViewModel");
                }
                catch (OperationCanceledException)
                {
                    LoggingService.LogInfo("AI enhancement was cancelled by user", "MainViewModel");
                    return;
                }
                catch (Exception aiEx)
                {
                    LoggingService.LogWarning($"AI enhancement failed, proceeding with basic task: {aiEx.Message}", "MainViewModel");

                    // Show user-friendly notification about AI failure
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"AI enhancement is unavailable: {aiEx.Message}\n\nCreating basic task instead.",
                            "AI Enhancement Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });

                    // Fall back to basic task if AI enhancement fails
                    newTask = NewTaskViewModel.CreateTaskSpec();
                }
                finally
                {
                    // Clear loading state
                    NewTaskViewModel.IsGenerating = false;
                }
            }
            else
            {
                // Create basic task without AI enhancement
                newTask = NewTaskViewModel.CreateTaskSpec();
            }

            // Get the next task number for this project
            var taskNumber = await _databaseService.GetNextTaskNumberAsync(newTask.ProjectId);
            newTask.Number = taskNumber;

            // Calculate complexity score and check for decomposition
            var complexityScore = _taskDecompositionService.CalculateComplexityScore(newTask);
            newTask.ComplexityScore = complexityScore;

            // Save the task immediately
            var savedTask = await _databaseService.SaveTaskSpecAsync(newTask);

            LoggingService.LogInfo($"Created new task #{savedTask.Number} - {savedTask.Title} (Complexity: {complexityScore})", "MainViewModel");

            // Check if task should be decomposed
            await CheckAndOfferDecompositionAsync(savedTask);

            // Close the popup only if not showing decomposition suggestion
            if (!ShowDecompositionSuggestion)
            {
                IsNewTaskPopupVisible = false;
            }

            // Open the task in the detail panel
            await OpenTaskDetailAsync(savedTask);

            // Refresh the task list if the project matches current selection
            if (SelectedProject?.Id == savedTask.ProjectId)
            {
                // Notify task list to refresh
                OnPropertyChanged(nameof(LastSavedSpecPath));
            }
        }
        catch (Exception ex)
        {
            // Ensure loading state is cleared on any error
            if (NewTaskViewModel != null)
                NewTaskViewModel.IsGenerating = false;

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
                SelectedTaskDetail = new TaskDetailViewModel(_databaseService, _claudeService, _panelService, _specFileService, _taskDecompositionService);
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

    // Task Decomposition Methods
    private async Task CheckAndOfferDecompositionAsync(TaskSpec task)
    {
        try
        {
            IsAnalyzingComplexity = true;
            LoggingService.LogInfo($"Analyzing complexity for task #{task.Number}", "MainViewModel");

            var suggestion = await _taskDecompositionService.SuggestDecompositionAsync(task);

            if (suggestion.ShouldDecompose)
            {
                DecompositionSuggestion = suggestion;
                ShowDecompositionSuggestion = true;
                LoggingService.LogInfo($"Offering decomposition for task #{task.Number} - {suggestion.Reason}", "MainViewModel");
            }
            else
            {
                LoggingService.LogInfo($"Task #{task.Number} does not need decomposition - {suggestion.Reason}", "MainViewModel");
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to analyze complexity for task #{task.Number}", ex, "MainViewModel");
        }
        finally
        {
            IsAnalyzingComplexity = false;
        }
    }

    [RelayCommand]
    private async Task AcceptDecompositionAsync()
    {
        if (DecompositionSuggestion == null || DecompositionSuggestion.SuggestedSubtasks.Count == 0)
            return;

        try
        {
            LoggingService.LogInfo($"Creating {DecompositionSuggestion.SuggestedSubtasks.Count} subtasks", "MainViewModel");

            // Find the parent task
            var parentTask = SelectedTaskDetail?.OriginalTask;
            if (parentTask == null)
            {
                LoggingService.LogError("No parent task found for decomposition", null, "MainViewModel");
                return;
            }

            // Create subtasks using the decomposition service
            var subtasks = await _taskDecompositionService.CreateSubtasksAsync(parentTask, DecompositionSuggestion.SuggestedSubtasks);

            // Save all subtasks to database
            foreach (var subtask in subtasks)
            {
                await _databaseService.SaveTaskSpecAsync(subtask);
                LoggingService.LogInfo($"Created subtask #{subtask.Number} - {subtask.Title}", "MainViewModel");
            }

            // Update the parent task to mark it as decomposed
            parentTask.IsDecomposed = true;
            parentTask.DecompositionStrategy = DecompositionSuggestion.Strategy.ToString();
            await _databaseService.SaveTaskSpecAsync(parentTask);

            ShowDecompositionSuggestion = false;
            DecompositionSuggestion = null;
            IsNewTaskPopupVisible = false; // Close the popup now

            LoggingService.LogInfo($"Successfully decomposed task #{parentTask.Number} into {subtasks.Count} subtasks", "MainViewModel");

            // Refresh the task list to show the new hierarchy
            if (SelectedProject?.Id == parentTask.ProjectId)
            {
                // Force a refresh of the task list
                OnPropertyChanged(nameof(SelectedProject));
            }

            MessageBox.Show($"Successfully created {subtasks.Count} subtasks for '{parentTask.Title}'",
                "Decomposition Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to create decomposed subtasks", ex, "MainViewModel");
            MessageBox.Show($"Failed to create subtasks: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void RejectDecomposition()
    {
        LoggingService.LogInfo("User rejected task decomposition suggestion", "MainViewModel");
        ShowDecompositionSuggestion = false;
        DecompositionSuggestion = null;
        IsNewTaskPopupVisible = false; // Close the popup now
    }

    [RelayCommand]
    private void DismissDecompositionSuggestion()
    {
        ShowDecompositionSuggestion = false;
        DecompositionSuggestion = null;
    }

    // Project Settings Panel Commands

    [RelayCommand]
    private void OpenProjectSettings()
    {
        if (SelectedProject == null)
        {
            MessageBox.Show("Please select a project first to access project settings.",
                "No Project Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Update analysis statistics from selected project
        FilesAnalyzedCount = SelectedProject.FilesAnalyzedCount;
        LastAnalysisDate = SelectedProject.LastAnalysisDate;

        IsProjectSettingsPanelOpen = true;
        LoggingService.LogInfo($"Opened project settings for {SelectedProject.Name}", "MainViewModel");
    }

    [RelayCommand]
    private void CloseProjectSettings()
    {
        IsProjectSettingsPanelOpen = false;
        LoggingService.LogInfo("Closed project settings panel", "MainViewModel");
    }

    [RelayCommand]
    private async Task BrowseProjectDirectory()
    {
        LogCommandExecution(nameof(BrowseProjectDirectory), SelectedProject?.Name);

        if (SelectedProject == null)
        {
            EnhancedLoggingService.LogWarning("BrowseProjectDirectory called with no selected project", "MainViewModel");
            return;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select project directory for analysis",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            SelectedPath = SelectedProject.ProjectDirectory ?? ""
        };

        EnhancedLoggingService.LogDebug($"Showing folder browser dialog with initial path: {SelectedProject.ProjectDirectory ?? "none"}", "MainViewModel");

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            // Ensure UI updates happen on the UI thread (though RelayCommand should already be on UI thread)
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Update both the model and the proxy property for binding
                SelectedProjectDirectory = dialog.SelectedPath;

                // Log the successful directory change
                EnhancedLoggingService.LogInfo($"Project directory updated successfully: {dialog.SelectedPath}", "MainViewModel", new
                {
                    ProjectName = SelectedProject?.Name,
                    NewPath = dialog.SelectedPath,
                    OldPath = SelectedProject?.ProjectDirectory
                });
            });

            // Persist changes to database immediately
            await UpdateProjectAsync();

            stopwatch.Stop();
            LogPerformance("BrowseProjectDirectory", stopwatch.Elapsed, new
            {
                ProjectName = SelectedProject?.Name,
                SelectedPath = dialog.SelectedPath
            });
        }
        else
        {
            stopwatch.Stop();
            EnhancedLoggingService.LogDebug("User cancelled folder browser dialog", "MainViewModel");
        }
    }

    [RelayCommand]
    private async Task RefreshProjectAnalysisAsync()
    {
        if (SelectedProject?.ProjectDirectory == null) return;

        try
        {
            LoggingService.LogInfo($"Starting project analysis refresh for {SelectedProject.Name}", "MainViewModel");

            // Clear existing context for this project
            await _databaseService.ClearProjectContextAsync(SelectedProject.Id);

            // Perform analysis
            var progress = new Progress<ProjectAnalysisProgress>(p =>
            {
                // Could show progress in UI if needed
                LoggingService.LogInfo($"Analysis progress: {p.Phase} - {p.Progress}%", "MainViewModel");
            });

            var result = await _projectContextService.AnalyzeProjectDirectoryAsync(
                SelectedProject.Id,
                SelectedProject.ProjectDirectory,
                progress);

            // Update project statistics
            SelectedProject.FilesAnalyzedCount = result.FilesAnalyzed;
            SelectedProject.LastAnalysisDate = DateTime.UtcNow;
            SelectedProject.LastDirectoryAnalysis = DateTime.UtcNow;

            // Update UI properties
            FilesAnalyzedCount = result.FilesAnalyzed;
            LastAnalysisDate = SelectedProject.LastAnalysisDate;

            // Save updated project to database
            await _databaseService.UpdateProjectAsync(SelectedProject);

            LoggingService.LogInfo($"Project analysis completed. Processed {result.FilesAnalyzed} files", "MainViewModel");

            MessageBox.Show($"Analysis completed!\n\nProcessed: {result.FilesAnalyzed} files\nSkipped: {result.FilesSkipped} files",
                           "Analysis Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to refresh project analysis for {SelectedProject.Name}", ex, "MainViewModel");
            MessageBox.Show($"Analysis failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ClearProjectCacheAsync()
    {
        if (SelectedProject == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to clear all analyzed data for '{SelectedProject.Name}'?\n\nThis will remove all file analysis cache and require a fresh analysis.",
            "Clear Project Cache",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            await _databaseService.ClearProjectContextAsync(SelectedProject.Id);

            // Reset statistics
            SelectedProject.FilesAnalyzedCount = 0;
            SelectedProject.LastAnalysisDate = null;
            SelectedProject.LastDirectoryAnalysis = null;

            // Update UI properties
            FilesAnalyzedCount = 0;
            LastAnalysisDate = null;

            // Save updated project to database
            await _databaseService.UpdateProjectAsync(SelectedProject);

            LoggingService.LogInfo($"Cleared project cache for {SelectedProject.Name}", "MainViewModel");
            MessageBox.Show("Project cache cleared successfully!", "Cache Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to clear project cache for {SelectedProject.Name}", ex, "MainViewModel");
            MessageBox.Show($"Failed to clear cache: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task SaveProjectChangesAsync()
    {
        if (SelectedProject == null) return;

        try
        {
            await _databaseService.UpdateProjectAsync(SelectedProject);
            LoggingService.LogInfo($"Saved project changes for {SelectedProject.Name}", "MainViewModel");
            MessageBox.Show("Project changes saved successfully!", "Changes Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to save project changes for {SelectedProject.Name}", ex, "MainViewModel");
            MessageBox.Show($"Failed to save changes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Calculate complexity scores for existing tasks that don't have them
    /// </summary>
    private async Task CalculateExistingTaskComplexityAsync()
    {
        try
        {
            LoggingService.LogInfo("Starting to calculate complexity for existing tasks", "MainViewModel");

            var projects = await _databaseService.GetProjectsAsync();

            int totalUpdated = 0;
            foreach (var project in projects)
            {
                var tasks = await _databaseService.GetTasksByProjectIdAsync(project.Id);
                var tasksToUpdate = tasks.Where(t => t.ComplexityScore == 0).ToList();

                foreach (var task in tasksToUpdate)
                {
                    var complexityScore = _taskDecompositionService.CalculateComplexityScore(task);
                    task.ComplexityScore = complexityScore;
                    await _databaseService.SaveTaskSpecAsync(task);
                    totalUpdated++;

                    LoggingService.LogInfo($"Updated task #{task.Number} complexity to {complexityScore}", "MainViewModel");
                }
            }

            if (totalUpdated > 0)
            {
                LoggingService.LogInfo($"Calculated complexity for {totalUpdated} existing tasks", "MainViewModel");
            }
            else
            {
                LoggingService.LogInfo("All existing tasks already have complexity scores", "MainViewModel");
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to calculate existing task complexity", ex, "MainViewModel");
        }
    }
}