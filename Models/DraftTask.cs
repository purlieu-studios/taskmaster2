namespace TaskMaster.Models;

public class DraftTask
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Type { get; set; } = "feature";

    public DateTime Created { get; set; } = DateTime.UtcNow;

    public bool IsNextStep { get; set; } = false; // true if this is a next-step suggestion

    // Navigation property
    public Project Project { get; set; } = null!;
}