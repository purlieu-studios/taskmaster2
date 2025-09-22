using System.Windows;
using TaskMaster.Models;

namespace TaskMaster.Views;

public partial class DeleteConfirmationDialog : Window
{
    public bool Confirmed { get; private set; } = false;

    public DeleteConfirmationDialog(TaskSpec task)
    {
        InitializeComponent();

        // Set task details in the UI
        TaskNumberText.Text = $"#{task.Number}";
        TaskTitleText.Text = task.Title;
        TaskSummaryText.Text = !string.IsNullOrWhiteSpace(task.Summary)
            ? task.Summary
            : "No summary available";
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        DialogResult = false;
        Close();
    }
}