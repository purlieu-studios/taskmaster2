using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using TaskMaster.Models;

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
    private string _taskType = "feature";

    public bool CanCreateTask => !string.IsNullOrWhiteSpace(TaskTitle) && SelectedProject != null;

    partial void OnTaskTitleChanged(string value)
    {
        OnPropertyChanged(nameof(CanCreateTask));
    }

    partial void OnSelectedProjectChanged(Project? value)
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
            Summary = "Task created via popup dialog", // Default summary
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
        TaskTitle = string.Empty;
        TaskType = "feature";
        SelectedProject = Projects.FirstOrDefault();
    }
}