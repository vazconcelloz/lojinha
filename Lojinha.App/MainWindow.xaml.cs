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
    private readonly EstoqueView _estoqueView = new();
    private readonly VendaView _vendaView = new();
    private readonly UsuarioView _usuarioView = new();

    public MainWindow(MainViewModel viewModel, ISnackbarService snackbarService, IContentDialogService contentDialogService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        snackbarService.SetSnackbarPresenter(RootSnackbarPresenter);
        contentDialogService.SetContentPresenter(RootContentDialogPresenter);

        CategoriasItem.IsActive = true;
        Loaded += (_, _) => NavigateTo("categorias");
    }

    private void NavigationViewItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not NavigationViewItem { TargetPageTag: { } tag })
        {
            return;
        }

        CategoriasItem.IsActive = tag == "categorias";
        FornecedoresItem.IsActive = tag == "fornecedores";
        ProdutosItem.IsActive = tag == "produtos";
        EstoqueItem.IsActive = tag == "estoque";
        VendasItem.IsActive = tag == "vendas";
        UsuariosItem.IsActive = tag == "usuarios";

        NavigateTo(tag);
    }

    private void NavigateTo(string tag)
    {
        (FrameworkElement view, object dataContext) = tag switch
        {
            "categorias" => ((FrameworkElement)_categoriaView, (object)_viewModel.Categorias),
            "fornecedores" => ((FrameworkElement)_fornecedorView, (object)_viewModel.Fornecedores),
            "produtos" => ((FrameworkElement)_produtoView, (object)_viewModel.Produtos),
            "estoque" => ((FrameworkElement)_estoqueView, (object)_viewModel.Estoque),
            "vendas" => ((FrameworkElement)_vendaView, (object)_viewModel.Vendas),
            "usuarios" => ((FrameworkElement)_usuarioView, (object)_viewModel.Usuarios),
            _ => ((FrameworkElement)_categoriaView, (object)_viewModel.Categorias)
        };

        RefreshViewModel(tag);

        view.DataContext = dataContext;
        RootNavigation.ReplaceContent(view, dataContext);
    }

    private void RefreshViewModel(string tag)
    {
        switch (tag)
        {
            case "categorias":
                _viewModel.Categorias.Refresh();
                break;
            case "fornecedores":
                _viewModel.Fornecedores.Refresh();
                break;
            case "produtos":
                _viewModel.Produtos.Refresh();
                break;
            case "estoque":
                _viewModel.Estoque.Refresh();
                break;
            case "vendas":
                _viewModel.Vendas.Refresh();
                break;
            case "usuarios":
                _viewModel.Usuarios.Refresh();
                break;
            default:
                _viewModel.Categorias.Refresh();
                break;
        }
    }

    private void ThemeToggle_OnToggle(object sender, RoutedEventArgs e)
    {
        var theme = ThemeToggle.IsChecked == true ? ApplicationTheme.Dark : ApplicationTheme.Light;
        ApplicationThemeManager.Apply(theme, WindowBackdropType.None, false);
    }
}
