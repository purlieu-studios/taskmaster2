using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;
using TaskMaster.Models;

namespace TaskMaster.Services;

public class ClaudeService
{
    public ClaudeService()
    {
        // Log system info on first use
        LoggingService.LogSystemInfo();
    }

    public async Task<ClaudeInferenceResponse?> InferSpecFieldsAsync(ClaudeInferenceRequest request)
    {
        LoggingService.LogInfo($"Starting inference for task: {request.Title}", "ClaudeService");

        try
        {
            // First check if Claude CLI is available
            if (!await IsClaudeAvailableAsync())
            {
                throw new InvalidOperationException("Claude CLI is not available or not properly configured");
            }

            // Use direct prompt approach
            LoggingService.LogInfo("Calling Claude with inference prompt", "ClaudeService");
            var prompt = BuildInferencePrompt(request);
            var response = await CallClaudeDirectAsync(prompt);

            if (response != null)
            {
                var parsed = ParseInferenceResponse(response);
                if (parsed != null)
                {
                    LoggingService.LogInfo("Inference successful", "ClaudeService");
                    return parsed;
                }
                else
                {
                    LoggingService.LogWarning("Failed to parse Claude response as JSON", "ClaudeService");
                }
            }

            LoggingService.LogError("Inference failed - no valid response from Claude", null, "ClaudeService");
            return null;
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Error in InferSpecFieldsAsync for task '{request.Title}'", ex, "ClaudeService");
            throw; // Re-throw to let the UI handle it
        }
    }


    private async Task<bool> IsClaudeAvailableAsync()
    {
        try
        {
            LoggingService.LogInfo("Checking Claude CLI availability", "ClaudeService");

            var processInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c claude --version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Copy environment variables to ensure PATH is inherited
            foreach (string key in Environment.GetEnvironmentVariables().Keys)
            {
                processInfo.EnvironmentVariables[key] = Environment.GetEnvironmentVariable(key) ?? "";
            }

            var stopwatch = Stopwatch.StartNew();
            using var process = Process.Start(processInfo);
            if (process == null)
            {
                LoggingService.LogError("Failed to start Claude CLI process for availability check", null, "ClaudeService");
                return false;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();
            stopwatch.Stop();

            LoggingService.LogClaudeResponse(process.ExitCode, output, error, stopwatch.Elapsed);

            if (process.ExitCode == 0)
            {
                LoggingService.LogInfo($"Claude CLI is available: {output.Trim()}", "ClaudeService");
                return true;
            }
            else
            {
                LoggingService.LogError($"Claude CLI availability check failed with exit code {process.ExitCode}", null, "ClaudeService");
                return false;
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Exception during Claude CLI availability check", ex, "ClaudeService");
            return false;
        }
    }


    private async Task<string?> CallClaudeDirectAsync(string prompt)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            LoggingService.LogInfo($"Calling Claude via stdin with prompt ({prompt.Length} characters)", "ClaudeService");

            var processInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c claude --print",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Copy environment variables to ensure PATH is inherited
            foreach (string key in Environment.GetEnvironmentVariables().Keys)
            {
                processInfo.EnvironmentVariables[key] = Environment.GetEnvironmentVariable(key) ?? "";
            }

            LoggingService.LogInfo($"Executing: cmd.exe /c claude --print (stdin input: {prompt.Length} characters)", "ClaudeService");

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start Claude CLI process");
            }

            // Write prompt to stdin and close it
            await process.StandardInput.WriteAsync(prompt);
            process.StandardInput.Close();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();
            stopwatch.Stop();

            LoggingService.LogClaudeResponse(process.ExitCode, output, error, stopwatch.Elapsed);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Claude CLI failed with exit code {process.ExitCode}: {error}");
            }

