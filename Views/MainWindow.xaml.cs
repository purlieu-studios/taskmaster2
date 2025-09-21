using System.Windows;
using TaskMaster.ViewModels;
using System.Windows.Controls;
using TaskMaster.Models;

namespace TaskMaster.Views;

public partial class MainWindow : Window
{
    private TaskListViewModel? _taskListViewModel;
    private MainViewModel? _mainViewModel;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;

            // Create and assign TaskListViewModel to TaskListView
            _taskListViewModel = new TaskListViewModel();
            TaskListView.DataContext = _taskListViewModel;

            // Set up task selection handler
            _taskListViewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(TaskListViewModel.SelectedTask) &&
                    _taskListViewModel.SelectedTask != null)
                {
                    // Open the selected task in the detail panel
                    _ = _mainViewModel.OpenTaskDetailAsync(_taskListViewModel.SelectedTask);
                }
            };

            // Handle task detail panel close event
            if (TaskDetailPanel != null)
            {
                TaskDetailPanel.PanelCloseRequested += (s, args) =>
                {
                    _mainViewModel.CloseTaskDetail();
                };
            }

            // Set up binding between view models
            mainViewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.SelectedProject))
                {
                    _taskListViewModel.SelectedProject = mainViewModel.SelectedProject;
                    // Apply filters from main view model
                    _taskListViewModel.StatusFilter = mainViewModel.StatusFilter;
                    _taskListViewModel.TypeFilter = mainViewModel.TypeFilter;
                }

                // Apply filter changes
                if (args.PropertyName == nameof(MainViewModel.StatusFilter))
                {
                    _taskListViewModel.StatusFilter = mainViewModel.StatusFilter;
                }

                if (args.PropertyName == nameof(MainViewModel.TypeFilter))
                {
                    _taskListViewModel.TypeFilter = mainViewModel.TypeFilter;
                }

                // Refresh task list when LastSavedSpecPath changes
                if (args.PropertyName == nameof(MainViewModel.LastSavedSpecPath))
                {
                    _ = _taskListViewModel.RefreshTasksCommand.ExecuteAsync(null);
                }
            };

            // Initialize with current selection
            _taskListViewModel.SelectedProject = mainViewModel.SelectedProject;
            _taskListViewModel.StatusFilter = mainViewModel.StatusFilter;
            _taskListViewModel.TypeFilter = mainViewModel.TypeFilter;

            TaskMaster.Services.LoggingService.LogInfo("MainWindow setup completed with new UI structure", "MainWindow");
        }
    }
}