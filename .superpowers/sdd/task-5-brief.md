### Task 5: Usuários screen

**Files:**
- Create: `Lojinha.App/ViewModels/UserViewModel.cs`
- Create: `Lojinha.App/Views/UsuarioView.xaml`
- Create: `Lojinha.App/Views/UsuarioView.xaml.cs`
- Modify: `Lojinha.App/ViewModels/MainViewModel.cs`
- Modify: `Lojinha.App/App.xaml.cs`
- Modify: `Lojinha.App/MainWindow.xaml`
- Modify: `Lojinha.App/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `UserService.Add`/`Update`/`Delete`/`GetAll` (Task 2), `ISnackbarService`/`IContentDialogService` (existing), `CountToVisibilityConverter`/`BooleanToVisibilityConverter` (existing).
- Produces: `UserViewModel` with `Usuarios`, `Papeis`, `NomeUsuario`, `Senha`, `PapelSelecionado`, `EditandoId`/`EmEdicao`, `AdicionarCommand`/`EditarCommand`/`SalvarCommand`/`CancelarCommand`/`ExcluirCommand`, public `Refresh()`. `MainViewModel.Usuarios` property (type `UserViewModel`). This screen is **not yet role-restricted** — Task 6 adds the Admin-only nav gating.

- [ ] **Step 1: Create `UserViewModel`**

Create `Lojinha.App/ViewModels/UserViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lojinha.Data.Models;
using Lojinha.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace Lojinha.App.ViewModels;

public partial class UserViewModel : ObservableObject
{
    private readonly UserService _service;
    private readonly ISnackbarService _snackbar;
    private readonly IContentDialogService _dialogService;

    public ObservableCollection<User> Usuarios { get; } = new();
    public PapelUsuario[] Papeis { get; } = Enum.GetValues<PapelUsuario>();

    [ObservableProperty]
    private string nomeUsuario = string.Empty;

    [ObservableProperty]
    private string senha = string.Empty;

    [ObservableProperty]
    private PapelUsuario papelSelecionado;

    [ObservableProperty]
    private int? editandoId;

    public bool EmEdicao => EditandoId is not null;

    public UserViewModel(UserService service, ISnackbarService snackbar, IContentDialogService dialogService)
    {
        _service = service;
        _snackbar = snackbar;
        _dialogService = dialogService;
        Carregar();
    }

    public void Refresh()
    {
        Carregar();
    }

    private void Carregar()
    {
        Usuarios.Clear();
        foreach (var usuario in _service.GetAll())
        {
            Usuarios.Add(usuario);
        }
    }

    partial void OnEditandoIdChanged(int? value)
    {
        OnPropertyChanged(nameof(EmEdicao));
    }