            return output;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LoggingService.LogError("Error calling Claude via stdin", ex, "ClaudeService");
            throw;
        }
    }

    private string BuildInferencePrompt(ClaudeInferenceRequest request)
    {
        var promptBuilder = new StringBuilder();

        promptBuilder.AppendLine("You are a technical project manager helping to create detailed task specifications.");
        promptBuilder.AppendLine("Based on the provided information, infer the missing fields for a task specification.");
        promptBuilder.AppendLine();

        promptBuilder.AppendLine($"**Project:** {request.ProjectName}");
        promptBuilder.AppendLine($"**Title:** {request.Title}");
        promptBuilder.AppendLine($"**Summary:** {request.Summary}");
        promptBuilder.AppendLine();

        if (!string.IsNullOrEmpty(request.ClaudeMdContent))
        {
            promptBuilder.AppendLine("**Project Context (CLAUDE.md):**");
            promptBuilder.AppendLine("```");
            promptBuilder.AppendLine(request.ClaudeMdContent);
            promptBuilder.AppendLine("```");
            promptBuilder.AppendLine();
        }

        if (request.RecentTasks.Any())
        {
            promptBuilder.AppendLine("**Recent Tasks in Project:**");
            foreach (var task in request.RecentTasks.Take(5))
            {
                promptBuilder.AppendLine($"- #{task.Number}: {task.Title} ({task.Type})");
                promptBuilder.AppendLine($"  Summary: {task.Summary}");
            }
            promptBuilder.AppendLine();
        }

        promptBuilder.AppendLine("Please provide a JSON response with the following structure:");
        promptBuilder.AppendLine("```json");
        promptBuilder.AppendLine("{");
        promptBuilder.AppendLine("  \"type\": \"feature|bug|enhancement|refactor|docs\",");
        promptBuilder.AppendLine("  \"scopePaths\": [\"path/to/file1\", \"path/to/file2\"],");
        promptBuilder.AppendLine("  \"acceptanceCriteria\": [\"criterion 1\", \"criterion 2\"],");
        promptBuilder.AppendLine("  \"testPlan\": [\"test step 1\", \"test step 2\"],");
        promptBuilder.AppendLine("  \"requiredDocs\": [\"doc1.md\", \"doc2.md\"],");
        promptBuilder.AppendLine("  \"nonGoals\": \"Optional: what this task explicitly does NOT include\",");
        promptBuilder.AppendLine("  \"suggestedTasks\": [");
        promptBuilder.AppendLine("    {\"title\": \"Task title\", \"summary\": \"Brief description\", \"type\": \"feature\"}");
        promptBuilder.AppendLine("  ],");
        promptBuilder.AppendLine("  \"nextSteps\": [");
        promptBuilder.AppendLine("    {\"title\": \"Next step title\", \"summary\": \"Brief description\", \"type\": \"feature\"}");
        promptBuilder.AppendLine("  ]");
        promptBuilder.AppendLine("}");
        promptBuilder.AppendLine("```");

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Guidelines:");
        promptBuilder.AppendLine("- Be specific and actionable in acceptance criteria");
        promptBuilder.AppendLine("- Include relevant file paths in scopePaths based on the project context");
        promptBuilder.AppendLine("- Suggest 2-3 related tasks that would complement this one");
        promptBuilder.AppendLine("- Suggest 1-2 logical next steps after this task is completed");
        promptBuilder.AppendLine("- Keep suggestions focused on the project domain");

        return promptBuilder.ToString();
    }

    private ClaudeInferenceResponse? ParseInferenceResponse(string response)
    {
        try
        {
            // Find JSON in the response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                return JsonConvert.DeserializeObject<ClaudeInferenceResponse>(json);
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing Claude response: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> RunPanelAsync(string specPath, string repoRoot)
    {
        return await RunClaudeCommandAsync("/panel-wpf-decide", specPath, repoRoot);
    }

    public async Task<bool> RunUpdateAsync(string specPath, string repoRoot)
    {
        return await RunClaudeCommandAsync("/update-wpf-spec", specPath, repoRoot);
    }

    private async Task<bool> RunClaudeCommandAsync(string command, string specPath, string repoRoot)
    {
        try
        {
            LoggingService.LogInfo($"Running Claude command: {command} with spec: {specPath}", "ClaudeService");

            var processInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c claude {command} \"{specPath}\"",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Copy environment variables to ensure PATH is inherited
            foreach (string key in Environment.GetEnvironmentVariables().Keys)
            {
                processInfo.EnvironmentVariables[key] = Environment.GetEnvironmentVariable(key) ?? "";
            }

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                LoggingService.LogError("Failed to start Claude CLI process for command execution", null, "ClaudeService");
                return false;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            LoggingService.LogInfo($"Claude command completed with exit code: {process.ExitCode}", "ClaudeService");

            if (process.ExitCode != 0)
            {
                LoggingService.LogError($"Claude command failed: {error}", null, "ClaudeService");
            }
            else if (!string.IsNullOrEmpty(output))
            {
                LoggingService.LogInfo($"Claude command output: {output}", "ClaudeService");
            }

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Exception running Claude command: {command}", ex, "ClaudeService");
            return false;
        }
    }
}