using Microsoft.Win32;
using System.Windows;
using System.Windows.Forms;

namespace TaskMaster.Views;

public partial class ProjectDialog : Window
{
    public string ProjectName => ProjectNameTextBox.Text;
    public string? ProjectDirectory => string.IsNullOrWhiteSpace(ProjectDirectoryTextBox.Text) ? null : ProjectDirectoryTextBox.Text;

    public ProjectDialog()
    {
        InitializeComponent();
        ProjectNameTextBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            System.Windows.MessageBox.Show("Please enter a project name.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            ProjectNameTextBox.Focus();
            return;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BrowseDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select project directory for analysis",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ProjectDirectoryTextBox.Text = dialog.SelectedPath;
        }
    }
}