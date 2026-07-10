using System.Windows;
using Lojinha.App.ViewModels;

namespace Lojinha.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
