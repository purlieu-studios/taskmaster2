using System.IO.Compression;
using TaskMaster.Services;
using Xunit;

namespace TaskMaster.Tests;

public class TemplateServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly TemplateService _templateService;
    private readonly DatabaseService _mockDatabaseService;

    public TemplateServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "TaskMasterTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);

        // Create mock database service (in real implementation, would use a proper mock)
        _mockDatabaseService = new DatabaseService();
        _templateService = new TemplateService(_mockDatabaseService);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task ExportProjectTemplateAsync_ValidProject_CreatesZipFile()
    {
        // Arrange
        var projectPath = CreateTestProject();
        var outputPath = Path.Combine(_testDirectory, "test-template.zip");

        // Act
        var result = await _templateService.ExportProjectTemplateAsync(
            projectPath,
            outputPath,
            "Test Template");

        // Assert
        Assert.True(File.Exists(outputPath));
        Assert.Equal(outputPath, result);

        // Verify zip contents
        using var archive = ZipFile.OpenRead(outputPath);
        var entries = archive.Entries.Select(e => e.FullName).ToList();

        Assert.Contains("CLAUDE.md", entries);
        Assert.Contains("TestFile.cs", entries);
        Assert.Contains(".claude/test.md", entries);
        Assert.DoesNotContain("bin/Debug/test.exe", entries); // Should be excluded
    }

    [Fact]
    public async Task ExportProjectTemplateAsync_InvalidPath_ThrowsArgumentException()
    {
        // Arrange
        var invalidPath = Path.Combine(_testDirectory, "nonexistent");
        var outputPath = Path.Combine(_testDirectory, "test.zip");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _templateService.ExportProjectTemplateAsync(invalidPath, outputPath, "Test"));
    }

    [Fact]
    public async Task PreviewFilesAsync_ValidProject_ReturnsFilteredFiles()
    {
        // Arrange
        var projectPath = CreateTestProject();

        // Act
        var files = await _templateService.PreviewFilesAsync(projectPath);

        // Assert
        Assert.NotEmpty(files);
        Assert.Contains(files, f => f.Contains("CLAUDE.md"));
        Assert.Contains(files, f => f.Contains("TestFile.cs"));
        Assert.Contains(files, f => f.Contains(".claude"));
        Assert.DoesNotContain(files, f => f.Contains("bin"));
        Assert.DoesNotContain(files, f => f.Contains("obj"));
    }

    [Fact]
    public async Task ExportProjectTemplateAsync_WithProgress_ReportsProgress()
    {
        // Arrange
        var projectPath = CreateTestProject();
        var outputPath = Path.Combine(_testDirectory, "progress-test.zip");
        var progressReports = new List<ExportProgress>();
        var progress = new Progress<ExportProgress>(p => progressReports.Add(p));

        // Act
        await _templateService.ExportProjectTemplateAsync(
            projectPath,
            outputPath,
            "Progress Test",
            progress);

        // Assert
        Assert.NotEmpty(progressReports);
        Assert.Contains(progressReports, p => p.Stage.Contains("Scanning"));
        Assert.Contains(progressReports, p => p.Stage.Contains("Creating"));
        Assert.True(progressReports.Any(p => p.PercentComplete > 0));
    }

    [Fact]
    public async Task ExportProjectTemplateAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var projectPath = CreateTestProject();
        var outputPath = Path.Combine(_testDirectory, "cancelled.zip");
        using var cts = new CancellationTokenSource();

        // Act & Assert
        var task = _templateService.ExportProjectTemplateAsync(
            projectPath,
            outputPath,
            "Cancelled Test",
            cancellationToken: cts.Token);

        cts.Cancel(); // Cancel immediately

        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
    }

    private string CreateTestProject()
    {
        var projectPath = Path.Combine(_testDirectory, "TestProject");
        Directory.CreateDirectory(projectPath);

        // Create CLAUDE.md
        File.WriteAllText(
            Path.Combine(projectPath, "CLAUDE.md"),
            "# Test Project\nThis is a test project for template export.");

        // Create source file
        File.WriteAllText(
            Path.Combine(projectPath, "TestFile.cs"),
            "using System;\nnamespace Test { public class TestClass { } }");

        // Create .claude directory
        var claudeDir = Path.Combine(projectPath, ".claude");
        Directory.CreateDirectory(claudeDir);
        File.WriteAllText(
            Path.Combine(claudeDir, "test.md"),
            "# Test Claude Configuration");

        // Create directories that should be excluded
        var binDir = Path.Combine(projectPath, "bin", "Debug");
        Directory.CreateDirectory(binDir);
        File.WriteAllText(Path.Combine(binDir, "test.exe"), "fake executable");

        var objDir = Path.Combine(projectPath, "obj");
        Directory.CreateDirectory(objDir);
        File.WriteAllText(Path.Combine(objDir, "test.cache"), "cache file");

        return projectPath;
    }
}

// Simple mock for testing - in real project would use proper mocking framework
public class MockDatabaseService : DatabaseService
{
    // Override methods as needed for testing
}