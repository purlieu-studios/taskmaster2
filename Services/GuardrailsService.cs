using System.Text;
using TaskMaster.Models;

namespace TaskMaster.Services;

public static class GuardrailsService
{
    public static readonly string[] DefaultFlags =
    {
        "--dry-run",
        "--small-commits",
        "--no-force",
        "--max-diff-lines=200"
    };

    public static readonly string[] RequiredPreflightChecks =
    {
        "git_installed",
        "clean_working_tree",
        "remote_origin_exists",
        "claude_authenticated",
        "spec_file_exists",
        "claude_md_exists"
    };

    public static string BuildGuardedCommand(string baseCommand, string[]? additionalFlags = null)
    {
        var flagsBuilder = new StringBuilder();

        foreach (var flag in DefaultFlags)
        {
            flagsBuilder.Append($" {flag}");
        }

        if (additionalFlags != null)
        {
            foreach (var flag in additionalFlags)
            {
                flagsBuilder.Append($" {flag}");
            }
        }

        return $"{baseCommand}{flagsBuilder}";
    }

    public static async Task<PreflightResult> ValidatePreflightChecksAsync(string specPath, string claudeMdPath, string repoRoot)
    {
        var result = new PreflightResult();

        try
        {
            LoggingService.LogInfo("Starting preflight validation", "GuardrailsService");

            // Check git installation
            result.GitInstalled = await CheckGitInstalledAsync();
            if (!result.GitInstalled)
            {
                result.Errors.Add("Git is not installed or not in PATH");
            }

            // Check clean working tree
            if (result.GitInstalled)
            {
                result.CleanWorkingTree = await CheckCleanWorkingTreeAsync(repoRoot);
                if (!result.CleanWorkingTree)
                {
                    result.Warnings.Add("Working tree has uncommitted changes - consider stashing");
                }
            }

            // Check remote origin
            if (result.GitInstalled)
            {
                result.RemoteOriginExists = await CheckRemoteOriginAsync(repoRoot);
                if (!result.RemoteOriginExists)
                {
                    result.Errors.Add("Remote 'origin' is not configured");
                }
            }

            // Check Claude authentication
            result.ClaudeAuthenticated = await CheckClaudeAuthAsync();
            if (!result.ClaudeAuthenticated)
            {
                result.Errors.Add("Claude CLI is not authenticated - run 'claude login'");
            }

            // Check spec file exists
            result.SpecFileExists = File.Exists(specPath);
            if (!result.SpecFileExists)
            {
                result.Errors.Add($"Spec file does not exist: {specPath}");
            }

            // Check CLAUDE.md exists
            result.ClaudeMdExists = File.Exists(claudeMdPath);
            if (!result.ClaudeMdExists)
            {
                result.Errors.Add($"CLAUDE.md file does not exist: {claudeMdPath}");
            }

            result.IsValid = result.Errors.Count == 0;

            LoggingService.LogInfo($"Preflight validation completed - Valid: {result.IsValid}, Errors: {result.Errors.Count}, Warnings: {result.Warnings.Count}", "GuardrailsService");

            return result;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Exception during preflight validation", ex, "GuardrailsService");
            result.Errors.Add($"Preflight validation failed: {ex.Message}");
            result.IsValid = false;
            return result;
        }
    }

    public static PostflightResult ValidatePostflightResults(string output, string error, int exitCode, string? expectedDecisionPath = null)
    {
        var result = new PostflightResult
        {
            ExitCode = exitCode,
            Output = output,
            Error = error
        };

        try
        {
            LoggingService.LogInfo($"Starting postflight validation - Exit Code: {exitCode}", "GuardrailsService");

            // Check exit code
            result.CommandSucceeded = exitCode == 0;
            if (!result.CommandSucceeded)
            {
                result.Issues.Add($"Command failed with exit code {exitCode}");
            }

            // Check for deterministic outputs
            if (result.CommandSucceeded)
            {
                // Look for decision file creation
                if (!string.IsNullOrEmpty(expectedDecisionPath))
                {
                    result.DecisionFileCreated = File.Exists(expectedDecisionPath);
                    if (!result.DecisionFileCreated)
                    {
                        result.Issues.Add($"Expected decision file not created: {expectedDecisionPath}");
                    }
                }

                // Parse PR URL from output
                result.PrUrl = ExtractPrUrlFromOutput(output);
                result.PrCreated = !string.IsNullOrEmpty(result.PrUrl);

                // Check for branch creation
                var branchMatch = System.Text.RegularExpressions.Regex.Match(output, @"branch[:\s]+([^\s]+)");
                if (branchMatch.Success)
                {
                    result.BranchName = branchMatch.Groups[1].Value;
                    result.BranchCreated = true;
                }

                // Validate output format
                result.OutputFormatValid = ValidateOutputFormat(output);
                if (!result.OutputFormatValid)
                {
                    result.Issues.Add("Output format does not match expected deterministic structure");
                }
            }

            result.IsValid = result.CommandSucceeded && result.Issues.Count == 0;

            LoggingService.LogInfo($"Postflight validation completed - Valid: {result.IsValid}, Issues: {result.Issues.Count}", "GuardrailsService");

            return result;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Exception during postflight validation", ex, "GuardrailsService");
            result.Issues.Add($"Postflight validation failed: {ex.Message}");
            result.IsValid = false;
            return result;
        }
    }

    private static async Task<bool> CheckGitInstalledAsync()
    {
        try
        {
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c git --version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(processInfo);
            if (process == null) return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> CheckCleanWorkingTreeAsync(string repoRoot)
    {
        try
        {
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c git status --porcelain",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(processInfo);
            if (process == null) return false;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0 && string.IsNullOrWhiteSpace(output);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> CheckRemoteOriginAsync(string repoRoot)
    {
        try
        {
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c git remote get-url origin",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(processInfo);
            if (process == null) return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> CheckClaudeAuthAsync()
    {
        try
        {
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c claude --print \"test\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(processInfo);
            if (process == null) return false;

            await process.StandardInput.WriteAsync("test");
            process.StandardInput.Close();

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string ExtractPrUrlFromOutput(string output)
    {
        var prUrlMatch = System.Text.RegularExpressions.Regex.Match(output, @"https://github\.com/[^\s]+/pull/\d+");
        return prUrlMatch.Success ? prUrlMatch.Value : null;
    }

    private static bool ValidateOutputFormat(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return false;

        var requiredPatterns = new[]
        {
            @"(?i)(branch|created|checkout)",
            @"(?i)(commit|committed)",
            @"(?i)(push|pushed)"
        };

        return requiredPatterns.All(pattern =>
            System.Text.RegularExpressions.Regex.IsMatch(output, pattern));
    }
}