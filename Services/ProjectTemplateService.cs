using System.Text;
using TaskMaster.Models;

namespace TaskMaster.Services;

public static class ProjectTemplateService
{
    public static async Task<bool> GenerateProjectStructureAsync(string repoRoot, string projectName)
    {
        try
        {
            LoggingService.LogInfo($"Generating project structure for {projectName} in {repoRoot}", "ProjectTemplateService");

            if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
            {
                LoggingService.LogError($"Invalid repo root: {repoRoot}", null, "ProjectTemplateService");
                return false;
            }

            // Create directory structure
            var directories = new[]
            {
                Path.Combine(repoRoot, "docs", "specs"),
                Path.Combine(repoRoot, "docs", "decisions"),
                Path.Combine(repoRoot, "catalog"),
                Path.Combine(repoRoot, ".claude")
            };

            foreach (var dir in directories)
            {
                Directory.CreateDirectory(dir);
                LoggingService.LogInfo($"Created directory: {dir}", "ProjectTemplateService");
            }

            // Create CLAUDE.md template if it doesn't exist
            var claudeMdPath = Path.Combine(repoRoot, "CLAUDE.md");
            if (!File.Exists(claudeMdPath))
            {
                await CreateClaudeMdTemplateAsync(claudeMdPath, projectName);
            }

            // Create .claude/commands.md template if it doesn't exist
            var commandsPath = Path.Combine(repoRoot, ".claude", "commands.md");
            if (!File.Exists(commandsPath))
            {
                await CreateCommandsTemplateAsync(commandsPath);
            }

            // Create catalog README if it doesn't exist
            var catalogReadmePath = Path.Combine(repoRoot, "catalog", "README.md");
            if (!File.Exists(catalogReadmePath))
            {
                await CreateCatalogReadmeAsync(catalogReadmePath);
            }

            LoggingService.LogInfo($"Project structure generated successfully for {projectName}", "ProjectTemplateService");
            return true;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to generate project structure", ex, "ProjectTemplateService");
            return false;
        }
    }

    private static async Task CreateClaudeMdTemplateAsync(string filePath, string projectName)
    {
        var template = $@"# {projectName}

## Project Overview
Brief description of what this project does and its main purpose.

## Architecture
Describe the overall architecture, key components, and design patterns used.

## Development Guidelines
- Coding standards and conventions
- Testing requirements
- Documentation standards

## Key Files and Directories
- `src/` - Main source code
- `docs/specs/` - Task specifications
- `docs/decisions/` - Architectural decisions
- `catalog/` - TaskMaster catalog (Git-tracked JSON snapshots)

## Setup Instructions
1. Clone the repository
2. Install dependencies
3. Configure development environment
4. Run initial setup

## Important Notes
- Add any project-specific context that Claude should know
- Include patterns to follow or avoid
- Reference key documentation or external resources
";

        await File.WriteAllTextAsync(filePath, template, Encoding.UTF8);
        LoggingService.LogInfo($"Created CLAUDE.md template: {filePath}", "ProjectTemplateService");
    }

    private static async Task CreateCommandsTemplateAsync(string filePath)
    {
        var template = @"# Claude Commands

## Panel Commands
- `/panel-wpf-decide` - Run architectural design panel for a spec

## Update Commands
- `/update-wpf-spec` - Implement code based on panel decision

## Custom Commands
Add any project-specific Claude commands here.
";

        await File.WriteAllTextAsync(filePath, template, Encoding.UTF8);
        LoggingService.LogInfo($"Created commands template: {filePath}", "ProjectTemplateService");
    }

    private static async Task CreateCatalogReadmeAsync(string filePath)
    {
        var template = @"# TaskMaster Catalog

This directory contains Git-tracked JSON snapshots of the TaskMaster catalog.

## Files
- `catalog.json` - Complete catalog snapshot with all projects and tasks
- `{project}.json` - Per-project snapshots (if enabled)

## Purpose
- **Git History**: Track all spec changes with readable diffs
- **Collaboration**: Team members can see catalog via Git
- **Backup**: JSON serves as readable backup of SQLite database
- **CI/CD**: Automated tools can consume catalog.json

## Auto-Export
The catalog is automatically exported to Git on every spec save when:
- A repository root is configured in TaskMaster
- The `catalog/` directory exists

## Manual Export
You can also manually export the catalog using the ""Export Catalog"" button in TaskMaster.
";

        await File.WriteAllTextAsync(filePath, template, Encoding.UTF8);
        LoggingService.LogInfo($"Created catalog README: {filePath}", "ProjectTemplateService");
    }

    public static async Task GenerateProjectExportAsync(Project project, string outputPath)
    {
        try
        {
            LoggingService.LogInfo($"Exporting project template for {project.Name}", "ProjectTemplateService");

            var exportData = new
            {
                name = project.Name,
                claudeMdPath = project.ClaudeMdPath,
                exportedAt = DateTime.UtcNow,
                version = "1.0"
            };

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(exportData, Newtonsoft.Json.Formatting.Indented);
            await File.WriteAllTextAsync(outputPath, json, Encoding.UTF8);

            LoggingService.LogInfo($"Project template exported to: {outputPath}", "ProjectTemplateService");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to export project template", ex, "ProjectTemplateService");
            throw;
        }
    }

    public static async Task<Project?> ImportProjectAsync(string importPath)
    {
        try
        {
            LoggingService.LogInfo($"Importing project template from: {importPath}", "ProjectTemplateService");

            if (!File.Exists(importPath))
            {
                LoggingService.LogError($"Import file not found: {importPath}", null, "ProjectTemplateService");
                return null;
            }

            var json = await File.ReadAllTextAsync(importPath);
            var importData = Newtonsoft.Json.JsonConvert.DeserializeAnonymousType(json, new
            {
                name = "",
                claudeMdPath = "",
                exportedAt = DateTime.MinValue,
                version = ""
            });

            if (importData == null || string.IsNullOrWhiteSpace(importData.name))
            {
                LoggingService.LogError("Invalid project template format", null, "ProjectTemplateService");
                return null;
            }

            var project = new Project
            {
                Name = importData.name,
                ClaudeMdPath = importData.claudeMdPath
            };

            LoggingService.LogInfo($"Project template imported: {project.Name}", "ProjectTemplateService");
            return project;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to import project template", ex, "ProjectTemplateService");
            return null;
        }
    }
}
