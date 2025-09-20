using Microsoft.Win32;
using System.Windows.Forms;

namespace TaskMaster.Services;

public static class FolderBrowserService
{
    public static string? SelectFolder(string description = "Select folder")
    {
        try
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = description,
                UseDescriptionForTitle = true,
                SelectedPath = Environment.CurrentDirectory,
                ShowNewFolderButton = true
            };

            var result = dialog.ShowDialog();
            return result == DialogResult.OK ? dialog.SelectedPath : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in folder selection: {ex.Message}");
            return null;
        }
    }

    public static string? SelectRepoRoot()
    {
        var selectedPath = SelectFolder("Select Repository Root Directory");

        if (selectedPath != null && ValidationService.ValidateGitRepository(selectedPath))
        {
            return selectedPath;
        }

        if (selectedPath != null)
        {
            System.Windows.MessageBox.Show(
                "Selected directory is not a Git repository. Please select a directory containing a .git folder.",
                "Invalid Repository",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }

        return null;
    }

    public static string? SelectClaudeMdFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Markdown files (*.md)|*.md|All files (*.*)|*.*",
            Title = "Select CLAUDE.md file",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}