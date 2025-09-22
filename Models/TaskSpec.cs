using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TaskMaster.Models;

public partial class TaskSpec : ObservableValidator
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public int Number { get; set; } // Per-project sequential number

    [ObservableProperty]
    [Required]
    private string _title = string.Empty;

    public string Slug { get; set; } = string.Empty;

    [ObservableProperty]
    private string _type = "feature"; // feature, bug, enhancement, etc.

    [ObservableProperty]
    private TaskStatus _status = TaskStatus.Todo;

    [ObservableProperty]
    private int _priority = 50; // 0-1000, default medium priority

    public DateTime Created { get; set; } = DateTime.UtcNow;

    [ObservableProperty]
    [Required]
    private string _summary = string.Empty;

    [ObservableProperty]
    private string _acceptanceCriteria = string.Empty; // JSON array

    [ObservableProperty]
    private string? _nonGoals;

    [ObservableProperty]
    private string _testPlan = string.Empty; // JSON array

    [ObservableProperty]
    private string _scopePaths = string.Empty; // JSON array

    [ObservableProperty]
    private string _requiredDocs = string.Empty; // JSON array

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _suggestedTasks; // JSON array of suggested related tasks

    [ObservableProperty]
    private string? _nextSteps; // JSON array of suggested next steps

    // Task Decomposition Properties
    public int? ParentTaskId { get; set; } // For hierarchical task structure

    [ObservableProperty]
    private int _estimatedEffort = 0; // Story points or hours

    [ObservableProperty]
    private int _actualEffort = 0; // Actual time spent

    [ObservableProperty]
    private int _complexityScore = 0; // Calculated complexity (0-100)

    [ObservableProperty]
    private bool _isDecomposed = false; // Whether task has been broken down

    [ObservableProperty]
    private string? _decompositionStrategy; // How task was decomposed (layer, feature, phase, etc.)

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