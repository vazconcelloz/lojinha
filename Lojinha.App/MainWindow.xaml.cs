using System.Windows;
using Lojinha.App.ViewModels;
using Lojinha.App.Views;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Lojinha.App;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private readonly CategoriaView _categoriaView = new();
    private readonly FornecedorView _fornecedorView = new();
    private readonly ProdutoView _produtoView = new();

    public MainWindow(MainViewModel viewModel, ISnackbarService snackbarService, IContentDialogService contentDialogService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        snackbarService.SetSnackbarPresenter(RootSnackbarPresenter);
        contentDialogService.SetContentPresenter(RootContentDialogPresenter);

        CategoriasItem.IsActive = true;
        Loaded += (_, _) => NavigateTo("categorias");
    }

    private void RootNavigation_OnSelectionChanged(NavigationView sender, RoutedEventArgs e)
    {
        var tag = (RootNavigation.SelectedItem as NavigationViewItem)?.TargetPageTag;
        if (tag is not null)
        {
            NavigateTo(tag);
        }
    }

    private void NavigateTo(string tag)
    {
        (FrameworkElement view, object dataContext) = tag switch
        {
            "categorias" => ((FrameworkElement)_categoriaView, (object)_viewModel.Categorias),
            "fornecedores" => ((FrameworkElement)_fornecedorView, (object)_viewModel.Fornecedores),
            "produtos" => ((FrameworkElement)_produtoView, (object)_viewModel.Produtos),
            _ => ((FrameworkElement)_categoriaView, (object)_viewModel.Categorias)
        };

        view.DataContext = dataContext;
        RootNavigation.ReplaceContent(view, dataContext);
    }

    private void ThemeToggle_OnToggle(object sender, RoutedEventArgs e)
    {
        var theme = ThemeToggle.IsChecked == true ? ApplicationTheme.Dark : ApplicationTheme.Light;
        ApplicationThemeManager.Apply(theme, WindowBackdropType.None, false);
    }
}
