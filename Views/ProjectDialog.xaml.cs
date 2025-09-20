using Microsoft.Win32;
using System.Windows;

namespace TaskMaster.Views;

public partial class ProjectDialog : Window
{
    public string ProjectName => ProjectNameTextBox.Text;
    public string? ClaudeMdPath => string.IsNullOrWhiteSpace(ClaudeMdPathTextBox.Text) ? null : ClaudeMdPathTextBox.Text;

    public ProjectDialog()
    {
        InitializeComponent();
        ProjectNameTextBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            MessageBox.Show("Please enter a project name.", "Validation Error",
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

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Markdown files (*.md)|*.md|All files (*.*)|*.*",
            Title = "Select CLAUDE.md file"
        };

        if (dialog.ShowDialog() == true)
        {
            ClaudeMdPathTextBox.Text = dialog.FileName;
        }
    }
}