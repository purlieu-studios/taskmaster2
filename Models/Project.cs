using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TaskMaster.Models;

public partial class Project : ObservableValidator
{
    public int Id { get; set; }

    [ObservableProperty]
    [Required]
    private string _name = string.Empty;

    [ObservableProperty]
    private int _taskCount = 0;

    [ObservableProperty]
    private int _nextNumber = 1; // Next sequential number for new tasks

    [ObservableProperty]
    private DateTime _lastUpdated = DateTime.UtcNow;

    [ObservableProperty]
    private string? _claudeMdPath; // Legacy - will be renamed to ProjectDirectory

    [ObservableProperty]
    private string? _projectDirectory; // Directory path for project analysis

    [ObservableProperty]
    private string? _metadata; // JSON string for additional metadata

    // Analysis Statistics
    [ObservableProperty]
    private DateTime? _lastAnalysisDate;

    [ObservableProperty]
    private int _filesAnalyzedCount = 0;

    [ObservableProperty]
    private DateTime? _lastDirectoryAnalysis;

    public List<TaskSpec> Tasks { get; set; } = new();
}