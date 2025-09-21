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
                mainViewModel.CloseNewTaskPopupCommand.Execute(null);
            }
        }
    }
}