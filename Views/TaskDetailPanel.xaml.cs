using System.Windows;
using System.Windows.Controls;

namespace TaskMaster.Views;

public partial class TaskDetailPanel : UserControl
{
    public TaskDetailPanel()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Notify parent window to close the panel
        if (Parent is Grid parentGrid)
        {
            Visibility = Visibility.Collapsed;
        }

        // Alternative: Raise an event that the parent can handle
        PanelCloseRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? PanelCloseRequested;
}