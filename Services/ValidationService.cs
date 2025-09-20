using Newtonsoft.Json;
using System.IO;

namespace TaskMaster.Services;

public static class ValidationService
{
    public static List<string> ValidateTaskSpecInput(string title, string summary, string? claudeMdPath)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(title))
        {
            errors.Add("Title is required");
        }
        else if (title.Length > 200)
        {
            errors.Add("Title must be 200 characters or less");
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            errors.Add("Summary is required");
        }
        else if (summary.Length < 10)
        {
            errors.Add("Summary must be at least 10 characters");
        }

        if (!string.IsNullOrWhiteSpace(claudeMdPath) && !File.Exists(claudeMdPath))
        {
            errors.Add("CLAUDE.md file does not exist at the specified path");
        }

        return errors;
    }

    public static bool IsValidJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            JsonConvert.DeserializeObject(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsValidJsonArray(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            var obj = JsonConvert.DeserializeObject(json);
            return obj is Newtonsoft.Json.Linq.JArray;
        }
        catch
        {
            return false;
        }
    }

    public static List<string> ValidateInferredFields(string scopePaths, string acceptanceCriteria,
        string testPlan, string requiredDocs)
    {
        var errors = new List<string>();

        if (!IsValidJsonArray(scopePaths))
        {
            errors.Add("Scope Paths must be a valid JSON array");
        }

        if (!IsValidJsonArray(acceptanceCriteria))
        {
            errors.Add("Acceptance Criteria must be a valid JSON array");
        }

        if (!IsValidJsonArray(testPlan))
        {
            errors.Add("Test Plan must be a valid JSON array");
        }

        if (!IsValidJsonArray(requiredDocs))
        {
            errors.Add("Required Docs must be a valid JSON array");
        }

        return errors;
    }

    public static bool ValidateClaudeCliAvailable()
    {
        try
        {
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "claude",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(processInfo);
            return process != null && process.WaitForExit(5000) && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool ValidateGitRepository(string repoRoot)
    {
        if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
            return false;

        var gitDir = Path.Combine(repoRoot, ".git");
        return Directory.Exists(gitDir) || File.Exists(gitDir); // .git can be a file in worktrees
    }
}