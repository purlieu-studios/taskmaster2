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


    public async Task<string?> CallClaudeDirectAsync(string prompt)
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

        promptBuilder.AppendLine("You are an expert technical project manager creating comprehensive task specifications for software development.");
        promptBuilder.AppendLine("Your role is to transform minimal input into detailed, actionable specifications that developers can implement immediately.");
        promptBuilder.AppendLine();

        // Analyze the input quality and provide appropriate guidance
        var isSummaryMinimal = string.IsNullOrWhiteSpace(request.Summary) ||
                              request.Summary.Length < 20 ||
                              !request.Summary.Contains(' ') ||
                              request.Summary.All(c => char.IsLetterOrDigit(c));

        if (isSummaryMinimal)
        {
            promptBuilder.AppendLine("**IMPORTANT:** The provided summary appears minimal or incomplete. Please:");
            promptBuilder.AppendLine("1. Infer the likely intent from the title and project context");
            promptBuilder.AppendLine("2. Expand the summary into a comprehensive 2-3 sentence description");
            promptBuilder.AppendLine("3. Focus on what the user wants to achieve, not just what they typed");
            promptBuilder.AppendLine();
        }

        promptBuilder.AppendLine($"**Project:** {request.ProjectName}");
        promptBuilder.AppendLine($"**Title:** {request.Title}");
        promptBuilder.AppendLine($"**Summary (user input):** {request.Summary}");
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
                if (!string.IsNullOrWhiteSpace(task.Summary))
                    promptBuilder.AppendLine($"  Summary: {task.Summary}");
            }
            promptBuilder.AppendLine();
        }

        promptBuilder.AppendLine("**Expected JSON Response:**");
        promptBuilder.AppendLine("```json");
        promptBuilder.AppendLine("{");
        promptBuilder.AppendLine("  \"expandedSummary\": \"A comprehensive 2-3 sentence description of what this task achieves\",");
        promptBuilder.AppendLine("  \"type\": \"feature|bug|enhancement|refactor|docs|test\",");
        promptBuilder.AppendLine("  \"scopePaths\": [\"specific/file/paths.cs\", \"Views/ComponentName.xaml\"],");
        promptBuilder.AppendLine("  \"acceptanceCriteria\": [\"Specific, testable requirement\", \"Another measurable outcome\"],");
        promptBuilder.AppendLine("  \"testPlan\": [\"Manual test step\", \"Automated test requirement\"],");
        promptBuilder.AppendLine("  \"requiredDocs\": [\"Documentation that should be referenced or updated\"],");
        promptBuilder.AppendLine("  \"nonGoals\": \"What this task explicitly does NOT include (optional)\",");
        promptBuilder.AppendLine("  \"suggestedTasks\": [");
        promptBuilder.AppendLine("    {\"title\": \"Related task title\", \"summary\": \"Why this complements the current task\", \"type\": \"feature\"}");
        promptBuilder.AppendLine("  ],");
        promptBuilder.AppendLine("  \"nextSteps\": [");
        promptBuilder.AppendLine("    {\"title\": \"Logical follow-up\", \"summary\": \"What should happen after this task\", \"type\": \"enhancement\"}");
        promptBuilder.AppendLine("  ]");
        promptBuilder.AppendLine("}");
        promptBuilder.AppendLine("```");

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**Quality Standards:**");
        promptBuilder.AppendLine("- **expandedSummary**: Must be comprehensive, clear, and explain the business value");
        promptBuilder.AppendLine("- **scopePaths**: Use actual file/folder paths from the project context when possible");
        promptBuilder.AppendLine("- **acceptanceCriteria**: Each criterion must be specific, measurable, and testable");
        promptBuilder.AppendLine("- **testPlan**: Include both manual verification steps and automated test requirements");
        promptBuilder.AppendLine("- **suggestedTasks**: Focus on tasks that would logically complement or extend this work");
        promptBuilder.AppendLine("- **nextSteps**: Consider what developers would naturally want to do after completing this task");
        promptBuilder.AppendLine();

        promptBuilder.AppendLine("**Context Analysis Guidelines:**");
        promptBuilder.AppendLine("- Use the CLAUDE.md content to understand the project's architecture and conventions");
        promptBuilder.AppendLine("- Reference recent tasks to maintain consistency with ongoing work");
        promptBuilder.AppendLine("- Suggest file paths that align with the project's existing structure");
        promptBuilder.AppendLine("- Ensure all suggestions fit within the project's scope and technical stack");

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

    public async Task<bool> RunPanelAsync(string specPath, string repoRoot, string claudeMdPath)
    {
        return await RunGuardedClaudeCommandAsync("/panel-wpf-decide", specPath, repoRoot, claudeMdPath);
    }

    public async Task<bool> RunUpdateAsync(string specPath, string repoRoot, string claudeMdPath)
    {
        return await RunGuardedClaudeCommandAsync("/update-wpf-spec", specPath, repoRoot, claudeMdPath);
    }

    private async Task<bool> RunGuardedClaudeCommandAsync(string command, string specPath, string repoRoot, string claudeMdPath)
    {
        try
        {
            LoggingService.LogInfo($"Running guarded Claude command: {command} with spec: {specPath}", "ClaudeService");

            // Preflight validation
            var preflightResult = await GuardrailsService.ValidatePreflightChecksAsync(specPath, claudeMdPath, repoRoot);
            if (!preflightResult.IsValid)
            {
                LoggingService.LogError($"Preflight validation failed: {string.Join(", ", preflightResult.Errors)}", null, "ClaudeService");
                throw new InvalidOperationException($"Preflight validation failed: {string.Join(", ", preflightResult.Errors)}");
            }

            if (preflightResult.Warnings.Any())
            {
                LoggingService.LogWarning($"Preflight warnings: {string.Join(", ", preflightResult.Warnings)}", "ClaudeService");
            }

            // Build command with guardrail flags
            var guardedCommand = GuardrailsService.BuildGuardedCommand($"claude {command}", new[] { $"\"{specPath}\"" });

            LoggingService.LogInfo($"Executing guarded command: {guardedCommand}", "ClaudeService");

            var processInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {guardedCommand}",
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

            var stopwatch = Stopwatch.StartNew();
            using var process = Process.Start(processInfo);
            if (process == null)
            {
                LoggingService.LogError("Failed to start Claude CLI process for guarded command execution", null, "ClaudeService");
                return false;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();
            stopwatch.Stop();

            LoggingService.LogClaudeResponse(process.ExitCode, output, error, stopwatch.Elapsed);

            // Determine expected decision file path for panel commands
            string expectedDecisionPath = null;
            if (command.Contains("panel"))
            {
                var specFileName = Path.GetFileNameWithoutExtension(specPath);
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");

                // Extract slug from spec filename (remove date prefix if present)
                var slug = System.Text.RegularExpressions.Regex.Replace(specFileName, @"^\d{8}-", "");

                expectedDecisionPath = Path.Combine(repoRoot, "docs", "decisions", $"{timestamp}-{slug}.md");
            }

            // Postflight validation
            var postflightResult = GuardrailsService.ValidatePostflightResults(output, error, process.ExitCode, expectedDecisionPath);
            if (!postflightResult.IsValid)
            {
                LoggingService.LogError($"Postflight validation failed: {string.Join(", ", postflightResult.Issues)}", null, "ClaudeService");
            }
            else
            {
                LoggingService.LogInfo("Postflight validation passed", "ClaudeService");
                if (!string.IsNullOrEmpty(postflightResult.PrUrl))
                {
                    LoggingService.LogInfo($"PR created: {postflightResult.PrUrl}", "ClaudeService");
                }
                if (!string.IsNullOrEmpty(postflightResult.BranchName))
                {
                    LoggingService.LogInfo($"Branch created: {postflightResult.BranchName}", "ClaudeService");
                }
            }

            return postflightResult.CommandSucceeded;
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Exception running guarded Claude command: {command}", ex, "ClaudeService");
            throw;
        }
    }

    public async Task<PanelResult> RunPanelServiceAsync(string specPath, string repoRoot, int rounds = 2, string scope = "src/")
    {
        try
        {
            LoggingService.LogInfo($"Running panel service for spec: {specPath}", "ClaudeService");

            var panelService = new PanelService(this, repoRoot);
            var result = await panelService.RunPanelAsync(specPath, rounds, scope);

            if (result.Success)
            {
                LoggingService.LogInfo($"Panel service completed successfully. Decision: {result.DecisionPath}", "ClaudeService");
            }
            else
            {
                LoggingService.LogError($"Panel service failed: {result.ErrorMessage}", null, "ClaudeService");
            }

            return result;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Exception in RunPanelServiceAsync", ex, "ClaudeService");
            return new PanelResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

}