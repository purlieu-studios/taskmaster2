using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using TaskMaster.Models;

namespace TaskMaster.Services;

public class ProjectContextService
{
    private readonly DatabaseService _databaseService;
    private FileSystemWatcher? _fileWatcher;
    private readonly Dictionary<string, DateTime> _lastAnalyzed = new();
    private readonly SemaphoreSlim _analysisSemaphore = new(1, 1);

    // File patterns to include/exclude
    private readonly HashSet<string> _includeExtensions = new()
    {
        ".md", ".txt", ".json", ".xml", ".yaml", ".yml", ".config",
        ".cs", ".ts", ".js", ".py", ".java", ".cpp", ".h", ".hpp",
        ".html", ".css", ".scss", ".less", ".vue", ".tsx", ".jsx",
        ".csproj", ".sln", ".props", ".targets", ".nuspec",
        ".dockerfile", ".gitignore", ".editorconfig", ".gitattributes"
    };

    private readonly HashSet<string> _excludeDirectories = new()
    {
        "node_modules", "bin", "obj", ".git", ".vs", ".vscode",
        "packages", "dist", "build", "coverage", "logs", "temp",
        "__pycache__", ".pytest_cache", ".idea", "vendor"
    };

    private readonly HashSet<string> _excludeFiles = new()
    {
        ".DS_Store", "Thumbs.db", "desktop.ini", "*.log", "*.tmp"
    };

    public ProjectContextService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<ProjectAnalysisResult> AnalyzeProjectDirectoryAsync(int projectId, string rootPath,
        IProgress<ProjectAnalysisProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        LoggingService.LogInfo($"Starting project analysis for directory: {rootPath}", "ProjectContextService");

        await _analysisSemaphore.WaitAsync(cancellationToken);
        try
        {
            var result = new ProjectAnalysisResult();
            var analysisProgress = new ProjectAnalysisProgress();

            if (!Directory.Exists(rootPath))
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Directory does not exist: {rootPath}";
                return result;
            }

            // Phase 1: Discover files
            progress?.Report(analysisProgress with { Phase = "Discovering files...", Progress = 0 });
            var allFiles = await DiscoverFilesAsync(rootPath, cancellationToken);

            var relevantFiles = allFiles.Where(f => IsRelevantFile(f)).ToList();
            result.TotalFilesDiscovered = allFiles.Count;
            result.RelevantFilesFound = relevantFiles.Count;

            LoggingService.LogInfo($"Discovered {allFiles.Count} files, {relevantFiles.Count} relevant", "ProjectContextService");

            // Phase 2: Analyze each file
            var processed = 0;
            var contextEntries = new List<ProjectContext>();

            foreach (var filePath in relevantFiles)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var context = await AnalyzeFileAsync(projectId, filePath, rootPath);
                    if (context != null)
                    {
                        contextEntries.Add(context);
                        result.FilesAnalyzed++;
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"Failed to analyze file {filePath}: {ex.Message}", "ProjectContextService");
                    result.FilesSkipped++;
                }

