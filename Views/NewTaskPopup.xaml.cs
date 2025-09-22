using System.ComponentModel;
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
            this.Loaded += NewTaskPopup_Loaded;
        }

        private void NewTaskPopup_Loaded(object sender, RoutedEventArgs e)
        {
            // Wire up property change notifications for dynamic button text
            if (Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
            {
                if (mainViewModel.NewTaskViewModel != null)
                {
                    mainViewModel.NewTaskViewModel.PropertyChanged += NewTaskViewModel_PropertyChanged;
                    UpdateCancelButtonText(mainViewModel.NewTaskViewModel.IsGenerating);
                }
            }
        }

        private void NewTaskViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NewTaskViewModel.IsGenerating))
            {
                if (sender is NewTaskViewModel viewModel)
                {
                    Dispatcher.Invoke(() => UpdateCancelButtonText(viewModel.IsGenerating));
                }
            }
        }

        private void UpdateCancelButtonText(bool isGenerating)
        {
            if (CancelButton != null)
            {
                CancelButton.Content = isGenerating ? "Cancel AI" : "Cancel";
            }
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