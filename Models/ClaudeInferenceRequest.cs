namespace TaskMaster.Models;

public class ClaudeInferenceRequest
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ClaudeMdContent { get; set; } = string.Empty;
    public List<TaskSpec> RecentTasks { get; set; } = new();
}

public class ClaudeInferenceResponse
{
    public string Type { get; set; } = "feature";
    public List<string> ScopePaths { get; set; } = new();
    public List<string> AcceptanceCriteria { get; set; } = new();
    public List<string> TestPlan { get; set; } = new();
    public List<string> RequiredDocs { get; set; } = new();
    public string? NonGoals { get; set; }
    public List<SuggestedTask> SuggestedTasks { get; set; } = new();
    public List<SuggestedTask> NextSteps { get; set; } = new();
}

public class SuggestedTask
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Type { get; set; } = "feature";
}