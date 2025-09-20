using Newtonsoft.Json;
using System.Text;
using TaskMaster.Models;

namespace TaskMaster.Services;

public class SpecFileService
{
    private readonly DatabaseService _databaseService;

    public SpecFileService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public Task<string> GenerateMarkdownSpecAsync(TaskSpec taskSpec, Project project)
    {
        var spec = new StringBuilder();

        // Header
        spec.AppendLine($"# {taskSpec.Title}");
        spec.AppendLine();
        spec.AppendLine($"**Number:** #{taskSpec.Number}");
        spec.AppendLine($"**Type:** {taskSpec.Type}");
        spec.AppendLine($"**Project:** {project.Name}");
        spec.AppendLine($"**Created:** {taskSpec.Created:yyyy-MM-dd}");
        spec.AppendLine();

        // Scope Paths
        var scopePaths = JsonConvert.DeserializeObject<List<string>>(taskSpec.ScopePaths) ?? new List<string>();
        if (scopePaths.Any())
        {
            spec.AppendLine("## Scope Paths");
            spec.AppendLine();
            foreach (var path in scopePaths)
            {
                spec.AppendLine($"- `{path}`");
            }
            spec.AppendLine();
        }

        // Required Docs
        var requiredDocs = JsonConvert.DeserializeObject<List<string>>(taskSpec.RequiredDocs) ?? new List<string>();
        if (requiredDocs.Any())
        {
            spec.AppendLine("## Required Docs");
            spec.AppendLine();
            foreach (var doc in requiredDocs)
            {
                spec.AppendLine($"- `{doc}`");
            }
            spec.AppendLine();
        }

        // Summary
        spec.AppendLine("## Summary");
        spec.AppendLine();
        spec.AppendLine(taskSpec.Summary);
        spec.AppendLine();

        // Acceptance Criteria
        var acceptanceCriteria = JsonConvert.DeserializeObject<List<string>>(taskSpec.AcceptanceCriteria) ?? new List<string>();
        if (acceptanceCriteria.Any())
        {
            spec.AppendLine("## Acceptance Criteria");
            spec.AppendLine();
            foreach (var criterion in acceptanceCriteria)
            {
                spec.AppendLine($"- {criterion}");
            }
            spec.AppendLine();
        }

        // Non-Goals
        if (!string.IsNullOrWhiteSpace(taskSpec.NonGoals))
        {
            spec.AppendLine("## Non-Goals");
            spec.AppendLine();
            spec.AppendLine(taskSpec.NonGoals);
            spec.AppendLine();
        }

        // Test Plan
        var testPlan = JsonConvert.DeserializeObject<List<string>>(taskSpec.TestPlan) ?? new List<string>();
        if (testPlan.Any())
        {
            spec.AppendLine("## Test Plan");
            spec.AppendLine();
            foreach (var test in testPlan)
            {
                spec.AppendLine($"- {test}");
            }
            spec.AppendLine();
        }

        // Notes
        if (!string.IsNullOrWhiteSpace(taskSpec.Notes))
        {
            spec.AppendLine("## Notes");
            spec.AppendLine();
            spec.AppendLine(taskSpec.Notes);
            spec.AppendLine();
        }

        return Task.FromResult(spec.ToString());
    }

    public async Task<string> SaveSpecToFileAsync(TaskSpec taskSpec, Project project, string repoRoot)
    {
        var markdown = await GenerateMarkdownSpecAsync(taskSpec, project);

        var fileName = $"{DateTime.UtcNow:yyyyMMdd}-{taskSpec.Slug}.md";
        var specsDir = Path.Combine(repoRoot, "docs", "specs");

        Directory.CreateDirectory(specsDir);
        var filePath = Path.Combine(specsDir, fileName);

        await File.WriteAllTextAsync(filePath, markdown, Encoding.UTF8);

        return filePath;
    }

    public string GetDecisionFilePath(string specFilePath)
    {
        var fileName = Path.GetFileName(specFilePath);
        var repoRoot = GetRepoRootFromSpecPath(specFilePath);
        var decisionsDir = Path.Combine(repoRoot, "docs", "decisions");

        Directory.CreateDirectory(decisionsDir);
        return Path.Combine(decisionsDir, fileName);
    }

    private string GetRepoRootFromSpecPath(string specFilePath)
    {
        var dir = Path.GetDirectoryName(specFilePath);
        while (dir != null && !Path.GetFileName(dir).Equals("specs", StringComparison.OrdinalIgnoreCase))
        {
            dir = Path.GetDirectoryName(dir);
        }

        if (dir != null)
        {
            // Go up from docs/specs to repo root
            return Path.GetDirectoryName(Path.GetDirectoryName(dir)) ?? dir;
        }

        return Path.GetDirectoryName(specFilePath) ?? Environment.CurrentDirectory;
    }

    public Task<string> ExportCatalogAsync(List<Project> projects)
    {
        var catalog = new
        {
            exportedAt = DateTime.UtcNow,
            projects = projects.Select(p => new
            {
                name = p.Name,
                taskCount = p.TaskCount,
                lastUpdated = p.LastUpdated,
                claudeMdPath = p.ClaudeMdPath,
                tasks = p.Tasks.Select(t => new
                {
                    id = t.Number,
                    title = t.Title,
                    slug = t.Slug,
                    type = t.Type,
                    status = t.Status.ToString().ToLowerInvariant(),
                    created = t.Created,
                    summary = t.Summary,
                    acceptanceCriteria = JsonConvert.DeserializeObject<List<string>>(t.AcceptanceCriteria),
                    testPlan = JsonConvert.DeserializeObject<List<string>>(t.TestPlan),
                    scopePaths = JsonConvert.DeserializeObject<List<string>>(t.ScopePaths),
                    requiredDocs = JsonConvert.DeserializeObject<List<string>>(t.RequiredDocs)
                })
            })
        };

        return Task.FromResult(JsonConvert.SerializeObject(catalog, Formatting.Indented));
    }
}