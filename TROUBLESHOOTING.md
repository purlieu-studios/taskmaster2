# TaskMaster Troubleshooting Guide

## "Failed to get response from Claude" Error

If you're getting this error, TaskMaster now provides comprehensive diagnostics to help identify the issue.

### Step 1: Check the Detailed Error Dialog

When the error occurs, TaskMaster will show a detailed error dialog with:
- **Solutions tab**: Specific troubleshooting steps
- **Recent Logs tab**: Detailed execution logs
- **System Info tab**: System configuration and Claude CLI status

### Step 2: Common Issues and Solutions

#### Issue: Claude CLI Not Installed
**Symptoms:**
- Error: "Claude CLI is not available or not properly configured"
- System Info shows "Claude CLI: Not found"

**Solution:**
1. Install Claude CLI from the official repository
2. Ensure it's added to your system PATH
3. Restart TaskMaster after installation

#### Issue: Claude CLI Not Authenticated
**Symptoms:**
- Claude CLI is found but commands fail
- Error messages about authentication

**Solution:**
1. Open a command prompt/terminal
2. Run: `claude login`
3. Follow the authentication prompts
4. Test with: `claude --version`

#### Issue: Slash Commands Not Found
**Symptoms:**
- Error: "command not found" or similar
- Claude CLI works but specific commands fail

**Solution:**
1. Use the "Setup Project" button in TaskMaster
2. This copies the required slash commands to your repository
3. Make sure you're running TaskMaster from the correct repository directory

#### Issue: Permission or Path Problems
**Symptoms:**
- File access errors
- "Access denied" messages

**Solution:**
1. Run TaskMaster as administrator (Windows) or with sudo (macOS/Linux)
2. Check that the repository directory is writable
3. Verify the CLAUDE.md path is accessible

### Step 3: Using the Diagnostic Tools

#### Test Claude CLI Button
In the error dialog, click "Test Claude CLI" to verify:
- Claude CLI is installed and accessible
- Basic command execution works
- Version information

#### View Recent Logs
1. Click "Show Logs" in the main application
2. Or use the "Recent Logs" tab in the error dialog
3. Look for specific error messages and stack traces

#### Copy Error Details
Use the "Copy Error Details" button to:
- Get a complete error report
- Share with support or troubleshooting
- Save for reference

### Step 4: Manual Claude CLI Testing

If TaskMaster isn't working, test Claude CLI manually:

```bash
# Test basic functionality
claude --version

# Test with a simple prompt (non-interactive mode)
claude --print "Hello, Claude! Please respond with just 'Hello, World!'"

# Test through cmd.exe (same way TaskMaster calls it)
cmd.exe /c claude --print "Test prompt"
```

### Step 5: Fallback Solutions

#### Use Direct Prompt Mode
TaskMaster automatically falls back to direct prompt mode if slash commands fail. This should work with any standard Claude CLI installation.

#### Manual Inference
If automatic inference fails:
1. Use the "Export Template" feature to get project structure
2. Create task specifications manually using the generated templates
3. Use Claude CLI directly with the exported prompts

### Step 6: Environment-Specific Issues

#### Windows
- Ensure Claude CLI is in your PATH environment variable
- Try running TaskMaster as administrator
- Check Windows Defender or antivirus isn't blocking CLI execution

#### macOS/Linux
- Check shell PATH configuration
- Verify executable permissions: `chmod +x $(which claude)`
- Test in different terminal environments

#### Corporate Networks
- Check proxy settings for Claude CLI authentication
- Verify firewall rules allow Claude CLI traffic
- Contact IT for corporate authentication requirements

### Step 7: Advanced Debugging

#### Enable Verbose Logging
TaskMaster automatically logs to:
- Location: `%LocalAppData%\TaskMaster\Logs\` (Windows)
- File pattern: `taskmaster-YYYYMMDD.log`

#### Check System Configuration
The error dialog's "System Info" tab shows:
- Operating system details
- Current directory
- PATH environment variable
- Claude CLI installation status

#### Capture Full Error Context
When reporting issues, include:
- Complete error message from the detailed dialog
- System information
- Recent log entries
- Steps to reproduce the problem

### Getting Help

If these steps don't resolve the issue:

1. **Check logs**: Use the "Show Logs" button for detailed error information
2. **Copy error details**: Use the error dialog to get a complete report
3. **Test components**: Use the diagnostic tools to isolate the problem
4. **Manual testing**: Verify Claude CLI works outside of TaskMaster

The comprehensive error handling and logging in TaskMaster should provide enough information to diagnose most Claude CLI integration issues.

## Common Error Messages

### "Failed to start Claude CLI process"
- Claude CLI is not installed or not in PATH
- File permissions issue
- Antivirus blocking execution

### "Claude CLI failed with exit code X"
- Authentication required (run `claude login`)
- Invalid command syntax
- Network connectivity issues
- Rate limiting or API errors

### "The command line is too long"
**Problem**: Windows has a command line length limit of ~8,191 characters. Large prompts (including CLAUDE.md content) exceed this limit.

**Solution**: TaskMaster v2+ uses stdin input instead of command-line arguments:
- Prompts are written to Claude's stdin stream
- No more command line length limitations
- Can handle prompts of any size

**If you still get this error**:
1. Ensure you're using the latest TaskMaster version
2. Check that the ClaudeService is using stdin method (should see "stdin input" in logs)
3. Manual test: `echo "long prompt" | claude --print`

### "Slash command not found"
- Missing `.claude` directory in repository
- Slash command files not copied
- Wrong working directory

### "JSON parsing error"
- Claude response format changed
- Incomplete response from Claude
- Network interruption during response

Each error now provides specific diagnostic information and suggested solutions through the enhanced error handling system.