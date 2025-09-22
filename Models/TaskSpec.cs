using System.ComponentModel.DataAnnotations;

namespace TaskMaster.Models;

public class TaskSpec
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public int Number { get; set; } // Per-project sequential number

    [Required]
    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Type { get; set; } = "feature"; // feature, bug, enhancement, etc.

    public TaskStatus Status { get; set; } = TaskStatus.Todo;

    public int Priority { get; set; } = 50; // 0-1000, default medium priority

    public DateTime Created { get; set; } = DateTime.UtcNow;

    [Required]
    public string Summary { get; set; } = string.Empty;

    public string AcceptanceCriteria { get; set; } = string.Empty; // JSON array

    public string? NonGoals { get; set; }

    public string TestPlan { get; set; } = string.Empty; // JSON array

    public string ScopePaths { get; set; } = string.Empty; // JSON array

    public string RequiredDocs { get; set; } = string.Empty; // JSON array

    public string? Notes { get; set; }

    public string? SuggestedTasks { get; set; } // JSON array of suggested related tasks

    public string? NextSteps { get; set; } // JSON array of suggested next steps

    // Task Decomposition Properties
    public int? ParentTaskId { get; set; } // For hierarchical task structure

    public int EstimatedEffort { get; set; } = 0; // Story points or hours

    public int ActualEffort { get; set; } = 0; // Actual time spent

    public int ComplexityScore { get; set; } = 0; // Calculated complexity (0-100)

    public bool IsDecomposed { get; set; } = false; // Whether task has been broken down

    public string? DecompositionStrategy { get; set; } // How task was decomposed (layer, feature, phase, etc.)

    // Navigation properties
    public Project Project { get; set; } = null!;

    public TaskSpec? ParentTask { get; set; }

    public ICollection<TaskSpec> ChildTasks { get; set; } = new List<TaskSpec>();
}

public enum TaskStatus
{
    Todo,
    InProgress,
    Done,
    Cancelled
}