using System.Windows;
using System.Windows.Controls;
using TaskMaster.ViewModels;

namespace TaskMaster.Views
{
    public partial class NewTaskPopup : UserControl
    {
        public NewTaskPopup()
        {
            InitializeComponent();
        }

        private void NewTaskPopup_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Prevent click event from bubbling up to the overlay
            e.Handled = true;
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the main window's DataContext (MainViewModel)
            if (Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
            {
                await mainViewModel.CreateTaskFromPopupCommand.ExecuteAsync(null);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the main window's DataContext (MainViewModel)
            if (Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
            {
                // If AI generation is in progress, cancel it
                if (mainViewModel.NewTaskViewModel?.IsGenerating == true)
                {
                    mainViewModel.NewTaskViewModel.CancelAIGeneration();
                }

                mainViewModel.CloseNewTaskPopupCommand.Execute(null);
            }
        }
    }
}