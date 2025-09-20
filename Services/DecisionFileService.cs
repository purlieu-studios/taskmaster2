using System.Text.RegularExpressions;

namespace TaskMaster.Services;

public class DecisionFileService
{
    private readonly string _repoRoot;

    public DecisionFileService(string repoRoot)
    {
        _repoRoot = repoRoot;
    }

    public string GetDecisionFilePath(string specPath)
    {
        try
        {
            var specFileName = Path.GetFileNameWithoutExtension(specPath);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");

            // Extract slug from spec filename (remove date prefix if present)
            var slug = Regex.Replace(specFileName, @"^\d{8}-", "");

            return Path.Combine(_repoRoot, "docs", "decisions", $"{timestamp}-{slug}.md");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Error generating decision file path", ex, "DecisionFileService");
            throw;
        }
    }

    public async Task<bool> DecisionExistsAsync(string specPath)
    {
        try
        {
            var decisionPath = GetDecisionFilePath(specPath);
            return File.Exists(decisionPath);
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Error checking decision file existence", ex, "DecisionFileService");
            return false;
        }
    }

    public async Task<string?> ReadDecisionAsync(string specPath)
    {
        try
        {
            var decisionPath = GetDecisionFilePath(specPath);

            if (!File.Exists(decisionPath))
            {
                LoggingService.LogWarning($"Decision file not found: {decisionPath}", "DecisionFileService");
                return null;
            }

            var content = await File.ReadAllTextAsync(decisionPath);
            LoggingService.LogInfo($"Read decision file: {decisionPath} ({content.Length} characters)", "DecisionFileService");
            return content;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Error reading decision file", ex, "DecisionFileService");
            return null;
        }
    }

    public async Task<bool> WriteDecisionAsync(string specPath, string decisionContent)
    {
        try
        {
            var decisionPath = GetDecisionFilePath(specPath);
            var decisionDir = Path.GetDirectoryName(decisionPath);

            // Ensure decision directory exists
            if (!Directory.Exists(decisionDir))
            {
                Directory.CreateDirectory(decisionDir);
                LoggingService.LogInfo($"Created decision directory: {decisionDir}", "DecisionFileService");
            }

            await File.WriteAllTextAsync(decisionPath, decisionContent);
            LoggingService.LogInfo($"Decision file written: {decisionPath} ({decisionContent.Length} characters)", "DecisionFileService");
            return true;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Error writing decision file", ex, "DecisionFileService");
            return false;
        }
    }

    public async Task<List<string>> GetRecentDecisionsAsync(int limit = 10)
    {
        try
        {
            var decisionsDir = Path.Combine(_repoRoot, "docs", "decisions");

            if (!Directory.Exists(decisionsDir))
            {
                return new List<string>();
            }

            var decisionFiles = Directory.GetFiles(decisionsDir, "*.md")
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .Take(limit)
                .ToList();

            LoggingService.LogInfo($"Found {decisionFiles.Count} recent decision files", "DecisionFileService");
            return decisionFiles;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Error getting recent decisions", ex, "DecisionFileService");
            return new List<string>();
        }
    }

    public DecisionSummary ParseDecision(string decisionContent)
    {
        try
        {
            var summary = new DecisionSummary();

            // Extract decision paragraph
            var decisionMatch = Regex.Match(decisionContent, @"Decision:\s*\n(.*?)(?=\n\n|\nWhy:)", RegexOptions.Singleline);
            if (decisionMatch.Success)
            {
                summary.Decision = decisionMatch.Groups[1].Value.Trim();
            }

            // Extract why points
            var whyMatches = Regex.Matches(decisionContent, @"(?<=Why:\s*\n)(.*?)(?=\n\n|\nChecklist:)", RegexOptions.Singleline);
            if (whyMatches.Count > 0)
            {
                var whyText = whyMatches[0].Groups[1].Value;
                summary.WhyPoints = Regex.Matches(whyText, @"^\s*-\s*(.+)$", RegexOptions.Multiline)
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value.Trim())
                    .ToList();
            }

            // Extract checklist items
            var checklistMatches = Regex.Matches(decisionContent, @"(?<=Checklist:\s*\n)(.*?)(?=\n\n|\nTests:)", RegexOptions.Singleline);
            if (checklistMatches.Count > 0)
            {
                var checklistText = checklistMatches[0].Groups[1].Value;
                summary.ChecklistItems = Regex.Matches(checklistText, @"^\s*\d+\.\s*(.+)$", RegexOptions.Multiline)
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value.Trim())
                    .ToList();
            }

            // Extract test items
            var testMatches = Regex.Matches(decisionContent, @"(?<=Tests:\s*\n)(.*?)(?=\n\n|\nRisks:)", RegexOptions.Singleline);
            if (testMatches.Count > 0)
            {
                var testText = testMatches[0].Groups[1].Value;
                summary.TestItems = Regex.Matches(testText, @"^\s*-\s*(.+)$", RegexOptions.Multiline)
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value.Trim())
                    .ToList();
            }

            LoggingService.LogInfo("Decision parsed successfully", "DecisionFileService");
            return summary;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Error parsing decision content", ex, "DecisionFileService");
            return new DecisionSummary();
        }
    }

    public string GenerateDecisionTemplate(string specTitle, string specPath)
    {
        var template = $@"# Design Decision: {specTitle}

**Spec:** {specPath}
**Date:** {DateTime.UtcNow:yyyy-MM-dd}
**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

---

Decision:
[Decision paragraph will be filled by panel]

Why:
- [Reason 1]
- [Reason 2]
- [Reason 3]

Checklist:
1. [Implementation step 1]
2. [Implementation step 2]
3. [Implementation step 3]
4. [Implementation step 4]
5. [Implementation step 5]
6. [Implementation step 6]

Tests:
- [Test requirement 1]
- [Test requirement 2]
- [Test requirement 3]
- [Test requirement 4]
- [Test requirement 5]

Risks:
| Risk | Level | Mitigation |
|------|-------|------------|
| [Risk 1] | [Low/Medium/High] | [Mitigation strategy] |
";

        return template;
    }
}

public class DecisionSummary
{
    public string Decision { get; set; } = string.Empty;
    public List<string> WhyPoints { get; set; } = new();
    public List<string> ChecklistItems { get; set; } = new();
    public List<string> TestItems { get; set; } = new();
}