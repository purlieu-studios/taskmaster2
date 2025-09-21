using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;
using System.Threading;
using TaskMaster.Models;

namespace TaskMaster.Services;

public class ClaudeService
{
    public ClaudeService()
    {
        // Log system info on first use
        LoggingService.LogSystemInfo();
    }

    public async Task<ClaudeInferenceResponse?> InferSpecFieldsAsync(ClaudeInferenceRequest request, CancellationToken cancellationToken = default)
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
            var response = await CallClaudeDirectAsync(prompt, cancellationToken);

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


    public async Task<string?> CallClaudeDirectAsync(string prompt, System.Threading.CancellationToken cancellationToken = default)
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

            // Register cancellation to kill the process
            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        LoggingService.LogInfo("Cancellation requested - killing Claude process", "ClaudeService");
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"Failed to kill Claude process: {ex.Message}", "ClaudeService");
                }
            });

            // Check for cancellation before proceeding
            cancellationToken.ThrowIfCancellationRequested();

            // Write prompt to stdin and close it
            await process.StandardInput.WriteAsync(prompt.AsMemory(), cancellationToken);
            process.StandardInput.Close();

            // Read output with cancellation support
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            var waitTask = process.WaitForExitAsync(cancellationToken);

            // Wait for completion with cancellation support
            await Task.WhenAll(outputTask, errorTask, waitTask);

            stopwatch.Stop();

            var output = await outputTask;
            var error = await errorTask;

            LoggingService.LogClaudeResponse(process.ExitCode, output, error, stopwatch.Elapsed);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Claude CLI failed with exit code {process.ExitCode}: {error}");
            }

            return output;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            LoggingService.LogInfo($"Claude CLI call was cancelled after {stopwatch.Elapsed}", "ClaudeService");
            throw;
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
            LoggingService.LogInfo($"Parsing Claude response ({response.Length} characters)", "ClaudeService");

            if (string.IsNullOrWhiteSpace(response))
            {
                LoggingService.LogWarning("Empty or null response from Claude", "ClaudeService");
                return null;
            }

            // Try multiple JSON extraction strategies
            var jsonCandidates = new List<string>();

            // Strategy 1: Look for JSON code blocks (```json ... ```)
            var jsonBlockMatch = System.Text.RegularExpressions.Regex.Match(response, @"```json\s*(\{.*?\})\s*```", System.Text.RegularExpressions.RegexOptions.Singleline);
            if (jsonBlockMatch.Success)
            {
                jsonCandidates.Add(jsonBlockMatch.Groups[1].Value);
            }

            // Strategy 2: Look for the largest complete JSON object
            var openBraces = 0;
            var startIndex = -1;

            for (int i = 0; i < response.Length; i++)
            {
                if (response[i] == '{')
                {
                    if (openBraces == 0) startIndex = i;
                    openBraces++;
                }
                else if (response[i] == '}')
                {
                    openBraces--;
                    if (openBraces == 0 && startIndex >= 0)
                    {
                        var json = response.Substring(startIndex, i - startIndex + 1);
                        jsonCandidates.Add(json);
                        break; // Take the first complete JSON object
                    }
                }
            }

            // Strategy 3: Fallback to simple first/last brace method
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                jsonCandidates.Add(json);
            }

            // Try to parse each candidate
            foreach (var jsonCandidate in jsonCandidates)
            {
                try
                {
                    var result = JsonConvert.DeserializeObject<ClaudeInferenceResponse>(jsonCandidate);
                    if (result != null)
                    {
                        LoggingService.LogInfo("Successfully parsed Claude inference response", "ClaudeService");
                        return result;
                    }
                }
                catch (JsonException jsonEx)
                {
                    LoggingService.LogInfo($"JSON parsing attempt failed: {jsonEx.Message}", "ClaudeService");
                    continue; // Try next candidate
                }
            }

            // If we get here, all parsing attempts failed
            LoggingService.LogError("Failed to parse any valid JSON from Claude response", null, "ClaudeService");
            LoggingService.LogInfo($"Raw response: {response}", "ClaudeService");

            return null;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Unexpected error parsing Claude response", ex, "ClaudeService");
            LoggingService.LogInfo($"Raw response: {response}", "ClaudeService");
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

    public async Task<TaskSpec> EnhanceTaskSpecAsync(string title, string summary, string type, string projectName, System.Threading.CancellationToken cancellationToken = default)
    {
        LoggingService.LogInfo($"Starting task enhancement for: {title}", "ClaudeService");

        try
        {
            // First check if Claude CLI is available
            LoggingService.LogInfo("Checking Claude CLI availability for enhancement", "ClaudeService");
            if (!await IsClaudeAvailableAsync())
            {
                var errorMsg = "Claude CLI is not available. Please ensure Claude CLI is installed and authenticated.";
                LoggingService.LogError(errorMsg, null, "ClaudeService");
                throw new InvalidOperationException(errorMsg);
            }

            LoggingService.LogInfo("Claude CLI is available, building enhancement prompt", "ClaudeService");

            // Build enhancement prompt
            var prompt = BuildEnhancementPrompt(title, summary, type, projectName);
            LoggingService.LogInfo($"Enhancement prompt built ({prompt.Length} characters)", "ClaudeService");

            // Call Claude
            LoggingService.LogInfo("Calling Claude CLI for task enhancement", "ClaudeService");
            var response = await CallClaudeDirectAsync(prompt, cancellationToken);

            if (response != null)
            {
                LoggingService.LogInfo($"Received Claude response ({response.Length} characters), parsing...", "ClaudeService");
                var parsed = ParseEnhancementResponse(response);
                if (parsed != null)
                {
                    LoggingService.LogInfo("Task enhancement successful", "ClaudeService");
                    return parsed;
                }
                else
                {
                    var errorMsg = "AI enhancement is unavailable: Failed to parse Claude enhancement response as valid JSON";
                    LoggingService.LogWarning($"{errorMsg}. Full response logged separately.", "ClaudeService");

                    // Log the full response for debugging (separate log entry to avoid UI clutter)
                    LoggingService.LogError($"Full Claude response that failed to parse: {response}", null, "ClaudeService");

                    throw new InvalidOperationException(errorMsg);
                }
            }

            // If we get here, something went wrong
            var noResponseMsg = "AI enhancement is unavailable: Claude CLI returned no response";
            LoggingService.LogError(noResponseMsg, null, "ClaudeService");
            throw new InvalidOperationException(noResponseMsg);
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Error in EnhanceTaskSpecAsync for task '{title}'", ex, "ClaudeService");
            throw; // Re-throw to let the UI handle it with user feedback
        }
    }

    private string BuildEnhancementPrompt(string title, string summary, string type, string projectName)
    {
        var promptBuilder = new StringBuilder();

        promptBuilder.AppendLine("You are an expert technical project manager creating comprehensive task specifications for software development.");
        promptBuilder.AppendLine("Transform the minimal input below into detailed, actionable specifications that developers can implement immediately.");
        promptBuilder.AppendLine();

        promptBuilder.AppendLine($"**Project:** {projectName}");
        promptBuilder.AppendLine($"**Title:** {title}");
        promptBuilder.AppendLine($"**Summary:** {summary}");
        promptBuilder.AppendLine($"**Type:** {type}");
        promptBuilder.AppendLine();

        promptBuilder.AppendLine("Please enhance this task with comprehensive details. Return only valid JSON in this exact format:");
        promptBuilder.AppendLine("```json");
        promptBuilder.AppendLine("{");
        promptBuilder.AppendLine("  \"acceptanceCriteria\": [\"Specific, testable requirement\", \"Another measurable outcome\"],");
        promptBuilder.AppendLine("  \"testPlan\": [\"Manual test step\", \"Automated test requirement\"],");
        promptBuilder.AppendLine("  \"scopePaths\": [\"specific/file/paths.cs\", \"Views/ComponentName.xaml\"],");
        promptBuilder.AppendLine("  \"requiredDocs\": [\"Documentation that should be referenced or updated\"],");
        promptBuilder.AppendLine("  \"nonGoals\": [\"What this task explicitly does NOT include\"],");
        promptBuilder.AppendLine("  \"notes\": [\"Implementation tips\", \"Technical considerations\"]");
        promptBuilder.AppendLine("}");
        promptBuilder.AppendLine("```");

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**Quality Standards:**");
        promptBuilder.AppendLine("- **acceptanceCriteria**: Each criterion must be specific, measurable, and testable");
        promptBuilder.AppendLine("- **testPlan**: Include both manual verification steps and automated test requirements");
        promptBuilder.AppendLine("- **scopePaths**: Use realistic file/folder paths relevant to the task");
        promptBuilder.AppendLine("- **requiredDocs**: List documentation that should be referenced or updated");
        promptBuilder.AppendLine("- **nonGoals**: Clarify what is explicitly out of scope");
        promptBuilder.AppendLine("- **notes**: Provide useful implementation tips or technical considerations");

        return promptBuilder.ToString();
    }

    private TaskSpec? ParseEnhancementResponse(string response)
    {
        try
        {
            LoggingService.LogInfo($"Parsing Claude response. Length: {response.Length}, First 200 chars: {response.Substring(0, Math.Min(200, response.Length))}", "ClaudeService");

            string json = null;

            // Method 1: Try to extract JSON from markdown code block
            var jsonBlockPattern = @"```(?:json)?\s*\n?\s*(\{[\s\S]*?\})\s*\n?\s*```";
            var match = System.Text.RegularExpressions.Regex.Match(response, jsonBlockPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
            if (match.Success)
            {
                json = match.Groups[1].Value.Trim();
                LoggingService.LogInfo("Found JSON in markdown code block", "ClaudeService");
            }

            // Method 2: If no code block, try to find first complete JSON object
            if (string.IsNullOrEmpty(json))
            {
                var jsonStart = response.IndexOf('{');
                if (jsonStart >= 0)
                {
                    // Find the matching closing brace by counting braces
                    int braceCount = 0;
                    int jsonEnd = -1;

                    for (int i = jsonStart; i < response.Length; i++)
                    {
                        if (response[i] == '{') braceCount++;
                        else if (response[i] == '}')
                        {
                            braceCount--;
                            if (braceCount == 0)
                            {
                                jsonEnd = i;
                                break;
                            }
                        }
                    }

                    if (jsonEnd > jsonStart)
                    {
                        json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                        LoggingService.LogInfo("Found JSON using brace counting method", "ClaudeService");
                    }
                }
            }

            if (!string.IsNullOrEmpty(json))
            {
                LoggingService.LogInfo($"Attempting to parse JSON: {json.Substring(0, Math.Min(500, json.Length))}...", "ClaudeService");

                var enhancementData = JsonConvert.DeserializeObject<dynamic>(json);

                if (enhancementData != null)
                {
                    var taskSpec = new TaskSpec();

                    // Convert arrays to JSON strings with null safety
                    taskSpec.AcceptanceCriteria = JsonConvert.SerializeObject(enhancementData.acceptanceCriteria ?? new string[0]);
                    taskSpec.TestPlan = JsonConvert.SerializeObject(enhancementData.testPlan ?? new string[0]);
                    taskSpec.ScopePaths = JsonConvert.SerializeObject(enhancementData.scopePaths ?? new string[0]);
                    taskSpec.RequiredDocs = JsonConvert.SerializeObject(enhancementData.requiredDocs ?? new string[0]);
                    taskSpec.NonGoals = JsonConvert.SerializeObject(enhancementData.nonGoals ?? new string[0]);
                    taskSpec.Notes = JsonConvert.SerializeObject(enhancementData.notes ?? new string[0]);

                    LoggingService.LogInfo("Successfully parsed enhancement response", "ClaudeService");
                    return taskSpec;
                }
                else
                {
                    LoggingService.LogWarning("Deserialized object was null", "ClaudeService");
                }
            }
            else
            {
                LoggingService.LogWarning("No JSON found in Claude response", "ClaudeService");
            }

            return null;
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Error parsing Claude enhancement response. Response length: {response.Length}", ex, "ClaudeService");

            // Log the first part of the response for debugging
            var responsePreview = response.Length > 1000 ? response.Substring(0, 1000) + "..." : response;
            LoggingService.LogError($"Claude response preview: {responsePreview}", null, "ClaudeService");

            return null;
        }
    }

    private TaskSpec CreateBasicTaskSpec(string title, string summary, string type)
    {
        return new TaskSpec
        {
            Title = title,
            Summary = summary,
            Type = type,
            AcceptanceCriteria = "[\"Task completed successfully\"]",
            TestPlan = "[\"Manual verification required\"]",
            ScopePaths = "[]",
            RequiredDocs = "[]",
            NonGoals = "[]",
            Notes = "[]"
        };
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