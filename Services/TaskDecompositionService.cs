using TaskMaster.Models;
using System.Text.Json;

namespace TaskMaster.Services;

public class TaskDecompositionService
{
    private readonly ClaudeService _claudeService;
    private readonly DatabaseService _databaseService;

    public TaskDecompositionService(ClaudeService claudeService, DatabaseService databaseService)
    {
        _claudeService = claudeService;
        _databaseService = databaseService;
    }

    /// <summary>
    /// Calculates complexity score for a task based on various factors
    /// </summary>
    public int CalculateComplexityScore(TaskSpec task)
    {
        var complexity = new ComplexityFactors();

        // Analyze task content for complexity indicators
        var title = task.Title.ToLower();
        var summary = task.Summary.ToLower();
        var content = $"{title} {summary}";

        // File/component count estimation
        complexity.FileCount = EstimateFileCount(content);

        // Line change estimation
        complexity.LineChangeEstimate = EstimateLineChanges(content);

        // Feature flags
        complexity.RequiresNewAPI = ContainsKeywords(content, ["api", "endpoint", "rest", "service", "interface"]);
        complexity.HasDatabaseChanges = ContainsKeywords(content, ["database", "table", "schema", "migration", "entity"]);
        complexity.RequiresUIChanges = ContainsKeywords(content, ["ui", "interface", "view", "component", "page", "form"]);
        complexity.RequiresTestingChanges = ContainsKeywords(content, ["test", "testing", "unit test", "integration"]);

        // Additional complexity factors
        complexity.HasExternalDependencies = ContainsKeywords(content, ["external", "third-party", "integration", "oauth", "payment"]);
        complexity.RequiresDocumentation = ContainsKeywords(content, ["documentation", "docs", "readme", "guide"]);
        complexity.HasSecurityImplications = ContainsKeywords(content, ["security", "authentication", "authorization", "encryption"]);

        return CalculateScore(complexity);
    }

    /// <summary>
    /// Suggests decomposition of a complex task into subtasks
    /// </summary>
    public async Task<TaskDecompositionSuggestion> SuggestDecompositionAsync(TaskSpec parentTask)
    {
        var complexityScore = CalculateComplexityScore(parentTask);

        // Don't decompose simple tasks
        if (complexityScore < 30)
        {
            return new TaskDecompositionSuggestion
            {
                ShouldDecompose = false,
                Reason = "Task complexity is low enough to implement as single unit",
                ComplexityScore = complexityScore
            };
        }

        // Determine best decomposition strategy
        var strategy = DetermineDecompositionStrategy(parentTask);

        // Use Claude to generate subtasks
        var subtasks = await GenerateSubtasksWithClaudeAsync(parentTask, strategy);

        return new TaskDecompositionSuggestion
        {
            ShouldDecompose = true,
            Strategy = strategy,
            ComplexityScore = complexityScore,
            SuggestedSubtasks = subtasks,
            Reason = $"High complexity ({complexityScore}/100) suggests breaking into {subtasks.Count} subtasks"
        };
    }

    /// <summary>
    /// Creates actual subtask records in database
    /// </summary>
    public async Task<List<TaskSpec>> CreateSubtasksAsync(TaskSpec parentTask, List<SubtaskSuggestion> subtaskSuggestions)
    {
        var createdTasks = new List<TaskSpec>();
        var sequenceNumber = 1;

        foreach (var suggestion in subtaskSuggestions)
        {
            var subtask = new TaskSpec
            {
                ProjectId = parentTask.ProjectId,
                ParentTaskId = parentTask.Id,
                Title = suggestion.Title,
                Summary = suggestion.Summary,
                Type = suggestion.Type ?? parentTask.Type,
                Priority = suggestion.Priority ?? parentTask.Priority,
                EstimatedEffort = suggestion.EstimatedEffort,
                AcceptanceCriteria = JsonSerializer.Serialize(suggestion.AcceptanceCriteria),
                ScopePaths = JsonSerializer.Serialize(suggestion.ScopePaths),
                TestPlan = JsonSerializer.Serialize(suggestion.TestPlan),
                DecompositionStrategy = parentTask.DecompositionStrategy,
                Created = DateTime.UtcNow
            };

            // Generate unique number and slug
            subtask.Number = await _databaseService.GetNextTaskNumberAsync(parentTask.ProjectId);
            subtask.Slug = GenerateSubtaskSlug(parentTask.Slug, sequenceNumber);

            // Calculate complexity score for subtask
            subtask.ComplexityScore = CalculateComplexityScore(subtask);

            createdTasks.Add(subtask);
            sequenceNumber++;
        }

        // Mark parent as decomposed
        parentTask.IsDecomposed = true;

        return createdTasks;
    }

