using System.Text;
using System.Text.RegularExpressions;
using TaskMaster.Models;

namespace TaskMaster.Services;

public class PanelService
{
    private readonly ClaudeService _claudeService;
    private readonly string _repoRoot;

    public PanelService(ClaudeService claudeService, string repoRoot)
    {
        _claudeService = claudeService;
        _repoRoot = repoRoot;
    }

    public async Task<PanelResult> RunPanelAsync(string specPath, int rounds = 2, string scope = "src/")
    {
        try
        {
            LoggingService.LogInfo($"Starting panel for spec: {specPath}", "PanelService");

            // 1. Read Context
            var contextFiles = await ReadContextFilesAsync(specPath);

            // 2. Derive Question & Output Path
            var (question, decisionPath, slug) = DeriveQuestionAndPaths(specPath);

            // 3. Build Panel Prompt
            var panelPrompt = BuildPanelPrompt(contextFiles, question, rounds, scope);

            // 4. Run Panel via Claude
            var panelResponse = await _claudeService.CallClaudeDirectAsync(panelPrompt);

            if (string.IsNullOrEmpty(panelResponse))
            {
                throw new InvalidOperationException("Panel failed to generate response");
            }

            // 5. Write Decision File
            await WriteDecisionFileAsync(decisionPath, panelResponse, specPath, slug);

            LoggingService.LogInfo($"Panel completed successfully. Decision written to: {decisionPath}", "PanelService");

            return new PanelResult
            {
                Success = true,
                DecisionPath = decisionPath,
                PanelOutput = panelResponse,
                Slug = slug
            };
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Panel execution failed", ex, "PanelService");
            return new PanelResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<Dictionary<string, string>> ReadContextFilesAsync(string specPath)
    {
        var contextFiles = new Dictionary<string, string>();

        try
        {
            // Always read panel definition
            var panelPath = Path.Combine(_repoRoot, ".claude", "panels", "panel-wpf.md");
            if (File.Exists(panelPath))
            {
                contextFiles["panel-wpf"] = await File.ReadAllTextAsync(panelPath);
            }

            // Read all role files
            var rolesDir = Path.Combine(_repoRoot, ".claude", "roles");
            if (Directory.Exists(rolesDir))
            {
                foreach (var roleFile in Directory.GetFiles(rolesDir, "*.md"))
                {
                    var roleName = Path.GetFileNameWithoutExtension(roleFile);
                    contextFiles[$"role-{roleName}"] = await File.ReadAllTextAsync(roleFile);
                }
            }

            // Read guardrails
            var guardrailsPath = Path.Combine(_repoRoot, "docs", "GUARDRAILS.md");
            if (File.Exists(guardrailsPath))
            {
                contextFiles["guardrails"] = await File.ReadAllTextAsync(guardrailsPath);
            }

            // Read CLAUDE.md (project architecture)
            var claudeMdPath = Path.Combine(_repoRoot, "CLAUDE.md");
            if (File.Exists(claudeMdPath))
            {
                contextFiles["claude-md"] = await File.ReadAllTextAsync(claudeMdPath);
            }

            // Read the spec if provided
            if (File.Exists(specPath))
            {
                contextFiles["spec"] = await File.ReadAllTextAsync(specPath);

                // Extract and read Required Docs from spec
                var specContent = contextFiles["spec"];
                var requiredDocsMatches = Regex.Matches(specContent, @"(?<=Required Docs[:\s]*)(.*?)(?=\n\n|\n\s*\*|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                foreach (Match match in requiredDocsMatches)
                {
                    var docPaths = match.Value.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var docPath in docPaths)
                    {
                        var cleanPath = docPath.Trim().TrimStart('-', '*').Trim();
                        var fullDocPath = Path.Combine(_repoRoot, cleanPath);
                        if (File.Exists(fullDocPath))
                        {
                            var docKey = $"required-doc-{Path.GetFileNameWithoutExtension(cleanPath)}";
                            contextFiles[docKey] = await File.ReadAllTextAsync(fullDocPath);
                        }
                    }
                }
            }

            LoggingService.LogInfo($"Read {contextFiles.Count} context files", "PanelService");
            return contextFiles;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Error reading context files", ex, "PanelService");
            throw;
        }
    }

    private (string question, string decisionPath, string slug) DeriveQuestionAndPaths(string specPath)
    {
        string question;
        string decisionPath;
        string slug;
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd");

        if (File.Exists(specPath))
        {
            // Spec mode
            var specFileName = Path.GetFileNameWithoutExtension(specPath);
            slug = Regex.Replace(specFileName, @"^\d{8}-", ""); // Remove date prefix
            question = "How should we implement this spec safely and minimally?";
            decisionPath = Path.Combine(_repoRoot, "docs", "decisions", $"{timestamp}-{slug}.md");
        }
        else
        {
            // Freeform mode - treat specPath as the question
            question = specPath;
            slug = CreateSlugFromQuestion(question);
            decisionPath = Path.Combine(_repoRoot, "docs", "decisions", $"{timestamp}-freeform-{slug}.md");
        }

        return (question, decisionPath, slug);
    }

    private string CreateSlugFromQuestion(string question)
    {
        // Convert to lowercase, replace non-alphanumeric with hyphens, collapse multiple hyphens
        var slug = Regex.Replace(question.ToLower(), @"[^a-z0-9\s]", "")
                        .Trim()
                        .Replace(" ", "-");
        slug = Regex.Replace(slug, @"-+", "-");

        // Truncate to reasonable length (~6 words max)
        var words = slug.Split('-');
        if (words.Length > 6)
        {
            slug = string.Join("-", words.Take(6));
        }

        return slug;
    }

    private string BuildPanelPrompt(Dictionary<string, string> contextFiles, string question, int rounds, string scope)
    {
        var promptBuilder = new StringBuilder();

        promptBuilder.AppendLine("You are running the WPF Expert Panel to make a design decision.");
        promptBuilder.AppendLine("You must follow the panel workflow exactly and produce the required output format.");
        promptBuilder.AppendLine();

        // Include all context files
        foreach (var (key, content) in contextFiles)
        {
            promptBuilder.AppendLine($"## Context File: {key}");
            promptBuilder.AppendLine("```");
            promptBuilder.AppendLine(content);
            promptBuilder.AppendLine("```");
            promptBuilder.AppendLine();
        }

        promptBuilder.AppendLine("## Panel Parameters");
        promptBuilder.AppendLine($"- **Question**: {question}");
        promptBuilder.AppendLine($"- **Phase**: decide");
        promptBuilder.AppendLine($"- **Rounds**: {rounds}");
        promptBuilder.AppendLine($"- **Scope**: {scope}");
        promptBuilder.AppendLine();

        promptBuilder.AppendLine("## Instructions");
        promptBuilder.AppendLine("Follow the panel workflow from panel-wpf.md exactly:");
        promptBuilder.AppendLine("1. Round 0 — Scan (≤5 bullets)");
        promptBuilder.AppendLine("2. Round 1 — Options (exactly 2 viable approaches)");
        promptBuilder.AppendLine("3. Rounds 2..N — Debate (each role in order)");
        promptBuilder.AppendLine("4. Finalization — Pick ONE winner");
        promptBuilder.AppendLine();

        promptBuilder.AppendLine("You must output in the exact format specified in panel-wpf.md:");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Decision:");
        promptBuilder.AppendLine("<one paragraph>");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Why:");
        promptBuilder.AppendLine("- ...");
        promptBuilder.AppendLine("- ...");
        promptBuilder.AppendLine("- ...");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Checklist:");
        promptBuilder.AppendLine("1.");
        promptBuilder.AppendLine("2.");
        promptBuilder.AppendLine("3.");
        promptBuilder.AppendLine("4.");
        promptBuilder.AppendLine("5.");
        promptBuilder.AppendLine("6.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Tests:");
        promptBuilder.AppendLine("- ...");
        promptBuilder.AppendLine("- ...");
        promptBuilder.AppendLine("- ...");
        promptBuilder.AppendLine("- ...");
        promptBuilder.AppendLine("- ...");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Risks:");
        promptBuilder.AppendLine("| Risk | Level | Mitigation |");
        promptBuilder.AppendLine("|------|-------|------------|");
        promptBuilder.AppendLine("| ...  | ...   | ...        |");

        return promptBuilder.ToString();
    }

    private async Task WriteDecisionFileAsync(string decisionPath, string panelOutput, string specPath, string slug)
    {
        try
        {
            // Ensure decision directory exists
            var decisionDir = Path.GetDirectoryName(decisionPath);
            if (!Directory.Exists(decisionDir))
            {
                Directory.CreateDirectory(decisionDir);
            }

            // Create decision file content with header
            var decisionBuilder = new StringBuilder();
            decisionBuilder.AppendLine($"# Design Decision: {slug}");
            decisionBuilder.AppendLine();
            decisionBuilder.AppendLine($"**Spec:** {specPath}");
            decisionBuilder.AppendLine($"**Date:** {DateTime.UtcNow:yyyy-MM-dd}");
            decisionBuilder.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            decisionBuilder.AppendLine();
            decisionBuilder.AppendLine("---");
            decisionBuilder.AppendLine();
            decisionBuilder.AppendLine(panelOutput);

            await File.WriteAllTextAsync(decisionPath, decisionBuilder.ToString());
            LoggingService.LogInfo($"Decision file written: {decisionPath}", "PanelService");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to write decision file", ex, "PanelService");
            throw;
        }
    }
}