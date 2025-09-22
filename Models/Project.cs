using System.ComponentModel.DataAnnotations;

namespace TaskMaster.Models;

public class Project
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public int TaskCount { get; set; } = 0;

    public int NextNumber { get; set; } = 1; // Next sequential number for new tasks

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    public string? ClaudeMdPath { get; set; } // Legacy - will be renamed to ProjectDirectory
    public string? ProjectDirectory { get; set; } // Directory path for project analysis

    public string? Metadata { get; set; } // JSON string for additional metadata

    // Analysis Statistics
    public DateTime? LastAnalysisDate { get; set; }
    public int FilesAnalyzedCount { get; set; } = 0;
    public DateTime? LastDirectoryAnalysis { get; set; }

    public List<TaskSpec> Tasks { get; set; } = new();
}