using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TaskMaster.Models;

namespace TaskMaster.Views;

public partial class NewTaskDialog : Window, INotifyPropertyChanged
{
    private ObservableCollection<Project> _projects = new();
    private Project? _selectedProject;
    private string _taskTitle = "";
    private string _taskType = "feature";

    public NewTaskDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    public ObservableCollection<Project> Projects
    {
        get => _projects;
        set
        {
            _projects = value;
            OnPropertyChanged();
        }
    }

    public Project? SelectedProject
    {
        get => _selectedProject;
        set
        {
            _selectedProject = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanCreateTask));
        }
    }

    public string TaskTitle
    {
        get => _taskTitle;
        set
        {
            _taskTitle = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanCreateTask));
        }
    }

    public string TaskType
    {
        get => _taskType;
        set
        {
            _taskType = value;
            OnPropertyChanged();
        }
    }

    public bool CanCreateTask => SelectedProject != null && !string.IsNullOrWhiteSpace(TaskTitle);

    public TaskSpec? CreatedTask { get; private set; }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProject == null || string.IsNullOrWhiteSpace(TaskTitle))
            return;

        CreatedTask = new TaskSpec
        {
            ProjectId = SelectedProject.Id,
            Title = TaskTitle.Trim(),
            Type = TaskType,
            Status = Models.TaskStatus.Todo,
            Summary = "", // Will be filled in later
            Created = DateTime.UtcNow
        };

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}