using System.Collections.ObjectModel;

namespace TaskMaster.Models;

/// <summary>
/// Wrapper class for hierarchical task display in TreeView
/// </summary>
public class TaskNode
{
    public TaskSpec Task { get; set; }
    public ObservableCollection<TaskNode> Children { get; set; } = new();
    public TaskNode? Parent { get; set; }
    public bool IsExpanded { get; set; } = true;
    public bool IsRoot => Parent == null;
    public bool HasChildren => Children.Count > 0;
    public int Level { get; set; } = 0;

    public TaskNode(TaskSpec task)
    {
        Task = task;
    }

    /// <summary>
    /// Adds a child node and sets up parent relationship
    /// </summary>
    public void AddChild(TaskNode child)
    {
        child.Parent = this;
        child.Level = Level + 1;
        Children.Add(child);
    }

    /// <summary>
    /// Gets all descendant tasks (children, grandchildren, etc.)
    /// </summary>
    public IEnumerable<TaskNode> GetAllDescendants()
    {
        var descendants = new List<TaskNode>();
        foreach (var child in Children)
        {
            descendants.Add(child);
            descendants.AddRange(child.GetAllDescendants());
        }
        return descendants;
    }

    /// <summary>
    /// Calculates total estimated effort including all descendants
    /// </summary>
    public int TotalEstimatedEffort => Task.EstimatedEffort + Children.Sum(c => c.TotalEstimatedEffort);

    /// <summary>
    /// Calculates total actual effort including all descendants
    /// </summary>
    public int TotalActualEffort => Task.ActualEffort + Children.Sum(c => c.TotalActualEffort);

    /// <summary>
    /// Gets the completion percentage based on child task statuses
    /// </summary>
    public double CompletionPercentage
    {
        get
        {
            if (!HasChildren)
            {
                return Task.Status == TaskStatus.Done ? 100.0 : 0.0;
            }

            var totalTasks = GetAllDescendants().Count() + 1; // +1 for this task
            var completedTasks = GetAllDescendants().Count(t => t.Task.Status == TaskStatus.Done);
            if (Task.Status == TaskStatus.Done) completedTasks++;

            return totalTasks > 0 ? (completedTasks * 100.0) / totalTasks : 0.0;
        }
    }

    /// <summary>
    /// Gets the aggregate status based on child task statuses
    /// </summary>
    public TaskStatus AggregateStatus
    {
        get
        {
            if (!HasChildren)
            {
                return Task.Status;
            }

            var allStatuses = GetAllDescendants().Select(t => t.Task.Status).ToList();
            allStatuses.Add(Task.Status);

            // If any are cancelled, aggregate is cancelled
            if (allStatuses.Any(s => s == TaskStatus.Cancelled))
                return TaskStatus.Cancelled;

            // If all are done, aggregate is done
            if (allStatuses.All(s => s == TaskStatus.Done))
                return TaskStatus.Done;

            // If any are in progress, aggregate is in progress
            if (allStatuses.Any(s => s == TaskStatus.InProgress))
                return TaskStatus.InProgress;

            // Otherwise, aggregate is todo
            return TaskStatus.Todo;
        }
    }

    /// <summary>
    /// Searches this node and all descendants for matching text
    /// </summary>
    public bool MatchesSearch(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText)) return true;

        var searchLower = searchText.ToLower();

        // Check if this task matches
        if (Task.Title.ToLower().Contains(searchLower) ||
            (Task.Summary?.ToLower().Contains(searchLower) ?? false) ||
            Task.Number.ToString().Contains(searchLower) ||
            Task.Type.ToLower().Contains(searchLower))
        {
            return true;
        }

        // Check if any descendant matches
        return GetAllDescendants().Any(node =>
            node.Task.Title.ToLower().Contains(searchLower) ||
            (node.Task.Summary?.ToLower().Contains(searchLower) ?? false) ||
            node.Task.Number.ToString().Contains(searchLower) ||
            node.Task.Type.ToLower().Contains(searchLower));
    }
}