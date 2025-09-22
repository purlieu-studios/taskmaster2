using System.ComponentModel.DataAnnotations;

namespace TaskMaster.Models;

public class ProjectContext
{
    public int Id { get; set; }

    [Required]
    public int ProjectId { get; set; }

    [Required]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    public string FileType { get; set; } = string.Empty;

    public string? Content { get; set; }

    public DateTime LastAnalyzed { get; set; } = DateTime.UtcNow;

    public string? Relationships { get; set; } // JSON array of related files

    public long FileSize { get; set; }

    public DateTime LastModified { get; set; }

    public string? FileHash { get; set; } // For change detection

    public bool IsRelevant { get; set; } = true; // For filtering

    public int RelevanceScore { get; set; } = 50; // 0-100, used for prioritization

    // Navigation properties
    public Project Project { get; set; } = null!;
}

public enum FileType
{
    Unknown,
    Documentation,
    Configuration,
    Source,
    Test,
    Build,
    Data,
    Asset
}

public enum DocumentationType
{
    ReadMe,
    ChangeLog,
    License,
    Contributing,
    CodeOfConduct,
    Api,
    Tutorial,
    Wiki
}

public enum ConfigurationType
{
    PackageJson,
    ProjectFile,
    Solution,
    Config,
    Environment,
    Docker,
    GitIgnore,
    EditorConfig
}

public enum SourceType
{
    CSharp,
    TypeScript,
    JavaScript,
    Python,
    Java,
    Cpp,
    Html,
    Css,
    Xml,
    Json,
    Yaml
}