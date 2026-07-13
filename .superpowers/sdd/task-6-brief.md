### Task 6: `MainWindow` role gating + "Sair"

**Files:**
- Modify: `Lojinha.App/MainWindow.xaml`
- Modify: `Lojinha.App/MainWindow.xaml.cs`
- Modify: `Lojinha.App/App.xaml.cs`

**Interfaces:**
- Consumes: `SessionService.CurrentUser` (Task 3), `App.MostrarLoginEEntrar()` (Task 4, made re-entrant here).
- Produces: `MainWindow.Sair` (`event EventHandler?`) — the app subscribes to this to re-show `LoginWindow` without recreating the DI scope.

- [ ] **Step 1: Add role gating and the "Sair" event to `MainWindow.xaml.cs`**

Replace the full contents of `Lojinha.App/MainWindow.xaml.cs` with:

```csharp
using System.Windows;
using Lojinha.App.Services;
using Lojinha.App.ViewModels;
using Lojinha.App.Views;
using Lojinha.Data.Models;
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

    public event EventHandler? Sair;

    public MainWindow(MainViewModel viewModel, ISnackbarService snackbarService, IContentDialogService contentDialogService, SessionService session)
    {
        InitializeComponent();

        _viewModel = viewModel;
        snackbarService.SetSnackbarPresenter(RootSnackbarPresenter);
        contentDialogService.SetContentPresenter(RootContentDialogPresenter);

        var isAdmin = session.CurrentUser?.Papel == PapelUsuario.Admin;
        if (!isAdmin)
        {
            CategoriasItem.Visibility = Visibility.Collapsed;
            FornecedoresItem.Visibility = Visibility.Collapsed;
            ProdutosItem.Visibility = Visibility.Collapsed;
            UsuariosItem.Visibility = Visibility.Collapsed;
        }

        var tagInicial = isAdmin ? "categorias" : "vendas";
        var itemInicial = isAdmin ? CategoriasItem : VendasItem;
        itemInicial.IsActive = true;
        Loaded += (_, _) => NavigateTo(tagInicial);
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

    private void SairButton_OnClick(object sender, RoutedEventArgs e)
    {
        Sair?.Invoke(this, EventArgs.Empty);
    }
}
```

- [ ] **Step 2: Add the "Sair" button to `MainWindow.xaml`**

In `Lojinha.App/MainWindow.xaml`, replace the `<ui:NavigationView.PaneFooter>` block:

```xml
            <ui:NavigationView.PaneFooter>
                <ui:ToggleSwitch x:Name="ThemeToggle" OnContent="Escuro" OffContent="Claro" Margin="12"
                                  Checked="ThemeToggle_OnToggle" Unchecked="ThemeToggle_OnToggle" />
            </ui:NavigationView.PaneFooter>
```

with:

```xml
            <ui:NavigationView.PaneFooter>
                <StackPanel>
                    <ui:ToggleSwitch x:Name="ThemeToggle" OnContent="Escuro" OffContent="Claro" Margin="12,12,12,4"
                                      Checked="ThemeToggle_OnToggle" Unchecked="ThemeToggle_OnToggle" />
                    <ui:Button Content="Sair" Icon="{ui:SymbolIcon Symbol=SignOut24}" Margin="12,4,12,12"
                               HorizontalAlignment="Stretch" Click="SairButton_OnClick" />
                </StackPanel>
            </ui:NavigationView.PaneFooter>
```

- [ ] **Step 3: Make `MostrarLoginEEntrar` re-entrant and wire "Sair" to it**

In `Lojinha.App/App.xaml.cs`, replace the `MostrarLoginEEntrar` method:

```csharp
    private void MostrarLoginEEntrar()
    {
        var loginWindow = _scope!.ServiceProvider.GetRequiredService<LoginWindow>();
        var loginOk = loginWindow.ShowDialog();

        if (loginOk != true)
        {
            Shutdown();
            return;
        }

        var mainWindow = _scope.ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Sair += (_, _) =>
        {
            mainWindow.Close();
            _scope.ServiceProvider.GetRequiredService<SessionService>().CurrentUser = null;
            MostrarLoginEEntrar();
        };
        Current.MainWindow = mainWindow;
        mainWindow.Show();
    }
```

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 53 tests total.

- [ ] **Step 6: Manual smoke check**

Run: `dotnet run --project Lojinha.App`, log in as the Admin created earlier. Expected: sidebar shows all 6 items (Admin sees everything). Click "Sair" — the window closes and the login screen reappears (app doesn't exit). Log back in as the same Admin — confirm the app returns to normal. Then, from the Usuários screen, create a Vendedor account; click "Sair"; log in as that Vendedor. Expected: sidebar shows only "Vendas" and "Estoque" (Categorias/Fornecedores/Produtos/Usuários are gone), and the app lands on the Vendas screen by default (not Categorias, which this user can't see).

- [ ] **Step 7: Commit**

```bash
git add Lojinha.App/MainWindow.xaml Lojinha.App/MainWindow.xaml.cs Lojinha.App/App.xaml.cs
git commit -m "feat: add role-based sidebar gating and Sair (logout without restart)"
```

---

