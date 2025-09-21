using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using TaskMaster.Models;
using TaskMaster.Services;

namespace TaskMaster.ViewModels;

public partial class TaskDetailViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly ClaudeService _claudeService;
    private readonly PanelService _panelService;
    private readonly SpecFileService _specFileService;

    [ObservableProperty]
    private TaskSpec? _originalTask;

    [ObservableProperty]
    private int _number;

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _type = "feature";

    [ObservableProperty]
    private Models.TaskStatus _status = Models.TaskStatus.Todo;

    [ObservableProperty]
    private string _summary = "";

    [ObservableProperty]
    private string _acceptanceCriteria = "";

    [ObservableProperty]
    private string _testPlan = "";

    [ObservableProperty]
    private string _scopePaths = "";

    [ObservableProperty]
    private string _requiredDocs = "";

    [ObservableProperty]
    private string _nonGoals = "";

    [ObservableProperty]
    private string _notes = "";

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private bool _isInferring;

    public TaskDetailViewModel(DatabaseService databaseService, ClaudeService claudeService,
                              PanelService panelService, SpecFileService specFileService)
    {
        _databaseService = databaseService;
        _claudeService = claudeService;
        _panelService = panelService;
        _specFileService = specFileService;
    }

    public void LoadTask(TaskSpec task)
    {
        OriginalTask = task;
        Number = task.Number;
        Title = task.Title;
        Type = task.Type;
        Status = task.Status;
        Summary = task.Summary ?? "";
        AcceptanceCriteria = task.AcceptanceCriteria ?? "";
        TestPlan = task.TestPlan ?? "";
        ScopePaths = task.ScopePaths ?? "";
        RequiredDocs = task.RequiredDocs ?? "";
        NonGoals = task.NonGoals ?? "";
        Notes = task.Notes ?? "";
        HasUnsavedChanges = false;

        LoggingService.LogInfo($"Loaded task #{task.Number} - {task.Title} into detail view", "TaskDetailViewModel");
    }

    partial void OnTitleChanged(string value) => CheckForChanges();
    partial void OnTypeChanged(string value) => CheckForChanges();
    partial void OnStatusChanged(Models.TaskStatus value) => CheckForChanges();
    partial void OnSummaryChanged(string value) => CheckForChanges();
    partial void OnAcceptanceCriteriaChanged(string value) => CheckForChanges();
    partial void OnTestPlanChanged(string value) => CheckForChanges();
    partial void OnScopePathsChanged(string value) => CheckForChanges();
    partial void OnRequiredDocsChanged(string value) => CheckForChanges();
    partial void OnNonGoalsChanged(string value) => CheckForChanges();
    partial void OnNotesChanged(string value) => CheckForChanges();

    private void CheckForChanges()
    {
        if (OriginalTask == null) return;

        HasUnsavedChanges =
            Title != OriginalTask.Title ||
            Type != OriginalTask.Type ||
            Status != OriginalTask.Status ||
            Summary != (OriginalTask.Summary ?? "") ||
            AcceptanceCriteria != (OriginalTask.AcceptanceCriteria ?? "") ||
            TestPlan != (OriginalTask.TestPlan ?? "") ||
            ScopePaths != (OriginalTask.ScopePaths ?? "") ||
            RequiredDocs != (OriginalTask.RequiredDocs ?? "") ||
            NonGoals != (OriginalTask.NonGoals ?? "") ||
            Notes != (OriginalTask.Notes ?? "");
    }

    [RelayCommand]
    private async Task SaveTaskAsync()
    {
        if (OriginalTask == null) return;

        try
        {
            // Update the original task with current values
            OriginalTask.Title = Title;
            OriginalTask.Type = Type;
            OriginalTask.Status = Status;
            OriginalTask.Summary = Summary;
            OriginalTask.AcceptanceCriteria = AcceptanceCriteria;
            OriginalTask.TestPlan = TestPlan;
            OriginalTask.ScopePaths = ScopePaths;
            OriginalTask.RequiredDocs = RequiredDocs;
            OriginalTask.NonGoals = NonGoals;
            OriginalTask.Notes = Notes;

            await _databaseService.SaveTaskSpecAsync(OriginalTask);
            HasUnsavedChanges = false;

            LoggingService.LogInfo($"Task #{Number} saved successfully", "TaskDetailViewModel");

            // Notify that task was saved
            TaskSaved?.Invoke(this, OriginalTask);
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to save task #{Number}", ex, "TaskDetailViewModel");
        }
    }

    [RelayCommand]
    private async Task InferSpecAsync()
    {
        if (OriginalTask == null) return;

        try
        {
            IsInferring = true;

            // Use Claude to infer missing fields based on title and summary
            var request = new ClaudeInferenceRequest
            {
                Title = Title,
                Summary = Summary,
                ProjectName = "Current Project", // Will need to get this from somewhere
                ClaudeMdContent = "", // Will need to get this from somewhere
                RecentTasks = new List<TaskSpec>()
            };
            var inferredFields = await _claudeService.InferSpecFieldsAsync(request);

            if (inferredFields != null)
            {
                // Only update empty fields to preserve existing content
                if (string.IsNullOrWhiteSpace(AcceptanceCriteria))
                    AcceptanceCriteria = string.Join("\n", inferredFields.AcceptanceCriteria ?? new List<string>());

                if (string.IsNullOrWhiteSpace(TestPlan))
                    TestPlan = string.Join("\n", inferredFields.TestPlan ?? new List<string>());

                if (string.IsNullOrWhiteSpace(ScopePaths))
                    ScopePaths = string.Join("\n", inferredFields.ScopePaths ?? new List<string>());

                if (string.IsNullOrWhiteSpace(RequiredDocs))
                    RequiredDocs = string.Join("\n", inferredFields.RequiredDocs ?? new List<string>());

                LoggingService.LogInfo($"AI inference completed for task #{Number}", "TaskDetailViewModel");
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to infer spec for task #{Number}", ex, "TaskDetailViewModel");
        }
        finally
        {
            IsInferring = false;
        }
    }

    [RelayCommand]
    private void CancelChanges()
    {
        if (OriginalTask != null)
        {
            LoadTask(OriginalTask); // Reload original values
        }
    }

    [RelayCommand]
    private async Task RunPanelAsync()
    {
        if (OriginalTask == null) return;

        try
        {
            LoggingService.LogInfo($"Running panel for task #{Number}", "TaskDetailViewModel");
            // First need to save the task to a spec file to get the path
            var specPath = await _specFileService.SaveSpecToFileAsync(OriginalTask, null, Environment.CurrentDirectory);
            await _panelService.RunPanelAsync(specPath);
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to run panel for task #{Number}", ex, "TaskDetailViewModel");
        }
    }

    [RelayCommand]
    private async Task RunUpdateAsync()
    {
        if (OriginalTask == null) return;

        try
        {
            LoggingService.LogInfo($"Running update for task #{Number}", "TaskDetailViewModel");
            MessageBox.Show("Update functionality will be implemented later.", "Not Implemented",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to run update for task #{Number}", ex, "TaskDetailViewModel");
        }
    }

    public event EventHandler<TaskSpec>? TaskSaved;
}