namespace TaskMaster.Models;

public class PreflightResult
{
    public bool IsValid { get; set; }
    public bool GitInstalled { get; set; }
    public bool CleanWorkingTree { get; set; }
    public bool RemoteOriginExists { get; set; }
    public bool ClaudeAuthenticated { get; set; }
    public bool SpecFileExists { get; set; }
    public bool ClaudeMdExists { get; set; }

    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
}

public class PostflightResult
{
    public bool IsValid { get; set; }
    public bool CommandSucceeded { get; set; }
    public bool DecisionFileCreated { get; set; }
    public bool BranchCreated { get; set; }
    public bool PrCreated { get; set; }
    public bool OutputFormatValid { get; set; }

    public int ExitCode { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string PrUrl { get; set; } = string.Empty;

    public List<string> Issues { get; set; } = new List<string>();
}