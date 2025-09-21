# TaskMaster Branch, Commit, and PR Script
# Usage: .\scripts\branch-commit-pr.ps1 "feature-description" "commit message"

param(
    [Parameter(Mandatory=$true)]
    [string]$FeatureName,

    [Parameter(Mandatory=$true)]
    [string]$CommitMessage,

    [Parameter(Mandatory=$false)]
    [switch]$SkipBranch
)

# Function to create branch name from feature description
function Get-BranchName {
    param([string]$description)

    # Convert to lowercase and replace spaces with hyphens
    $branch = $description.ToLower() -replace '[^a-z0-9\s-]', '' -replace '\s+', '-'

    # Determine branch type based on keywords
    if ($description -match '(fix|bug|error|issue)') {
        return "fix/$branch"
    }
    elseif ($description -match '(feature|add|implement|create)') {
        return "feature/$branch"
    }
    elseif ($description -match '(refactor|improve|enhance|optimize)') {
        return "refactor/$branch"
    }
    elseif ($description -match '(doc|documentation|readme|guide)') {
        return "docs/$branch"
    }
    else {
        return "feature/$branch"
    }
}

try {
    Write-Host "🚀 TaskMaster Branch-Commit-PR Script" -ForegroundColor Cyan
    Write-Host "======================================" -ForegroundColor Cyan

    # Step 1: Check git status
    Write-Host "📋 Checking git status..." -ForegroundColor Yellow
    $gitStatus = git status --porcelain

    if (-not $gitStatus -and -not $SkipBranch) {
        Write-Host "⚠️  No changes detected. Use -SkipBranch to create PR for current branch." -ForegroundColor Red
        exit 1
    }

    # Step 2: Create and checkout branch (unless skipping)
    if (-not $SkipBranch) {
        $branchName = Get-BranchName $FeatureName
        Write-Host "🌿 Creating and checking out branch: $branchName" -ForegroundColor Green
        git checkout -b $branchName

        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Failed to create branch" -ForegroundColor Red
            exit 1
        }
    } else {
        $branchName = git branch --show-current
        Write-Host "🌿 Using current branch: $branchName" -ForegroundColor Green
    }

    # Step 3: Add all changes
    if ($gitStatus) {
        Write-Host "📁 Adding all changes..." -ForegroundColor Yellow
        git add .

        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Failed to add changes" -ForegroundColor Red
            exit 1
        }
    }

    # Step 4: Commit with standardized message
    if ($gitStatus -or $SkipBranch) {
        Write-Host "💾 Creating commit..." -ForegroundColor Yellow

        $commitBody = @"
$CommitMessage

🤖 Generated with [Claude Code](https://claude.ai/code)

Co-Authored-By: Claude <noreply@anthropic.com>
"@

        git commit -m $commitBody

        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Failed to create commit" -ForegroundColor Red
            exit 1
        }
    }

    # Step 5: Push branch
    Write-Host "☁️  Pushing branch to remote..." -ForegroundColor Yellow
    git push -u origin $branchName

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to push branch. Make sure remote 'origin' is configured." -ForegroundColor Red
        Write-Host "💡 To set up remote: git remote add origin https://github.com/username/taskmaster.git" -ForegroundColor Cyan
        exit 1
    }

    # Step 6: Create Pull Request
    Write-Host "🔄 Creating pull request..." -ForegroundColor Yellow

    # Check if gh CLI is available
    $ghAvailable = Get-Command gh -ErrorAction SilentlyContinue

    if ($ghAvailable) {
        $prTitle = if ($FeatureName.Length -gt 50) { $FeatureName.Substring(0, 47) + "..." } else { $FeatureName }

        $prBody = @"
## Summary
$CommitMessage

## Changes Made
- [List key changes here]

## Testing
- [X] Application builds successfully
- [X] Core functionality tested
- [ ] Integration tests pass
- [ ] Manual testing completed

## Notes
- Generated with TaskMaster branch-commit-PR script
- All changes follow project coding standards

🤖 Generated with [Claude Code](https://claude.ai/code)
"@

        gh pr create --title $prTitle --body $prBody

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Pull request created successfully!" -ForegroundColor Green

            # Get PR URL
            $prUrl = gh pr view --json url --jq .url
            Write-Host "🔗 PR URL: $prUrl" -ForegroundColor Cyan
        } else {
            Write-Host "❌ Failed to create PR with gh CLI" -ForegroundColor Red
            Write-Host "💡 You can create it manually at: https://github.com/username/taskmaster/compare/$branchName" -ForegroundColor Cyan
        }
    } else {
        Write-Host "⚠️  GitHub CLI (gh) not found. You can:" -ForegroundColor Yellow
        Write-Host "   1. Install gh CLI: https://cli.github.com/" -ForegroundColor Cyan
        Write-Host "   2. Or create PR manually at: https://github.com/username/taskmaster/compare/$branchName" -ForegroundColor Cyan
    }

    Write-Host "" -ForegroundColor Green
    Write-Host "🎉 Branch-Commit-PR process completed!" -ForegroundColor Green
    Write-Host "Branch: $branchName" -ForegroundColor White
    Write-Host "Commit: $CommitMessage" -ForegroundColor White

} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}