    private int EstimateFileCount(string content)
    {
        // Simple heuristic based on content analysis
        var keywords = new Dictionary<string, int>
        {
            ["single file"] = 1,
            ["multiple files"] = 3,
            ["several components"] = 4,
            ["many files"] = 6,
            ["entire module"] = 8,
            ["system-wide"] = 10
        };

        foreach (var keyword in keywords)
        {
            if (content.Contains(keyword.Key))
                return keyword.Value;
        }

        // Default estimation based on task type and keywords
        var baseCount = 1;
        if (content.Contains("component")) baseCount += 1;
        if (content.Contains("service")) baseCount += 1;
        if (content.Contains("model")) baseCount += 1;
        if (content.Contains("view")) baseCount += 1;
        if (content.Contains("controller")) baseCount += 1;

        return Math.Min(baseCount, 8); // Cap at 8 files
    }

    private int EstimateLineChanges(string content)
    {
        // Heuristic based on task scope keywords
        var multipliers = new Dictionary<string, int>
        {
            ["small"] = 50,
            ["minor"] = 75,
            ["medium"] = 150,
            ["large"] = 300,
            ["major"] = 500,
            ["complete"] = 800,
            ["rewrite"] = 1000
        };

        foreach (var multiplier in multipliers)
        {
            if (content.Contains(multiplier.Key))
                return multiplier.Value;
        }

        // Default based on keywords
        var baseLines = 100;
        if (content.Contains("new")) baseLines += 50;
        if (content.Contains("complex")) baseLines += 100;
        if (content.Contains("integration")) baseLines += 150;
        if (content.Contains("refactor")) baseLines += 200;

        return baseLines;
    }

    private bool ContainsKeywords(string content, string[] keywords)
    {
        return keywords.Any(keyword => content.Contains(keyword));
    }

    private int CalculateScore(ComplexityFactors factors)
    {
        var score = 0;

        // File count contribution (0-25 points)
        score += Math.Min(factors.FileCount * 3, 25);

        // Line changes contribution (0-30 points)
        score += Math.Min(factors.LineChangeEstimate / 30, 30);

        // Boolean factors (5 points each, max 35 points)
        if (factors.RequiresNewAPI) score += 8;
        if (factors.HasDatabaseChanges) score += 7;
        if (factors.RequiresUIChanges) score += 6;
        if (factors.RequiresTestingChanges) score += 5;
        if (factors.HasExternalDependencies) score += 9;
        if (factors.RequiresDocumentation) score += 3;
        if (factors.HasSecurityImplications) score += 7;

        return Math.Min(score, 100); // Cap at 100
    }

    private DecompositionStrategy DetermineDecompositionStrategy(TaskSpec task)
    {
        var content = $"{task.Title} {task.Summary}".ToLower();

        // Strategy priority order
        if (ContainsKeywords(content, ["api", "backend", "frontend", "ui", "database"]))
            return DecompositionStrategy.ArchitecturalLayer;

        if (ContainsKeywords(content, ["feature", "component", "module", "section"]))
            return DecompositionStrategy.FeatureComponent;

        if (ContainsKeywords(content, ["phase", "step", "stage", "milestone"]))
            return DecompositionStrategy.ImplementationPhase;

        if (ContainsKeywords(content, ["risk", "complex", "critical", "important"]))
            return DecompositionStrategy.RiskLevel;

        // Default to feature component
        return DecompositionStrategy.FeatureComponent;
    }

    private async Task<List<SubtaskSuggestion>> GenerateSubtasksWithClaudeAsync(TaskSpec parentTask, DecompositionStrategy strategy)
    {
        var prompt = BuildDecompositionPrompt(parentTask, strategy);

        try
        {
            var response = await _claudeService.CallClaudeDirectAsync(prompt);
            return ParseClaudeDecompositionResponse(response ?? string.Empty);
        }
        catch (Exception)
        {
            // Fallback to rule-based decomposition
            return GenerateFallbackSubtasks(parentTask, strategy);
        }
    }