                processed++;
                var progressPercent = (int)((double)processed / relevantFiles.Count * 100);
                progress?.Report(analysisProgress with
                {
                    Phase = $"Analyzing files... ({processed}/{relevantFiles.Count})",
                    Progress = progressPercent,
                    CurrentFile = Path.GetFileName(filePath)
                });
            }

            // Phase 3: Save to database
            progress?.Report(analysisProgress with { Phase = "Saving to database...", Progress = 90 });
            await SaveContextsToDatabase(projectId, contextEntries);

            // Phase 4: Build relationships
            progress?.Report(analysisProgress with { Phase = "Building relationships...", Progress = 95 });
            await BuildFileRelationships(contextEntries);

            result.IsSuccess = true;
            result.AnalysisCompletedAt = DateTime.UtcNow;

            progress?.Report(analysisProgress with { Phase = "Analysis complete!", Progress = 100 });
            LoggingService.LogInfo($"Project analysis completed: {result.FilesAnalyzed} files analyzed", "ProjectContextService");

            return result;
        }
        finally
        {
            _analysisSemaphore.Release();
        }
    }

    private async Task<List<string>> DiscoverFilesAsync(string rootPath, CancellationToken cancellationToken)
    {
        var files = new List<string>();
        var directories = new Queue<string>();
        directories.Enqueue(rootPath);

        while (directories.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            var currentDir = directories.Dequeue();

            try
            {
                var dirInfo = new DirectoryInfo(currentDir);
                if (_excludeDirectories.Contains(dirInfo.Name.ToLowerInvariant()))
                    continue;

                // Add subdirectories
                foreach (var subDir in Directory.GetDirectories(currentDir))
                {
                    directories.Enqueue(subDir);
                }

                // Add files
                foreach (var file in Directory.GetFiles(currentDir))
                {
                    files.Add(file);
                }
            }
            catch (UnauthorizedAccessException)
            {
                LoggingService.LogWarning($"Access denied to directory: {currentDir}", "ProjectContextService");
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"Error accessing directory {currentDir}: {ex.Message}", "ProjectContextService");
            }
        }

        return files;
    }

    private bool IsRelevantFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath).ToLowerInvariant();
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        // Check exclude patterns
        if (_excludeFiles.Any(pattern =>
            pattern.Contains('*') ? fileName.Contains(pattern.Replace("*", "")) : fileName == pattern))
        {
            return false;
        }

        // Check if extension is included
        if (!_includeExtensions.Contains(extension))
        {
            return false;
        }

        // Check file size (skip very large files)
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > 5 * 1024 * 1024) // 5MB limit
            {
                return false;
            }
        }
        catch
        {
            return false;
        }

        return true;
    }

    private async Task<ProjectContext?> AnalyzeFileAsync(int projectId, string filePath, string rootPath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
                return null;

            var relativePath = Path.GetRelativePath(rootPath, filePath);
            var content = await ReadFileContentSafelyAsync(filePath);
            var fileType = DetermineFileType(filePath, content);
            var hash = CalculateFileHash(filePath);

            var context = new ProjectContext
            {
                ProjectId = projectId,
                FilePath = relativePath,
                FileType = fileType.ToString(),
                Content = content,
                LastAnalyzed = DateTime.UtcNow,
                FileSize = fileInfo.Length,
                LastModified = fileInfo.LastWriteTimeUtc,
                FileHash = hash,
                IsRelevant = true,
                RelevanceScore = CalculateRelevanceScore(filePath, content, fileType)
            };

            return context;
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Error analyzing file {filePath}", ex, "ProjectContextService");
            return null;
        }
    }

    private async Task<string?> ReadFileContentSafelyAsync(string filePath)
    {
        try
        {
            // Detect encoding and read content
            var bytes = await File.ReadAllBytesAsync(filePath);

            // Check if it's a binary file
            if (IsBinaryFile(bytes))
            {
                return $"[Binary file - {bytes.Length} bytes]";
            }

            // Try to read as text
            var content = Encoding.UTF8.GetString(bytes);

            // Truncate very long content
            if (content.Length > 50000) // 50KB text limit
            {
                content = content[..50000] + "\n[... content truncated ...]";
            }

            return content;
        }
        catch
        {
            return "[Unable to read file content]";
        }
    }

    private bool IsBinaryFile(byte[] bytes)
    {
        // Simple heuristic: if file contains null bytes in first 1024 bytes, it's likely binary
        var checkLength = Math.Min(1024, bytes.Length);
        for (int i = 0; i < checkLength; i++)
        {
            if (bytes[i] == 0)
                return true;
        }
        return false;
    }

    private FileType DetermineFileType(string filePath, string? content)
    {
        var fileName = Path.GetFileName(filePath).ToLowerInvariant();
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        // Documentation files
        if (fileName.Contains("readme") || fileName.Contains("changelog") ||
            fileName.Contains("license") || extension == ".md" || extension == ".txt")
        {
            return FileType.Documentation;
        }

        // Configuration files
        if (fileName == "package.json" || extension == ".csproj" || extension == ".sln" ||
            extension == ".config" || extension == ".xml" || extension == ".json" ||
            extension == ".yaml" || extension == ".yml" || fileName.Contains("dockerfile"))
        {
            return FileType.Configuration;
        }

        // Source code files
        if (extension == ".cs" || extension == ".ts" || extension == ".js" ||
            extension == ".py" || extension == ".java" || extension == ".cpp" ||
            extension == ".h" || extension == ".hpp")
        {
            return FileType.Source;
        }

        // Test files
        if (fileName.Contains("test") || fileName.Contains("spec") ||
            filePath.Contains("/test/") || filePath.Contains("\\test\\"))
        {
            return FileType.Test;
        }

        // Build files
        if (fileName.Contains("build") || fileName.Contains("make") ||
            extension == ".props" || extension == ".targets")
        {
            return FileType.Build;
        }

        // Web assets
        if (extension == ".html" || extension == ".css" || extension == ".scss")
        {
            return FileType.Asset;
        }

        return FileType.Unknown;
    }

    private string CalculateFileHash(string filePath)
    {
        try
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            var hash = md5.ComputeHash(stream);
            return Convert.ToHexString(hash);
        }
        catch
        {
            return string.Empty;
        }
    }

    private int CalculateRelevanceScore(string filePath, string? content, FileType fileType)
    {
        var score = 50; // Base score

        // File type scoring
        score += fileType switch
        {
            FileType.Documentation => 30,
            FileType.Configuration => 25,
            FileType.Source => 20,
            FileType.Test => 15,
            FileType.Build => 10,
            _ => 0
        };

        // Path-based scoring
        if (filePath.Contains("README", StringComparison.OrdinalIgnoreCase))
            score += 20;
        if (filePath.Contains("package.json", StringComparison.OrdinalIgnoreCase))
            score += 15;
        if (filePath.Contains(".csproj") || filePath.Contains(".sln"))
            score += 15;

        // Content-based scoring
        if (!string.IsNullOrEmpty(content))
        {
            if (content.Length > 1000) // Substantial content
                score += 10;
            if (content.Contains("TODO", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("FIXME", StringComparison.OrdinalIgnoreCase))
                score += 5;
        }

        return Math.Clamp(score, 0, 100);
    }

    private async Task SaveContextsToDatabase(int projectId, List<ProjectContext> contexts)
    {
        // First, clear existing contexts for this project
        await _databaseService.ClearProjectContextAsync(projectId);

        // Save new contexts
        foreach (var context in contexts)
        {
            await _databaseService.SaveProjectContextAsync(context);
        }

        LoggingService.LogInfo($"Saved {contexts.Count} context entries to database", "ProjectContextService");
    }

    private async Task BuildFileRelationships(List<ProjectContext> contexts)
    {
        // Build relationships based on file content and structure
        foreach (var context in contexts)
        {
            var relationships = new List<string>();

            // Find files that reference this file
            var fileName = Path.GetFileNameWithoutExtension(context.FilePath);
            foreach (var other in contexts)
            {
                if (other.FilePath != context.FilePath &&
                    !string.IsNullOrEmpty(other.Content) &&
                    other.Content.Contains(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    relationships.Add(other.FilePath);
                }
            }

            // Save relationships as JSON
            if (relationships.Any())
            {
                context.Relationships = JsonSerializer.Serialize(relationships);
            }
        }

        LoggingService.LogInfo("File relationships built", "ProjectContextService");
    }

    public void StartFileWatching(string rootPath, int projectId)
    {
        StopFileWatching();

        _fileWatcher = new FileSystemWatcher(rootPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
        };

        _fileWatcher.Changed += (sender, e) => OnFileChanged(e, projectId, rootPath);
        _fileWatcher.Created += (sender, e) => OnFileChanged(e, projectId, rootPath);
        _fileWatcher.Deleted += (sender, e) => OnFileDeleted(e, projectId);
        _fileWatcher.Renamed += (sender, e) => OnFileRenamed(e, projectId, rootPath);

        _fileWatcher.EnableRaisingEvents = true;
        LoggingService.LogInfo($"Started file watching for: {rootPath}", "ProjectContextService");
    }

    public void StopFileWatching()
    {
        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Dispose();
            _fileWatcher = null;
            LoggingService.LogInfo("Stopped file watching", "ProjectContextService");
        }
    }

    private async void OnFileChanged(FileSystemEventArgs e, int projectId, string rootPath)
    {
        if (!IsRelevantFile(e.FullPath))
            return;

        // Debounce rapid file changes
        var key = e.FullPath;
        var now = DateTime.UtcNow;
        if (_lastAnalyzed.ContainsKey(key) && (now - _lastAnalyzed[key]).TotalSeconds < 2)
            return;

        _lastAnalyzed[key] = now;

        try
        {
            var context = await AnalyzeFileAsync(projectId, e.FullPath, rootPath);
            if (context != null)
            {
                await _databaseService.SaveProjectContextAsync(context);
                LoggingService.LogInfo($"Updated context for file: {e.Name}", "ProjectContextService");
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Error updating context for file {e.FullPath}", ex, "ProjectContextService");
        }
    }

    private async void OnFileDeleted(FileSystemEventArgs e, int projectId)
    {
        try
        {
            var relativePath = Path.GetFileName(e.FullPath);
            await _databaseService.DeleteProjectContextByPathAsync(projectId, relativePath);
            LoggingService.LogInfo($"Deleted context for file: {e.Name}", "ProjectContextService");
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Error deleting context for file {e.FullPath}", ex, "ProjectContextService");
        }
    }

    private async void OnFileRenamed(RenamedEventArgs e, int projectId, string rootPath)
    {
        try
        {
            // Delete old context
            var oldRelativePath = Path.GetRelativePath(rootPath, e.OldFullPath);
            await _databaseService.DeleteProjectContextByPathAsync(projectId, oldRelativePath);

            // Create new context if relevant
            if (IsRelevantFile(e.FullPath))
            {
                var context = await AnalyzeFileAsync(projectId, e.FullPath, rootPath);
                if (context != null)
                {
                    await _databaseService.SaveProjectContextAsync(context);
                }
            }

            LoggingService.LogInfo($"Renamed context: {e.OldName} -> {e.Name}", "ProjectContextService");
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Error handling file rename {e.OldFullPath} -> {e.FullPath}", ex, "ProjectContextService");
        }
    }

    public async Task<string> GetEnhancedProjectContextAsync(int projectId, int maxContextLength = 50000)
    {
        var contexts = await _databaseService.GetProjectContextsAsync(projectId);

        if (!contexts.Any())
        {
            return "No project context available. Please analyze the project directory first.";
        }

        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("# Project Context Analysis");
        contextBuilder.AppendLine();

        // Group by file type for organized output
        var groupedContexts = contexts
            .Where(c => c.IsRelevant)
            .OrderByDescending(c => c.RelevanceScore)
            .GroupBy(c => c.FileType)
            .OrderBy(g => GetFileTypeOrder(g.Key));

        var currentLength = 0;

        foreach (var group in groupedContexts)
        {
            if (currentLength > maxContextLength)
                break;

            contextBuilder.AppendLine($"## {group.Key} Files");
            contextBuilder.AppendLine();

            foreach (var context in group.Take(10)) // Limit per type
            {
                if (currentLength > maxContextLength)
                    break;

                var section = BuildContextSection(context);
                if (currentLength + section.Length > maxContextLength)
                    break;

                contextBuilder.Append(section);
                currentLength += section.Length;
            }

            contextBuilder.AppendLine();
        }

        return contextBuilder.ToString();
    }

    private string BuildContextSection(ProjectContext context)
    {
        var section = new StringBuilder();
        section.AppendLine($"### {context.FilePath}");
        section.AppendLine($"**Type:** {context.FileType} | **Relevance:** {context.RelevanceScore}/100");
        section.AppendLine();

        if (!string.IsNullOrEmpty(context.Content))
        {
            var content = context.Content;
            if (content.Length > 2000) // Truncate long content
            {
                content = content[..2000] + "\n[... truncated ...]";
            }
            section.AppendLine("```");
            section.AppendLine(content);
            section.AppendLine("```");
        }

        section.AppendLine();
        return section.ToString();
    }

    private int GetFileTypeOrder(string fileType)
    {
        return fileType switch
        {
            nameof(FileType.Documentation) => 1,
            nameof(FileType.Configuration) => 2,
            nameof(FileType.Source) => 3,
            nameof(FileType.Test) => 4,
            nameof(FileType.Build) => 5,
            nameof(FileType.Asset) => 6,
            _ => 7
        };
    }

    public void Dispose()
    {
        StopFileWatching();
        _analysisSemaphore?.Dispose();
    }
}

public record ProjectAnalysisProgress(
    string Phase = "",
    int Progress = 0,
    string CurrentFile = ""
);

public class ProjectAnalysisResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int TotalFilesDiscovered { get; set; }
    public int RelevantFilesFound { get; set; }
    public int FilesAnalyzed { get; set; }
    public int FilesSkipped { get; set; }
    public DateTime AnalysisCompletedAt { get; set; }
}