    [RelayCommand]
    private void Adicionar()
    {
        try
        {
            _service.Add(NomeUsuario, Senha, PapelSelecionado);
            LimparFormulario();
            Carregar();
            _snackbar.Show("Sucesso", "Usuário adicionado.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private void Editar(User usuario)
    {
        EditandoId = usuario.Id;
        NomeUsuario = usuario.NomeUsuario;
        Senha = string.Empty;
        PapelSelecionado = usuario.Papel;
    }

    [RelayCommand]
    private void Salvar()
    {
        if (EditandoId is null)
        {
            return;
        }

        try
        {
            _service.Update(EditandoId.Value, NomeUsuario, string.IsNullOrEmpty(Senha) ? null : Senha, PapelSelecionado);
            EditandoId = null;
            LimparFormulario();
            Carregar();
            _snackbar.Show("Sucesso", "Usuário atualizado.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private void Cancelar()
    {
        EditandoId = null;
        LimparFormulario();
    }

    private void LimparFormulario()
    {
        NomeUsuario = string.Empty;
        Senha = string.Empty;
    }

    [RelayCommand]
    private async Task Excluir(User usuario)
    {
        var options = new SimpleContentDialogCreateOptions
        {
            Title = "Excluir usuário",
            Content = $"Tem certeza que deseja excluir '{usuario.NomeUsuario}'?",
            PrimaryButtonText = "Excluir",
            CloseButtonText = "Cancelar",
        };

        var result = await _dialogService.ShowSimpleDialogAsync(options, CancellationToken.None);
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            _service.Delete(usuario.Id);
            Carregar();
            _snackbar.Show("Sucesso", $"Usuário '{usuario.NomeUsuario}' excluído.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }
}
```

`Senha` is deliberately cleared (not populated) by `Editar` — leaving it empty and saving means "keep the current password" (see `Salvar`'s `string.IsNullOrEmpty(Senha) ? null : Senha`), matching `UserService.Update`'s optional-password contract from Task 2.

- [ ] **Step 2: Create `UsuarioView.xaml`**

Create `Lojinha.App/Views/UsuarioView.xaml`:

```xml
<UserControl x:Class="Lojinha.App.Views.UsuarioView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
             mc:Ignorable="d">
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <ui:Card Grid.Row="0" Margin="0,0,0,16">
            <StackPanel Orientation="Horizontal">
                <ui:TextBox Width="200" PlaceholderText="Nome de usuário"
                            Text="{Binding NomeUsuario, UpdateSourceTrigger=PropertyChanged}" />
                <ui:PasswordBox Width="200" Margin="12,0,0,0" PlaceholderText="Senha"
                                Password="{Binding Senha, UpdateSourceTrigger=PropertyChanged}" />
                <ComboBox Width="130" Margin="12,0,0,0" ItemsSource="{Binding Papeis}"
                          SelectedItem="{Binding PapelSelecionado}" />
                <ui:Button Content="Adicionar" Margin="12,0,0,0" Appearance="Primary"
                           Icon="{ui:SymbolIcon Symbol=Add24}"
                           Visibility="{Binding EmEdicao, Converter={StaticResource BooleanToVisibilityConverter}, ConverterParameter=Invert}"
                           Command="{Binding AdicionarCommand}" />
                <ui:Button Content="Salvar" Margin="12,0,0,0" Appearance="Primary"
                           Icon="{ui:SymbolIcon Symbol=Save24}"
                           Visibility="{Binding EmEdicao, Converter={StaticResource BooleanToVisibilityConverter}}"
                           Command="{Binding SalvarCommand}" />
                <ui:Button Content="Cancelar" Margin="8,0,0,0"
                           Icon="{ui:SymbolIcon Symbol=Dismiss24}"
                           Visibility="{Binding EmEdicao, Converter={StaticResource BooleanToVisibilityConverter}}"
                           Command="{Binding CancelarCommand}" />
            </StackPanel>
        </ui:Card>

        <Grid Grid.Row="1">
            <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center"
                        Visibility="{Binding Usuarios.Count, Converter={StaticResource CountToVisibilityConverter}}">
                <ui:SymbolIcon Symbol="Person24" FontSize="48" HorizontalAlignment="Center" Opacity="0.5" />
                <TextBlock Text="Nenhum usuário cadastrado ainda" Margin="0,8,0,0" Opacity="0.7"
                           HorizontalAlignment="Center" />
            </StackPanel>

            <DataGrid ItemsSource="{Binding Usuarios}" AutoGenerateColumns="False" IsReadOnly="True"
                      Visibility="{Binding Usuarios.Count, Converter={StaticResource CountToVisibilityConverter}, ConverterParameter=Invert}">
                <DataGrid.Columns>
                    <DataGridTextColumn Header="Id" Binding="{Binding Id}" Width="60" />
                    <DataGridTextColumn Header="Usuário" Binding="{Binding NomeUsuario}" Width="*" />
                    <DataGridTextColumn Header="Papel" Binding="{Binding Papel}" Width="120" />
                    <DataGridTemplateColumn Header="" Width="110">
                        <DataGridTemplateColumn.CellTemplate>
                            <DataTemplate>
                                <StackPanel Orientation="Horizontal">
                                    <ui:Button Icon="{ui:SymbolIcon Symbol=Edit24}"
                                               Command="{Binding DataContext.EditarCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                               CommandParameter="{Binding}" />
                                    <ui:Button Appearance="Danger" Margin="4,0,0,0" Icon="{ui:SymbolIcon Symbol=Delete24}"
                                               Command="{Binding DataContext.ExcluirCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                               CommandParameter="{Binding}" />
                                </StackPanel>
                            </DataTemplate>
                        </DataGridTemplateColumn.CellTemplate>
                    </DataGridTemplateColumn>
                </DataGrid.Columns>
            </DataGrid>
        </Grid>
    </Grid>
</UserControl>
```

- [ ] **Step 3: Create `UsuarioView.xaml.cs`**

Create `Lojinha.App/Views/UsuarioView.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace Lojinha.App.Views;

public partial class UsuarioView : UserControl
{
    public UsuarioView()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 4: Add `Usuarios` to `MainViewModel`**

Replace the full contents of `Lojinha.App/ViewModels/MainViewModel.cs` with:

```csharp
namespace Lojinha.App.ViewModels;

public class MainViewModel
{
    public CategoryViewModel Categorias { get; }
    public SupplierViewModel Fornecedores { get; }
    public ProductViewModel Produtos { get; }
    public StockViewModel Estoque { get; }
    public SalesViewModel Vendas { get; }
    public UserViewModel Usuarios { get; }

    public MainViewModel(CategoryViewModel categorias, SupplierViewModel fornecedores, ProductViewModel produtos, StockViewModel estoque, SalesViewModel vendas, UserViewModel usuarios)
    {
        Categorias = categorias;
        Fornecedores = fornecedores;
        Produtos = produtos;
        Estoque = estoque;
        Vendas = vendas;
        Usuarios = usuarios;
    }
}
```

- [ ] **Step 5: Register `UserViewModel` in `App.xaml.cs`**

In `Lojinha.App/App.xaml.cs`, in `ConfigureServices`, add `services.AddScoped<UserViewModel>();` right after `services.AddScoped<SalesViewModel>();`.

- [ ] **Step 6: Add the Usuários nav item to `MainWindow.xaml`**

In `Lojinha.App/MainWindow.xaml`, inside `<ui:NavigationView.MenuItems>`, add a 6th item after the `VendasItem` `NavigationViewItem`, before `</ui:NavigationView.MenuItems>`:

```xml
                <ui:NavigationViewItem x:Name="UsuariosItem" Content="Usuários" TargetPageTag="usuarios"
                                        Click="NavigationViewItem_OnClick">
                    <ui:NavigationViewItem.Icon>
                        <ui:SymbolIcon Symbol="Person24" />
                    </ui:NavigationViewItem.Icon>
                </ui:NavigationViewItem>
```

- [ ] **Step 7: Wire Usuários navigation in `MainWindow.xaml.cs`**

In `Lojinha.App/MainWindow.xaml.cs`, make these four changes:

1. Add `using Lojinha.App.Views;` is already present; add a field next to the other five views:

```csharp
    private readonly UsuarioView _usuarioView = new();
```

2. Add the `UsuariosItem.IsActive` line to `NavigationViewItem_OnClick`:

```csharp
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
```

3. Add the `"usuarios"` case to the `switch` inside `NavigateTo`:

```csharp
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
```

4. Add the `"usuarios"` case to the `switch` inside `RefreshViewModel`:

```csharp
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
```

- [ ] **Step 8: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 9: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 53 tests total.

- [ ] **Step 10: Manual smoke check**

Run: `dotnet run --project Lojinha.App` (log in with the Admin created in Task 4). Expected: a 6th "Usuários" sidebar item appears (visible to everyone for now, that's fixed in Task 6). Clicking it shows the Admin user created during first-access in the grid. Add a second user with Papel "Vendedor"; edit it (leave Senha blank, change only the Papel or name) and confirm the change saved without needing to re-enter the password; try excluding the only Admin (the very first user, if it's the only Admin) and confirm the "não é possível excluir o último administrador" error shows via snackbar.

- [ ] **Step 11: Commit**

```bash
git add Lojinha.App/ViewModels/UserViewModel.cs Lojinha.App/Views/UsuarioView.xaml Lojinha.App/Views/UsuarioView.xaml.cs Lojinha.App/ViewModels/MainViewModel.cs Lojinha.App/App.xaml.cs Lojinha.App/MainWindow.xaml Lojinha.App/MainWindow.xaml.cs
git commit -m "feat: add Usuários screen (create/edit/delete users and roles)"
```

---

