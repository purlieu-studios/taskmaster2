using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using TaskMaster.Models;
using TaskMaster.Services;
using TaskMaster.Views;

namespace TaskMaster.ViewModels;

public partial class TaskListViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;

    [ObservableProperty]
    private Project? _selectedProject;

    [ObservableProperty]
    private TaskSpec? _selectedTask;

    partial void OnSelectedTaskChanged(TaskSpec? value)
    {
        LoggingService.LogInfo($"Task selection changed: {(value != null ? $"#{value.Number} - {value.Title}" : "null")}", "TaskListViewModel");
    }

    [ObservableProperty]
    private ObservableCollection<TaskSpec> _tasks = new();

    [ObservableProperty]
    private ObservableCollection<TaskSpec> _filteredTasks = new();

    [ObservableProperty]
    private ObservableCollection<TaskNode> _hierarchicalTasks = new();

    [ObservableProperty]
    private ObservableCollection<TaskNode> _filteredHierarchicalTasks = new();

    [ObservableProperty]
    private string _statusFilter = "All";

    [ObservableProperty]
    private string _typeFilter = "All";

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private int _pageSize = 25;

    [ObservableProperty]
    private string _taskCountText = "0 tasks";

    [ObservableProperty]
    private bool _isCompactView = false;

    [ObservableProperty]
    private bool _isHierarchicalView = false;

    [ObservableProperty]
    private string _sortBy = "Priority"; // Default sort by priority

    [ObservableProperty]
    private bool _sortAscending = false; // Default descending

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

    partial void OnSearchTextChanged(string value)
    {
        CurrentPage = 1;
        ApplyFiltersAndPagination();
    }

    partial void OnPageSizeChanged(int value)
    {
        CurrentPage = 1;
        ApplyFiltersAndPagination();
    }

    partial void OnIsHierarchicalViewChanged(bool value)
    {
        CurrentPage = 1;
        ApplyFiltersAndPagination();
    }

    partial void OnSortByChanged(string value)
    {
        CurrentPage = 1;
        ApplyFiltersAndPagination();
    }

    partial void OnSortAscendingChanged(bool value)
    {
        CurrentPage = 1;
        ApplyFiltersAndPagination();
    }

    [RelayCommand]
    private async Task RefreshTasksAsync()
    {
        LoggingService.LogInfo($"RefreshTasksAsync called - SelectedProject: {SelectedProject?.Name ?? "null"}", "TaskListViewModel");

        if (SelectedProject == null)
        {
            LoggingService.LogInfo("No project selected, clearing task list", "TaskListViewModel");
            Tasks.Clear();
            FilteredTasks.Clear();
            TaskCountText = "0 tasks";
            return;
        }

        try
        {
            LoggingService.LogInfo($"Getting tasks for project ID: {SelectedProject.Id}", "TaskListViewModel");

            // Load flat tasks
            var tasks = await _databaseService.GetTasksByProjectIdAsync(SelectedProject.Id);
            LoggingService.LogInfo($"Retrieved {tasks.Count} tasks from database", "TaskListViewModel");

            Tasks.Clear();

            // Apply current sort order to tasks
            var sortedTasks = ApplySorting(tasks);
            foreach (var task in sortedTasks)
            {
                Tasks.Add(task);
                LoggingService.LogInfo($"Added task to list: #{task.Number} - {task.Title}", "TaskListViewModel");
            }

            // Load hierarchical tasks
            var hierarchicalTasks = await _databaseService.GetTasksWithHierarchyAsync(SelectedProject.Id);
            LoggingService.LogInfo($"Retrieved {hierarchicalTasks.Count} root tasks for hierarchy", "TaskListViewModel");

            await BuildHierarchicalStructureAsync(hierarchicalTasks);

            ApplyFiltersAndPagination();
            LoggingService.LogInfo($"Task refresh completed - {Tasks.Count} flat tasks, {HierarchicalTasks.Count} root tasks loaded", "TaskListViewModel");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to refresh tasks", ex, "TaskListViewModel");
        }
    }

    private async Task BuildHierarchicalStructureAsync(List<TaskSpec> rootTasks)
    {
        HierarchicalTasks.Clear();

        foreach (var rootTask in rootTasks)
        {
            var rootNode = new TaskNode(rootTask);
            await BuildTaskNodeRecursiveAsync(rootNode);
            HierarchicalTasks.Add(rootNode);
        }
    }

    private async Task BuildTaskNodeRecursiveAsync(TaskNode parentNode)
    {
        try
        {
            var childTasks = await _databaseService.GetSubtasksAsync(parentNode.Task.Id);

            foreach (var childTask in childTasks)
            {
                var childNode = new TaskNode(childTask);
                parentNode.AddChild(childNode);

                // Recursively build children for this child node
                await BuildTaskNodeRecursiveAsync(childNode);
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to build task node for task #{parentNode.Task.Number}", ex, "TaskListViewModel");
        }
    }

    private void ApplyFiltersAndPagination()
    {
        if (IsHierarchicalView)
        {
            ApplyHierarchicalFiltersAndPagination();
        }
        else
        {
            ApplyFlatFiltersAndPagination();
        }
    }

    private void ApplyFlatFiltersAndPagination()
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

        // Apply search text filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLower();
            filtered = filtered.Where(t =>
                t.Title.ToLower().Contains(searchLower) ||
                (t.Summary?.ToLower().Contains(searchLower) ?? false) ||
                t.Number.ToString().Contains(searchLower) ||
                t.Type.ToLower().Contains(searchLower) ||
                t.Status.ToString().ToLower().Contains(searchLower) ||
                (t.AcceptanceCriteria?.ToLower().Contains(searchLower) ?? false) ||
                (t.TestPlan?.ToLower().Contains(searchLower) ?? false) ||
                (t.ScopePaths?.ToLower().Contains(searchLower) ?? false) ||
                (t.RequiredDocs?.ToLower().Contains(searchLower) ?? false) ||
                (t.NonGoals?.ToLower().Contains(searchLower) ?? false) ||
                (t.Notes?.ToLower().Contains(searchLower) ?? false));
        }

        // Apply sorting before pagination
        var sortedTasks = ApplySorting(filtered);
        var filteredList = sortedTasks.ToList();
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

    private void ApplyHierarchicalFiltersAndPagination()
    {
        var filtered = HierarchicalTasks.AsEnumerable();

        // Apply filters to hierarchical tasks
        filtered = filtered.Where(node => MatchesHierarchicalFilters(node));

        var filteredList = filtered.ToList();
        var totalCount = filteredList.Sum(node => CountAllNodes(node));

        // For hierarchical view, we don't paginate the tree structure
        // Instead, we show all matching root nodes and their children
        FilteredHierarchicalTasks.Clear();
        foreach (var node in filteredList)
        {
            FilteredHierarchicalTasks.Add(node);
        }

        // Update task count text for hierarchical view
        TaskCountText = totalCount > 0
            ? $"{totalCount} tasks in {filteredList.Count} hierarchies"
            : "0 tasks";

        // Reset pagination for hierarchical view
        TotalPages = 1;
        CurrentPage = 1;
    }

    private bool MatchesHierarchicalFilters(TaskNode node)
    {
        // Check if this node or any of its descendants match the filters
        if (MatchesFilters(node.Task) || node.MatchesSearch(SearchText))
        {
            return true;
        }

        // Check descendants
        return node.Children.Any(child => MatchesHierarchicalFilters(child));
    }

    private bool MatchesFilters(TaskSpec task)
    {
        // Status filter
        if (StatusFilter != "All")
        {
            if (Enum.TryParse<TaskMaster.Models.TaskStatus>(StatusFilter, out var status))
            {
                if (task.Status != status) return false;
            }
        }

        // Type filter
        if (TypeFilter != "All")
        {
            if (!task.Type.Equals(TypeFilter, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private int CountAllNodes(TaskNode node)
    {
        return 1 + node.Children.Sum(child => CountAllNodes(child));
    }

    private IEnumerable<TaskSpec> ApplySorting(IEnumerable<TaskSpec> tasks)
    {
        return SortBy switch
        {
            "Priority" => SortAscending
                ? tasks.OrderBy(t => t.Priority).ThenBy(t => t.Number)
                : tasks.OrderByDescending(t => t.Priority).ThenByDescending(t => t.Number),
            "Complexity" => SortAscending
                ? tasks.OrderBy(t => t.ComplexityScore).ThenBy(t => t.Number)
                : tasks.OrderByDescending(t => t.ComplexityScore).ThenByDescending(t => t.Number),
            "Status" => SortAscending
                ? tasks.OrderBy(t => t.Status).ThenBy(t => t.Number)
                : tasks.OrderByDescending(t => t.Status).ThenByDescending(t => t.Number),
            "Type" => SortAscending
                ? tasks.OrderBy(t => t.Type).ThenBy(t => t.Number)
                : tasks.OrderByDescending(t => t.Type).ThenByDescending(t => t.Number),
            "Created" => SortAscending
                ? tasks.OrderBy(t => t.Created).ThenBy(t => t.Number)
                : tasks.OrderByDescending(t => t.Created).ThenByDescending(t => t.Number),
            "Title" => SortAscending
                ? tasks.OrderBy(t => t.Title).ThenBy(t => t.Number)
                : tasks.OrderByDescending(t => t.Title).ThenByDescending(t => t.Number),
            "Number" => SortAscending
                ? tasks.OrderBy(t => t.Number)
                : tasks.OrderByDescending(t => t.Number),
            _ => SortAscending
                ? tasks.OrderBy(t => t.Priority).ThenBy(t => t.Number)
                : tasks.OrderByDescending(t => t.Priority).ThenByDescending(t => t.Number)
        };
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

    [RelayCommand]
    private void SelectTask(TaskSpec task)
    {
        if (task != null)
        {
            SelectedTask = task;
            LoggingService.LogInfo($"Selected task #{task.Number} - {task.Title}", "TaskListViewModel");
            TaskSelected?.Invoke(this, task);
        }
    }

    [RelayCommand]
    private void ToggleView()
    {
        IsCompactView = !IsCompactView;
        LoggingService.LogInfo($"View toggled to: {(IsCompactView ? "Compact" : "Card")}", "TaskListViewModel");
    }

    [RelayCommand]
    private void ToggleHierarchicalView()
    {
        IsHierarchicalView = !IsHierarchicalView;
        LoggingService.LogInfo($"Hierarchical view toggled to: {(IsHierarchicalView ? "Tree" : "Flat")}", "TaskListViewModel");
    }

    [RelayCommand]
    private void ToggleSortDirection()
    {
        SortAscending = !SortAscending;
        LoggingService.LogInfo($"Sort direction toggled to: {(SortAscending ? "Ascending" : "Descending")} for {SortBy}", "TaskListViewModel");
    }

    [RelayCommand]
    private async Task DeleteTaskAsync(TaskSpec task)
    {
        if (task == null) return;

        try
        {
            // Show custom confirmation dialog
            var confirmDialog = new DeleteConfirmationDialog(task)
            {
                Owner = Application.Current.MainWindow
            };

            var result = confirmDialog.ShowDialog();

            if (result == true && confirmDialog.Confirmed)
            {
                LoggingService.LogInfo($"Deleting task #{task.Number} - {task.Title}", "TaskListViewModel");

                var success = await _databaseService.DeleteTaskAsync(task.Id);

                if (success)
                {
                    // Remove from local collections
                    Tasks.Remove(task);
                    if (FilteredTasks.Contains(task))
                    {
                        FilteredTasks.Remove(task);
                    }

                    // Update pagination and counts
                    ApplyFiltersAndPagination();

                    LoggingService.LogInfo($"Task #{task.Number} deleted successfully", "TaskListViewModel");

                    // Notify that task was deleted
                    TaskDeleted?.Invoke(this, task);
                }
                else
                {
                    MessageBox.Show(
                        $"Failed to delete task #{task.Number}. Please try again.",
                        "Delete Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Error deleting task #{task.Number}", ex, "TaskListViewModel");
            MessageBox.Show(
                $"An error occurred while deleting the task: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public event EventHandler<TaskSpec>? TaskSelected;
    public event EventHandler<TaskSpec>? TaskDeleted;
}