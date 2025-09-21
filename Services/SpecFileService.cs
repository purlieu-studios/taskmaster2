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

        // Header - exactly as specified in vision document
        spec.AppendLine($"# {taskSpec.Title}");
        spec.AppendLine();
        spec.AppendLine($"**Number:** #{taskSpec.Number}");
        spec.AppendLine($"**Type:** {taskSpec.Type}");
        spec.AppendLine($"**Project:** {project.Name}");
        spec.AppendLine($"**Created:** {taskSpec.Created:yyyy-MM-dd}");
        spec.AppendLine();

        // Scope Paths - required section, even if empty
        spec.AppendLine("## Scope Paths");
        spec.AppendLine();
        var scopePaths = JsonConvert.DeserializeObject<List<string>>(taskSpec.ScopePaths) ?? new List<string>();
        if (scopePaths.Any())
        {
            foreach (var path in scopePaths)
            {
                spec.AppendLine($"- `{path}`");
            }
        }
        else
        {
            spec.AppendLine("- *To be determined during implementation*");
        }
        spec.AppendLine();

        // Required Docs - required section, even if empty
        spec.AppendLine("## Required Docs");
        spec.AppendLine();
        var requiredDocs = JsonConvert.DeserializeObject<List<string>>(taskSpec.RequiredDocs) ?? new List<string>();
        if (requiredDocs.Any())
        {
            foreach (var doc in requiredDocs)
            {
                spec.AppendLine($"- `{doc}`");
            }
        }
        else
        {
            spec.AppendLine("- *No additional documentation required*");
        }
        spec.AppendLine();

        // Summary - required section
        spec.AppendLine("## Summary");
        spec.AppendLine();
        spec.AppendLine(string.IsNullOrWhiteSpace(taskSpec.Summary) ? "*Summary not provided*" : taskSpec.Summary);
        spec.AppendLine();

        // Acceptance Criteria - required section, even if empty
        spec.AppendLine("## Acceptance Criteria");
        spec.AppendLine();
        var acceptanceCriteria = JsonConvert.DeserializeObject<List<string>>(taskSpec.AcceptanceCriteria) ?? new List<string>();
        if (acceptanceCriteria.Any())
        {
            foreach (var criterion in acceptanceCriteria)
            {
                spec.AppendLine($"- {criterion}");
            }
        }
        else
        {
            spec.AppendLine("- *Acceptance criteria to be defined*");
        }
        spec.AppendLine();

        // Non-Goals - optional section, only include if specified
        if (!string.IsNullOrWhiteSpace(taskSpec.NonGoals))
        {
            spec.AppendLine("## Non-Goals");
            spec.AppendLine();
            spec.AppendLine(taskSpec.NonGoals);
            spec.AppendLine();
        }

        // Test Plan - required section, even if empty
        spec.AppendLine("## Test Plan");
        spec.AppendLine();
        var testPlan = JsonConvert.DeserializeObject<List<string>>(taskSpec.TestPlan) ?? new List<string>();
        if (testPlan.Any())
        {
            foreach (var test in testPlan)
            {
                spec.AppendLine($"- {test}");
            }
        }
        else
        {
            spec.AppendLine("- *Test plan to be defined during implementation*");
        }
        spec.AppendLine();

        // Notes - optional section, only include if specified
        if (!string.IsNullOrWhiteSpace(taskSpec.Notes))
        {
            spec.AppendLine("## Notes");
            spec.AppendLine();
            spec.AppendLine(taskSpec.Notes);
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
        var repoRoot = GetRepoRootFromSpecPath(specFilePath);
        var decisionsDir = Path.Combine(repoRoot, "docs", "decisions");

        Directory.CreateDirectory(decisionsDir);

        // Extract slug from spec filename (remove date prefix if present)
        var specFileName = Path.GetFileNameWithoutExtension(specFilePath);
        var slug = System.Text.RegularExpressions.Regex.Replace(specFileName, @"^\d{8}-", "");

        // Look for existing decision file with any timestamp and this slug
        var pattern = $"*-{slug}.md";
        var existingFiles = Directory.GetFiles(decisionsDir, pattern);

        if (existingFiles.Length > 0)
        {
            // Return the most recent decision file for this slug
            return existingFiles.OrderByDescending(f => f).First();
        }

        // If no existing file, return path with today's timestamp
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        return Path.Combine(decisionsDir, $"{timestamp}-{slug}.md");
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
            // Metadata for schema versioning and tracking
            version = "1.0",
            exportedAt = DateTime.UtcNow,
            exportedBy = Environment.UserName,
            machineName = Environment.MachineName,
            taskMasterVersion = "1.0.0", // TODO: Get from assembly version
            totalProjects = projects.Count,
            totalTasks = projects.Sum(p => p.Tasks?.Count ?? 0),

            // Projects data in deterministic order
            projects = projects
                .OrderBy(p => p.Name)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    taskCount = p.TaskCount,
                    lastUpdated = p.LastUpdated,
                    claudeMdPath = p.ClaudeMdPath,
                    slug = SlugService.GenerateSlug(p.Name),

                    // Tasks in deterministic order (by number)
                    tasks = (p.Tasks ?? new List<TaskSpec>())
                        .OrderBy(t => t.Number)
                        .Select(t => new
                        {
                            id = t.Number,
                            title = t.Title,
                            slug = t.Slug,
                            type = t.Type,
                            status = t.Status.ToString().ToLowerInvariant(),
                            created = t.Created,
                            summary = t.Summary,
                            nonGoals = t.NonGoals,
                            notes = t.Notes,

                            // Parse JSON fields safely
                            acceptanceCriteria = SafeDeserializeJsonArray(t.AcceptanceCriteria),
                            testPlan = SafeDeserializeJsonArray(t.TestPlan),
                            scopePaths = SafeDeserializeJsonArray(t.ScopePaths),
                            requiredDocs = SafeDeserializeJsonArray(t.RequiredDocs),
                            suggestedTasks = SafeDeserializeJsonArray(t.SuggestedTasks ?? "[]"),
                            nextSteps = SafeDeserializeJsonArray(t.NextSteps ?? "[]")
                        })
                })
        };

        return Task.FromResult(JsonConvert.SerializeObject(catalog, Formatting.Indented));
    }

    private List<object> SafeDeserializeJsonArray(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<object>();

            var result = JsonConvert.DeserializeObject<List<object>>(json);
            return result ?? new List<object>();
        }
        catch
        {
            // If JSON parsing fails, return the raw string as a single item
            return new List<object> { json };
        }
    }

    public async Task SaveCatalogToRepoAsync(List<Project> projects, string repoRoot, string catalogPath = "catalog", bool enablePerProjectCatalogs = false)
    {
        try
        {
            LoggingService.LogInfo($"Saving catalog to repo: {repoRoot}/{catalogPath}", "SpecFileService");

            // Create catalog directory if it doesn't exist
            var catalogDir = Path.Combine(repoRoot, catalogPath);
            Directory.CreateDirectory(catalogDir);

            // Generate catalog JSON
            var catalogJson = await ExportCatalogAsync(projects);

            // Save global catalog
            var globalCatalogPath = Path.Combine(catalogDir, "catalog.json");
            await File.WriteAllTextAsync(globalCatalogPath, catalogJson, Encoding.UTF8);

            LoggingService.LogInfo($"Global catalog saved to: {globalCatalogPath}", "SpecFileService");

            // Save per-project catalogs if enabled
            if (enablePerProjectCatalogs)
            {
                foreach (var project in projects)
                {
                    var projectCatalogJson = await ExportCatalogAsync(new List<Project> { project });
                    var projectSlug = SlugService.GenerateSlug(project.Name);
                    var projectCatalogPath = Path.Combine(catalogDir, $"{projectSlug}.json");

                    await File.WriteAllTextAsync(projectCatalogPath, projectCatalogJson, Encoding.UTF8);
                    LoggingService.LogInfo($"Project catalog saved: {projectCatalogPath}", "SpecFileService");
                }
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to save catalog to repo", ex, "SpecFileService");
            throw;
        }
    }
}