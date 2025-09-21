using System.Windows;
using TaskMaster.ViewModels;
using System.Windows.Controls;

namespace TaskMaster.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private TaskListViewModel? _taskListViewModel;

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Wire up the TaskListView to the MainViewModel's selected project
        if (DataContext is MainViewModel mainViewModel)
        {
            // Create and assign TaskListViewModel to TaskListView
            _taskListViewModel = new TaskListViewModel();
            TaskListView.DataContext = _taskListViewModel;

            // Set up property changed handler to update task detail DataContext
            _taskListViewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(TaskListViewModel.SelectedTask))
                {
                    TaskMaster.Services.LoggingService.LogInfo($"SelectedTask changed to: {(_taskListViewModel.SelectedTask?.Title ?? "null")}", "MainWindow");

                    // Use Dispatcher to ensure UI updates happen on UI thread
                    Dispatcher.BeginInvoke(() =>
                    {
                        var taskDetailGrid = FindName("TaskDetailGrid") as FrameworkElement;
                        if (taskDetailGrid != null)
                        {
                            TaskMaster.Services.LoggingService.LogInfo($"Setting TaskDetailGrid DataContext to: {(_taskListViewModel.SelectedTask?.Title ?? "null")}", "MainWindow");
                            taskDetailGrid.DataContext = _taskListViewModel.SelectedTask;
                        }
                        else
                        {
                            TaskMaster.Services.LoggingService.LogError("TaskDetailGrid not found during update!", null, "MainWindow");
                        }
                    });
                }
            };

            // Also hook into the DataGrid selection changed event directly as a backup
            if (TaskListView.FindName("TaskDataGrid") is DataGrid dataGrid)
            {
                dataGrid.SelectionChanged += (s, args) =>
                {
                    TaskMaster.Services.LoggingService.LogInfo($"DataGrid SelectionChanged event fired", "MainWindow");
                    if (dataGrid.SelectedItem is TaskMaster.Models.TaskSpec selectedTask)
                    {
                        TaskMaster.Services.LoggingService.LogInfo($"DataGrid selected task: {selectedTask.Title}", "MainWindow");
                        _taskListViewModel.SelectedTask = selectedTask;
                    }
                };
            }

            // Set up binding between the two view models
            mainViewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.SelectedProject))
                {
                    _taskListViewModel.SelectedProject = mainViewModel.SelectedProject;
                }

                // Refresh task list when LastSavedSpecPath changes (indicates a new spec was saved)
                if (args.PropertyName == nameof(MainViewModel.LastSavedSpecPath))
                {
                    _ = _taskListViewModel.RefreshTasksCommand.ExecuteAsync(null);
                }
            };

            // Initialize with current selection
            _taskListViewModel.SelectedProject = mainViewModel.SelectedProject;

            TaskMaster.Services.LoggingService.LogInfo("MainWindow setup completed", "MainWindow");
        }
    }
}