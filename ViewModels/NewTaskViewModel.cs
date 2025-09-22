using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using TaskMaster.Models;
using TaskMaster.Services;

namespace TaskMaster.ViewModels;

public partial class NewTaskViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<Project> _projects = new();

    [ObservableProperty]
    private Project? _selectedProject;

    [ObservableProperty]
    private string _taskTitle = string.Empty;

    [ObservableProperty]
    private string _taskSummary = string.Empty;

    [ObservableProperty]
    private string _taskType = "feature";

    [ObservableProperty]
    private bool _isGenerating = false;

    [ObservableProperty]
    private bool _useAIEnhancement = true;

    private CancellationTokenSource? _cancellationTokenSource;

    public bool CanCreateTask => !string.IsNullOrWhiteSpace(TaskTitle) && SelectedProject != null && !IsGenerating;

    partial void OnTaskTitleChanged(string value)
    {
        OnPropertyChanged(nameof(CanCreateTask));
    }

    partial void OnSelectedProjectChanged(Project? value)
    {
        OnPropertyChanged(nameof(CanCreateTask));
    }

    partial void OnIsGeneratingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCreateTask));
    }

    public TaskSpec CreateTaskSpec()
    {
        if (SelectedProject == null)
            throw new InvalidOperationException("No project selected");

        return new TaskSpec
        {
            Title = TaskTitle,
            Type = TaskType,
            Summary = !string.IsNullOrWhiteSpace(TaskSummary) ? TaskSummary : TaskTitle,
            ProjectId = SelectedProject.Id,
            Created = DateTime.UtcNow,
            Status = Models.TaskStatus.Todo,
            AcceptanceCriteria = "[]",
            TestPlan = "[]",
            ScopePaths = "[]",
            RequiredDocs = "[]",
            NonGoals = "[]",
            Notes = "[]"
        };
    }

    public void Reset()
    {
        // Cancel any ongoing AI generation
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        TaskTitle = string.Empty;
        TaskSummary = string.Empty;
        TaskType = "feature";
        SelectedProject = Projects.FirstOrDefault();
        IsGenerating = false;
    }

    public void CancelAIGeneration()
    {
        _cancellationTokenSource?.Cancel();
        IsGenerating = false;
        LoggingService.LogInfo("AI generation cancelled by user", "NewTaskViewModel");
    }

    public CancellationToken GetCancellationToken()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
        return _cancellationTokenSource.Token;
    }
}