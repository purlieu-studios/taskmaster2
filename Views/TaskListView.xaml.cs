using System.Windows.Controls;
using TaskMaster.ViewModels;

namespace TaskMaster.Views;

public partial class TaskListView : UserControl
{
    public TaskListViewModel ViewModel { get; private set; }

    public TaskListView()
    {
        InitializeComponent();
        ViewModel = new TaskListViewModel();
        DataContext = ViewModel;
    }
}