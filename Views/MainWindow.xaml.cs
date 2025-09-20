using System.Windows;
using TaskMaster.ViewModels;

namespace TaskMaster.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Wire up the TaskListView to the MainViewModel's selected project
        if (DataContext is MainViewModel mainViewModel && TaskListView.DataContext is TaskListViewModel taskListViewModel)
        {
            // Set up binding between the two view models
            mainViewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.SelectedProject))
                {
                    taskListViewModel.SelectedProject = mainViewModel.SelectedProject;
                }

                // Refresh task list when LastSavedSpecPath changes (indicates a new spec was saved)
                if (args.PropertyName == nameof(MainViewModel.LastSavedSpecPath))
                {
                    _ = taskListViewModel.RefreshTasksCommand.ExecuteAsync(null);
                }
            };

            // Initialize with current selection
            taskListViewModel.SelectedProject = mainViewModel.SelectedProject;
        }
    }
}