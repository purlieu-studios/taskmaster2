using System.Windows;
using TaskMaster.ViewModels;

namespace TaskMaster.Views;

public partial class ExportTemplateDialog : Window
{
    public ExportTemplateDialog(TemplateExportViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}