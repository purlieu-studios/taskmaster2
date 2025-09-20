using System.IO.Compression;
using System.Text.RegularExpressions;

namespace TaskMaster.Services;

public class TemplateService
{
    private readonly DatabaseService _databaseService;
    private readonly string[] _excludeDirectories =
    {
        "bin", "obj", ".vs", ".git", "packages", "node_modules",
        "Debug", "Release", "TestResults", ".vscode"
    };

    private readonly string[] _excludeExtensions =
    {
        ".exe", ".dll", ".pdb", ".cache", ".tmp", ".log",
        ".user", ".suo", ".aps", ".ncb", ".opensdf", ".sdf"
    };

    public TemplateService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<string> ExportProjectTemplateAsync(
        string projectPath,
        string outputPath,
        string templateName,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            LoggingService.LogInfo($"Starting template export: {templateName}", "TemplateService");

            if (!Directory.Exists(projectPath))
            {
                throw new ArgumentException($"Project path does not exist: {projectPath}");
            }

            // Create output directory if needed
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Get all files to include
            progress?.Report(new ExportProgress { Stage = "Scanning files...", PercentComplete = 0 });
            var filesToInclude = await GetFilesToIncludeAsync(projectPath, cancellationToken);

            if (filesToInclude.Count == 0)
            {
                throw new InvalidOperationException("No files found to include in template");
            }

            // Create zip archive
            progress?.Report(new ExportProgress { Stage = "Creating archive...", PercentComplete = 10 });
            await CreateZipArchiveAsync(projectPath, outputPath, filesToInclude, progress, cancellationToken);

            // Log export history
            await LogExportHistoryAsync(templateName, outputPath, filesToInclude.Count);

            LoggingService.LogInfo($"Template export completed: {outputPath}", "TemplateService");
            return outputPath;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Template export failed", ex, "TemplateService");
            throw;
        }
    }

    private async Task<List<string>> GetFilesToIncludeAsync(string projectPath, CancellationToken cancellationToken)
    {
        var filesToInclude = new List<string>();

        await Task.Run(() =>
        {
            var allFiles = Directory.GetFiles(projectPath, "*", SearchOption.AllDirectories);

            foreach (var file in allFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (ShouldIncludeFile(file, projectPath))
                {
                    filesToInclude.Add(file);
                }
            }
        }, cancellationToken);

        return filesToInclude;
    }

    private bool ShouldIncludeFile(string filePath, string projectPath)
    {
        var relativePath = Path.GetRelativePath(projectPath, filePath);

        // Check for excluded directories
        var pathParts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (pathParts.Any(part => _excludeDirectories.Contains(part, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        // Check for excluded extensions
        var extension = Path.GetExtension(filePath);
        if (_excludeExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        // Always include these important files
        var fileName = Path.GetFileName(filePath);
        var importantFiles = new[] { "CLAUDE.md", ".gitignore", "README.md", "global.json" };
        if (importantFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        // Include .claude directory contents
        if (relativePath.StartsWith(".claude", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Include source files
        var sourceExtensions = new[] { ".cs", ".xaml", ".csproj", ".sln", ".md", ".json", ".xml", ".config" };
        if (sourceExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private async Task CreateZipArchiveAsync(
        string projectPath,
        string outputPath,
        List<string> filesToInclude,
        IProgress<ExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        // Remove existing file if it exists
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);

        for (int i = 0; i < filesToInclude.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = filesToInclude[i];
            var relativePath = Path.GetRelativePath(projectPath, filePath);

            // Sanitize path for cross-platform compatibility
            var entryName = relativePath.Replace('\\', '/');

            // Read file content and sanitize if needed
            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            var sanitizedContent = SanitizeFileContent(fileContent, filePath);

            // Create zip entry
            var entry = archive.CreateEntry(entryName);
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream);
            await writer.WriteAsync(sanitizedContent);

            // Report progress
            var percentComplete = 10 + (int)((i + 1) * 80.0 / filesToInclude.Count);
            progress?.Report(new ExportProgress
            {
                Stage = $"Adding file {i + 1} of {filesToInclude.Count}...",
                PercentComplete = percentComplete,
                CurrentFile = Path.GetFileName(filePath)
            });
        }

        progress?.Report(new ExportProgress { Stage = "Finalizing archive...", PercentComplete = 95 });
    }

    private string SanitizeFileContent(string content, string filePath)
    {
        // For now, basic sanitization - remove absolute paths
        var fileName = Path.GetFileName(filePath);

        if (fileName.Equals("CLAUDE.md", StringComparison.OrdinalIgnoreCase))
        {
            // Remove any absolute file paths in CLAUDE.md
            content = Regex.Replace(content, @"[A-Z]:\\[^\\/:*?""<>|\r\n]+", "{{PROJECT_ROOT}}", RegexOptions.IgnoreCase);
        }

        // Remove any machine-specific paths in other files
        content = Regex.Replace(content, @"[A-Z]:\\(?:[^\\/:*?""<>|\r\n]+\\)*", "", RegexOptions.IgnoreCase);

        return content;
    }

    private async Task LogExportHistoryAsync(string templateName, string outputPath, int fileCount)
    {
        try
        {
            // Simple export history tracking - could be enhanced later
            var historyEntry = new
            {
                TemplateName = templateName,
                OutputPath = outputPath,
                FileCount = fileCount,
                ExportedAt = DateTime.UtcNow,
                Success = true
            };

            LoggingService.LogInfo($"Export history: {templateName} -> {outputPath} ({fileCount} files)", "TemplateService");

            // TODO: Add to database export_history table if needed
            // For now, just log the export
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to log export history", ex, "TemplateService");
            // Don't fail the export for logging issues
        }
    }

    public async Task<List<string>> PreviewFilesAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var filesToInclude = await GetFilesToIncludeAsync(projectPath, cancellationToken);
            return filesToInclude.Select(f => Path.GetRelativePath(projectPath, f)).ToList();
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Error previewing files for export", ex, "TemplateService");
            throw;
        }
    }
}

public class ExportProgress
{
    public string Stage { get; set; } = string.Empty;
    public int PercentComplete { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
}