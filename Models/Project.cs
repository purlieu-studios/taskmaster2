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

    public string? ClaudeMdPath { get; set; }

    public string? Metadata { get; set; } // JSON string for additional metadata

    public List<TaskSpec> Tasks { get; set; } = new();
}