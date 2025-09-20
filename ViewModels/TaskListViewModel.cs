using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using TaskMaster.Models;
using TaskMaster.Services;

namespace TaskMaster.ViewModels;

public partial class TaskListViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;

    [ObservableProperty]
    private Project? _selectedProject;

    [ObservableProperty]
    private TaskSpec? _selectedTask;

    [ObservableProperty]
    private ObservableCollection<TaskSpec> _tasks = new();

    [ObservableProperty]
    private ObservableCollection<TaskSpec> _filteredTasks = new();

    [ObservableProperty]
    private string _statusFilter = "All";

    [ObservableProperty]
    private string _typeFilter = "All";

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private int _pageSize = 25;

    [ObservableProperty]
    private string _taskCountText = "0 tasks";

    public TaskListViewModel()
    {
        _databaseService = new DatabaseService();
    }

    partial void OnSelectedProjectChanged(Project? value)
    {
        _ = RefreshTasksAsync();
    }

    partial void OnStatusFilterChanged(string value)
    {
        ApplyFiltersAndPagination();
    }

    partial void OnTypeFilterChanged(string value)
    {
        ApplyFiltersAndPagination();
    }

    partial void OnPageSizeChanged(int value)
    {
        CurrentPage = 1;
        ApplyFiltersAndPagination();
    }

    [RelayCommand]
    private async Task RefreshTasksAsync()
    {
        if (SelectedProject == null)
        {
            Tasks.Clear();
            FilteredTasks.Clear();
            TaskCountText = "0 tasks";
            return;
        }

        try
        {
            var tasks = await _databaseService.GetTasksByProjectIdAsync(SelectedProject.Id);
            Tasks.Clear();

            // Sort by number descending (newest first)
            foreach (var task in tasks.OrderByDescending(t => t.Number))
            {
                Tasks.Add(task);
            }

            ApplyFiltersAndPagination();
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to refresh tasks", ex, "TaskListViewModel");
        }
    }

    private void ApplyFiltersAndPagination()
    {
        var filtered = Tasks.AsEnumerable();

        // Apply status filter
        if (StatusFilter != "All")
        {
            if (Enum.TryParse<TaskMaster.Models.TaskStatus>(StatusFilter, out var status))
            {
                filtered = filtered.Where(t => t.Status == status);
            }
        }

        // Apply type filter
        if (TypeFilter != "All")
        {
            filtered = filtered.Where(t => t.Type.Equals(TypeFilter, StringComparison.OrdinalIgnoreCase));
        }

        var filteredList = filtered.ToList();
        var totalCount = filteredList.Count;

        // Calculate pagination
        TotalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / PageSize));
        CurrentPage = Math.Min(CurrentPage, TotalPages);

        var skip = (CurrentPage - 1) * PageSize;
        var pagedTasks = filteredList.Skip(skip).Take(PageSize);

        FilteredTasks.Clear();
        foreach (var task in pagedTasks)
        {
            FilteredTasks.Add(task);
        }

        // Update task count text
        var startIndex = totalCount > 0 ? skip + 1 : 0;
        var endIndex = Math.Min(skip + PageSize, totalCount);
        TaskCountText = totalCount > 0
            ? $"Showing {startIndex}-{endIndex} of {totalCount} tasks"
            : "0 tasks";
    }

    [RelayCommand]
    private void FirstPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage = 1;
            ApplyFiltersAndPagination();
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            ApplyFiltersAndPagination();
        }
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            ApplyFiltersAndPagination();
        }
    }

    [RelayCommand]
    private void LastPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage = TotalPages;
            ApplyFiltersAndPagination();
        }
    }
}