    private string BuildDecompositionPrompt(TaskSpec parentTask, DecompositionStrategy strategy)
    {
        return $@"
Please decompose this task into subtasks using the {strategy} strategy:

**Parent Task:**
Title: {parentTask.Title}
Summary: {parentTask.Summary}
Type: {parentTask.Type}
Priority: {parentTask.Priority}

**Decomposition Strategy:** {strategy}

**Instructions:**
1. Break the task into 2-5 logical subtasks
2. Each subtask should be independently implementable
3. Provide clear titles, summaries, and acceptance criteria
4. Estimate effort for each subtask (1-8 story points)
5. Suggest appropriate file paths and test requirements

**Response Format (JSON):**
{{
  ""subtasks"": [
    {{
      ""title"": ""Subtask title"",
      ""summary"": ""Detailed description"",
      ""acceptanceCriteria"": [""Criteria 1"", ""Criteria 2""],
      ""scopePaths"": [""file/path.cs"", ""other/path.cs""],
      ""testPlan"": [""Test 1"", ""Test 2""],
      ""estimatedEffort"": 3,
      ""priority"": 50,
      ""type"": ""feature""
    }}
  ]
}}
";
    }

    private List<SubtaskSuggestion> ParseClaudeDecompositionResponse(string response)
    {
        try
        {
            // Extract JSON from response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}') + 1;

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonText = response.Substring(jsonStart, jsonEnd - jsonStart);
                var parsed = JsonSerializer.Deserialize<DecompositionResponse>(jsonText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return parsed?.Subtasks ?? new List<SubtaskSuggestion>();
            }
        }
        catch (JsonException)
        {
            // Fall through to empty list
        }

        return new List<SubtaskSuggestion>();
    }

    private List<SubtaskSuggestion> GenerateFallbackSubtasks(TaskSpec parentTask, DecompositionStrategy strategy)
    {
        // Simple rule-based fallback when Claude is unavailable
        return strategy switch
        {
            DecompositionStrategy.ArchitecturalLayer => new List<SubtaskSuggestion>
            {
                new() { Title = $"{parentTask.Title} - Backend Implementation", Summary = "Implement backend logic and data layer", EstimatedEffort = 5 },
                new() { Title = $"{parentTask.Title} - Frontend Implementation", Summary = "Implement user interface and user experience", EstimatedEffort = 4 },
                new() { Title = $"{parentTask.Title} - Integration & Testing", Summary = "Integrate components and add comprehensive tests", EstimatedEffort = 3 }
            },
            DecompositionStrategy.FeatureComponent => new List<SubtaskSuggestion>
            {
                new() { Title = $"{parentTask.Title} - Core Feature", Summary = "Implement main feature functionality", EstimatedEffort = 5 },
                new() { Title = $"{parentTask.Title} - Supporting Features", Summary = "Add supporting features and edge cases", EstimatedEffort = 3 },
                new() { Title = $"{parentTask.Title} - Polish & Documentation", Summary = "Polish implementation and add documentation", EstimatedEffort = 2 }
            },
            _ => new List<SubtaskSuggestion>
            {
                new() { Title = $"{parentTask.Title} - Phase 1", Summary = "Initial implementation", EstimatedEffort = 4 },
                new() { Title = $"{parentTask.Title} - Phase 2", Summary = "Extended functionality", EstimatedEffort = 4 }
            }
        };
    }

    private string GenerateSubtaskSlug(string parentSlug, int sequenceNumber)
    {
        return $"{parentSlug}-{sequenceNumber:D2}";
    }
}

// Supporting classes
public class ComplexityFactors
{
    public int FileCount { get; set; }
    public int LineChangeEstimate { get; set; }
    public bool RequiresNewAPI { get; set; }
    public bool HasDatabaseChanges { get; set; }
    public bool RequiresUIChanges { get; set; }
    public bool RequiresTestingChanges { get; set; }
    public bool HasExternalDependencies { get; set; }
    public bool RequiresDocumentation { get; set; }
    public bool HasSecurityImplications { get; set; }
}

public class TaskDecompositionSuggestion
{
    public bool ShouldDecompose { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int ComplexityScore { get; set; }
    public DecompositionStrategy Strategy { get; set; }
    public List<SubtaskSuggestion> SuggestedSubtasks { get; set; } = new();
}

public class SubtaskSuggestion
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> AcceptanceCriteria { get; set; } = new();
    public List<string> ScopePaths { get; set; } = new();
    public List<string> TestPlan { get; set; } = new();
    public int EstimatedEffort { get; set; }
    public int? Priority { get; set; }
    public string? Type { get; set; }
}

public class DecompositionResponse
{
    public List<SubtaskSuggestion> Subtasks { get; set; } = new();
}

public enum DecompositionStrategy
{
    ArchitecturalLayer,  // Backend, Frontend, Database
    FeatureComponent,    // Core feature, Supporting features, Polish
    ImplementationPhase, // Phase 1, Phase 2, Phase 3
    RiskLevel           // High risk first, Low risk items
}