# Fluent Frontend (Cadastro + Estoque) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign `Lojinha.App` with a WPF-UI (Fluent) shell — sidebar navigation, theme toggle, snackbar feedback, delete-with-confirmation — and add the missing Estoque screen, backed by new `Delete` methods on the services.

**Architecture:** `MainWindow` becomes a `Wpf.Ui.Controls.FluentWindow` hosting a `NavigationView` sidebar. Each of the 4 screens (Categorias/Fornecedores/Produtos/Estoque) is a `UserControl` + matching ViewModel, constructed once via DI and swapped into the `NavigationView`'s content area on selection via `NavigationView.ReplaceContent(view, viewModel)`. Snackbar and confirmation-dialog services are DI singletons, injected into ViewModels so they can report success/failure and confirm destructive actions without any code-behind involvement.

**Tech Stack:** .NET 8, WPF, WPF-UI 4.3.0 (Fluent controls), CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection, EF Core 8 + SQLite, xUnit.

## Global Constraints

- WPF-UI package version: `4.3.0` (already restored in `Lojinha.App.csproj` — verified via reflection against the installed assembly, all API signatures below are taken from that exact version).
- XAML namespace for WPF-UI controls: `xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"`.
- No Mica/Acrylic/blur: every `WindowBackdropType` usage in this plan is `WindowBackdropType.None`.
- No automated UI tests in this plan (per spec, `docs/superpowers/specs/2026-07-10-fluent-frontend-design.md`) — frontend tasks are verified by `dotnet build` + a manual smoke run described in each task.
- Every service `Delete` method throws `InvalidOperationException` with a Portuguese, user-facing message on failure (consistent with the existing `Add` methods' error style).
- All new/changed UI copy (button text, dialog text, snackbar text, empty-state text) is in Portuguese, consistent with the rest of the app.

---

### Task 1: `CategoryService.Delete`

**Files:**
- Modify: `Lojinha.Services/CategoryService.cs`
- Test: `Lojinha.Services.Tests/CategoryServiceTests.cs`

**Interfaces:**
- Produces: `CategoryService.Delete(int id)` — throws `InvalidOperationException` if the category doesn't exist or has products attached; otherwise removes it.

- [ ] **Step 1: Write the failing tests**

Add `using Lojinha.Data.Models;` to the top of `Lojinha.Services.Tests/CategoryServiceTests.cs` (after `using Lojinha.Data;`), then add these two `[Fact]` methods inside the `CategoryServiceTests` class, after `Add_ThrowsWhenNameIsEmpty`:

```csharp
    [Fact]
    public void Delete_RemovesCategoryWithoutProducts()
    {
        var category = _service.Add("Bebidas");

        _service.Delete(category.Id);

        Assert.Empty(_service.GetAll());
    }

    [Fact]
    public void Delete_ThrowsWhenCategoryHasProducts()
    {
        var category = _service.Add("Bebidas");
        _context.Products.Add(new Product
        {
            Nome = "Coca-Cola 2L",
            CodigoBarras = "789000000001",
            CategoryId = category.Id,
            TipoVenda = TipoVenda.Unidade,
            PrecoCusto = 5,
            PrecoVenda = 8,
            EstoqueMinimo = 10
        });
        _context.SaveChanges();

        Assert.Throws<InvalidOperationException>(() => _service.Delete(category.Id));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~CategoryServiceTests"`
Expected: build error (`CategoryService` has no `Delete` method) or FAIL.

- [ ] **Step 3: Implement `Delete`**

In `Lojinha.Services/CategoryService.cs`, add this method after `Add`:

```csharp
    public void Delete(int id)
    {
        var category = _context.Categories.Find(id);
        if (category is null)
        {
            throw new InvalidOperationException("Categoria não encontrada.");
        }

        if (_context.Products.Any(p => p.CategoryId == id))
        {
            throw new InvalidOperationException("Categoria possui produtos vinculados e não pode ser excluída.");
        }

        _context.Categories.Remove(category);
        _context.SaveChanges();
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~CategoryServiceTests"`
Expected: PASS, 4 tests total for this class.

- [ ] **Step 5: Commit**

```bash
git add Lojinha.Services/CategoryService.cs Lojinha.Services.Tests/CategoryServiceTests.cs
git commit -m "feat: add CategoryService.Delete with in-use guard"
```

---

### Task 2: `SupplierService.Delete`

**Files:**
- Modify: `Lojinha.Services/SupplierService.cs`
- Test: `Lojinha.Services.Tests/SupplierServiceTests.cs`

**Interfaces:**
- Produces: `SupplierService.Delete(int id)` — throws `InvalidOperationException` if not found; otherwise removes it (existing `StockLot.SupplierId` FK is `SetNull`, so lots referencing this supplier are simply detached).

- [ ] **Step 1: Write the failing test**

Add inside `SupplierServiceTests`, after `Add_ThrowsWhenNameIsEmpty`:

```csharp
    [Fact]
    public void Delete_RemovesSupplier()
    {
        var supplier = _service.Add("Padaria Insumos LTDA", null);

        _service.Delete(supplier.Id);

        Assert.Empty(_service.GetAll());
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~SupplierServiceTests"`
Expected: build error (no `Delete` method) or FAIL.

- [ ] **Step 3: Implement `Delete`**

In `Lojinha.Services/SupplierService.cs`, add after `Add`:

```csharp
    public void Delete(int id)
    {
        var supplier = _context.Suppliers.Find(id);
        if (supplier is null)
        {
            throw new InvalidOperationException("Fornecedor não encontrado.");
        }

        _context.Suppliers.Remove(supplier);
        _context.SaveChanges();
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~SupplierServiceTests"`
Expected: PASS, 3 tests total for this class.

- [ ] **Step 5: Commit**

```bash
git add Lojinha.Services/SupplierService.cs Lojinha.Services.Tests/SupplierServiceTests.cs
git commit -m "feat: add SupplierService.Delete"
```

---

### Task 3: `ProductService.Delete`

**Files:**
- Modify: `Lojinha.Services/ProductService.cs`
- Test: `Lojinha.Services.Tests/ProductServiceTests.cs`

**Interfaces:**
- Produces: `ProductService.Delete(int id)` — throws `InvalidOperationException` if not found; otherwise removes it (existing `StockLot.ProductId` FK is `Cascade`, so lots of this product are removed too).

- [ ] **Step 1: Write the failing test**

Add inside `ProductServiceTests`, after `Add_ThrowsWhenBarcodeAlreadyExists`:

```csharp
    [Fact]
    public void Delete_RemovesProduct()
    {
        var product = _service.Add("Coca-Cola 2L", "789000000001", _category.Id, TipoVenda.Unidade, 5m, 8m, 10m);

        _service.Delete(product.Id);

        Assert.Empty(_service.GetAll());
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ProductServiceTests"`
Expected: build error (no `Delete` method) or FAIL.

- [ ] **Step 3: Implement `Delete`**

In `Lojinha.Services/ProductService.cs`, add after `Add`:

```csharp
    public void Delete(int id)
    {
        var product = _context.Products.Find(id);
        if (product is null)
        {
            throw new InvalidOperationException("Produto não encontrado.");
        }

        _context.Products.Remove(product);
        _context.SaveChanges();
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~ProductServiceTests"`
Expected: PASS, 4 tests total for this class.

- [ ] **Step 5: Commit**

```bash
git add Lojinha.Services/ProductService.cs Lojinha.Services.Tests/ProductServiceTests.cs
git commit -m "feat: add ProductService.Delete"
```

---

### Task 4: `StockService.DeleteLot` + eager-load `Product` on alert queries

**Files:**
- Modify: `Lojinha.Services/StockService.cs`
- Test: `Lojinha.Services.Tests/StockServiceTests.cs`

**Interfaces:**
- Produces: `StockService.DeleteLot(int id)` — throws `InvalidOperationException` if not found; otherwise removes the lot.
- Also fixes `GetExpiringLots`/`GetExpiredLots` to `.Include(l => l.Product)` — the Estoque screen (Task 9) needs `lot.Product.Nome` to display which product a lot belongs to; without the `Include`, `Product` is null because EF Core has no lazy-loading/proxies configured here.

- [ ] **Step 1: Write the failing test and extend an existing one**

Add inside `StockServiceTests`, after `AddLot_ThrowsWhenQuantidadeIsNotPositive`:

```csharp
    [Fact]
    public void DeleteLot_RemovesLot()
    {
        var product = CreateProduct();
        var lot = _service.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        _service.DeleteLot(lot.Id);

        Assert.Equal(0, _service.GetCurrentStock(product.Id));
    }
```

Then update the existing `GetExpiringLots_ReturnsLotsWithinThresholdDays` test to assert the product name is populated, by changing:

```csharp
        Assert.Single(expiring);
        Assert.Equal(DateTime.Today.AddDays(3), expiring.First().DataValidade);
```

to:

```csharp
        Assert.Single(expiring);
        Assert.Equal(DateTime.Today.AddDays(3), expiring.First().DataValidade);
        Assert.Equal("Coca-Cola 2L", expiring.First().Product?.Nome);
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~StockServiceTests"`
Expected: `DeleteLot_RemovesLot` fails to build (no `DeleteLot` method); `GetExpiringLots_ReturnsLotsWithinThresholdDays` FAILs (`Product` is null).

- [ ] **Step 3: Implement `DeleteLot` and the `Include` fix**

In `Lojinha.Services/StockService.cs`, add after `AddLot`:

```csharp
    public void DeleteLot(int id)
    {
        var lot = _context.StockLots.Find(id);
        if (lot is null)
        {
            throw new InvalidOperationException("Lote não encontrado.");
        }

        _context.StockLots.Remove(lot);
        _context.SaveChanges();
    }
```

Then change `GetExpiringLots` and `GetExpiredLots` to eager-load `Product`:

```csharp
    public IEnumerable<StockLot> GetExpiringLots(int diasLimite = 7)
    {
        var limite = DateTime.Today.AddDays(diasLimite);
        return _context.StockLots
            .Include(l => l.Product)
            .Where(l => l.QuantidadeRestante > 0
                && l.DataValidade != null
                && l.DataValidade >= DateTime.Today
                && l.DataValidade <= limite)
            .ToList();
    }

    public IEnumerable<StockLot> GetExpiredLots()
    {
        return _context.StockLots
            .Include(l => l.Product)
            .Where(l => l.QuantidadeRestante > 0
                && l.DataValidade != null
                && l.DataValidade < DateTime.Today)
            .ToList();
    }
```

This requires `using Microsoft.EntityFrameworkCore;` at the top of `StockService.cs` for `.Include` — add it if not already present.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~StockServiceTests"`
Expected: PASS, 7 tests total for this class.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 18 tests total.

- [ ] **Step 6: Commit**

```bash
git add Lojinha.Services/StockService.cs Lojinha.Services.Tests/StockServiceTests.cs
git commit -m "feat: add StockService.DeleteLot and eager-load Product on alert queries"
```

---

### Task 5: WPF-UI Fluent shell (package, App resources, FluentWindow, NavigationView, theme toggle)

**Files:**
- Modify: `Lojinha.App/Lojinha.App.csproj`
- Create: `Lojinha.App/Converters/CountToVisibilityConverter.cs`
- Modify: `Lojinha.App/App.xaml`
- Modify: `Lojinha.App/App.xaml.cs`
- Modify: `Lojinha.App/MainWindow.xaml`
- Modify: `Lojinha.App/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: existing `MainViewModel` (`Categorias`, `Fornecedores`, `Produtos` properties, from `Lojinha.App/ViewModels/MainViewModel.cs`), existing `CategoriaView`/`FornecedorView`/`ProdutoView` (parameterless-constructor `UserControl`s).
- Produces: `CountToVisibilityConverter` (resource key `"CountToVisibilityConverter"` in `App.xaml`, used by every view task from here on) — binds an `int` count to `Visibility` (`0 → Visible`, else `Collapsed`; pass `ConverterParameter=Invert` to flip it). `ISnackbarService` and `IContentDialogService` registered as DI singletons, consumed by every ViewModel from Task 6 onward.

- [ ] **Step 1: Confirm the WPF-UI package reference**

Run: `dotnet add Lojinha.App/Lojinha.App.csproj package WPF-UI --version 4.3.0`
Expected: `info : PackageReference do pacote 'WPF-UI' versão '4.3.0' adicionada...` (or "already referenced" if re-run).

- [ ] **Step 2: Add the count-to-visibility converter**

Create `Lojinha.App/Converters/CountToVisibilityConverter.cs`:

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Lojinha.App.Converters;

public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value is int i ? i : 0;
        var isEmpty = count == 0;
        var invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        var visible = invert ? !isEmpty : isEmpty;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
```

- [ ] **Step 3: Merge WPF-UI resource dictionaries and register the converter in `App.xaml`**

Replace the full contents of `Lojinha.App/App.xaml` with:

```xml
<Application x:Class="Lojinha.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:Lojinha.App"
             xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
             xmlns:converters="clr-namespace:Lojinha.App.Converters">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ui:ThemesDictionary Theme="Light" />
                <ui:ControlsDictionary />
            </ResourceDictionary.MergedDictionaries>
            <converters:CountToVisibilityConverter x:Key="CountToVisibilityConverter" />
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

- [ ] **Step 4: Register Snackbar/ContentDialog services in `App.xaml.cs`**

In `Lojinha.App/App.xaml.cs`, add `using Wpf.Ui;` to the usings, then in `ConfigureServices`, add these two lines right after the `AddDbContext` call:

```csharp
        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddSingleton<IContentDialogService, ContentDialogService>();
```

- [ ] **Step 5: Rebuild `MainWindow.xaml` as a Fluent shell with a 3-item sidebar**

Replace the full contents of `Lojinha.App/MainWindow.xaml` with:

```xml
<ui:FluentWindow x:Class="Lojinha.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
        mc:Ignorable="d"
        Title="Lojinha" Height="650" Width="1000"
        WindowStartupLocation="CenterScreen"
        ExtendsContentIntoTitleBar="True"
        WindowBackdropType="None">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <ui:TitleBar Grid.Row="0" Title="Lojinha" />

        <ui:NavigationView x:Name="RootNavigation" Grid.Row="1" PaneDisplayMode="Left"
                            OpenPaneLength="200" IsBackButtonVisible="Collapsed"
                            SelectionChanged="RootNavigation_OnSelectionChanged">
            <ui:NavigationView.MenuItems>
                <ui:NavigationViewItem x:Name="CategoriasItem" Content="Categorias" TargetPageTag="categorias">
                    <ui:NavigationViewItem.Icon>
                        <ui:SymbolIcon Symbol="Tag24" />
                    </ui:NavigationViewItem.Icon>
                </ui:NavigationViewItem>
                <ui:NavigationViewItem Content="Fornecedores" TargetPageTag="fornecedores">
                    <ui:NavigationViewItem.Icon>
                        <ui:SymbolIcon Symbol="BuildingShop24" />
                    </ui:NavigationViewItem.Icon>
                </ui:NavigationViewItem>
                <ui:NavigationViewItem Content="Produtos" TargetPageTag="produtos">
                    <ui:NavigationViewItem.Icon>
                        <ui:SymbolIcon Symbol="Box24" />
                    </ui:NavigationViewItem.Icon>
                </ui:NavigationViewItem>
            </ui:NavigationView.MenuItems>
            <ui:NavigationView.PaneFooter>
                <ui:ToggleSwitch x:Name="ThemeToggle" OnContent="Escuro" OffContent="Claro" Margin="12"
                                  Checked="ThemeToggle_OnToggle" Unchecked="ThemeToggle_OnToggle" />
            </ui:NavigationView.PaneFooter>
        </ui:NavigationView>

        <ui:SnackbarPresenter x:Name="RootSnackbarPresenter" Grid.Row="0" Grid.RowSpan="2" />
        <ContentPresenter x:Name="RootContentDialogPresenter" Grid.Row="0" Grid.RowSpan="2" />
    </Grid>
</ui:FluentWindow>
```

- [ ] **Step 6: Rewrite `MainWindow.xaml.cs` to wire navigation, snackbar host, dialog host and theme toggle**

Replace the full contents of `Lojinha.App/MainWindow.xaml.cs` with:

```csharp
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

        RootNavigation.SelectedItem = CategoriasItem;
        NavigateTo("categorias");
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
            "categorias" => (_categoriaView, (object)_viewModel.Categorias),
            "fornecedores" => (_fornecedorView, _viewModel.Fornecedores),
            "produtos" => (_produtoView, _viewModel.Produtos),
            _ => (_categoriaView, _viewModel.Categorias)
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
```

- [ ] **Step 7: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 8: Manual smoke check**

Run: `dotnet run --project Lojinha.App`
Expected: window opens titled "Lojinha", sidebar shows Categorias/Fornecedores/Produtos with icons, Categorias is selected and shown by default, the theme toggle at the bottom of the sidebar switches the whole app between light and dark when clicked. Close the app (`taskkill` or close the window) when done.

- [ ] **Step 9: Commit**

```bash
git add Lojinha.App/Lojinha.App.csproj Lojinha.App/Converters/CountToVisibilityConverter.cs Lojinha.App/App.xaml Lojinha.App/App.xaml.cs Lojinha.App/MainWindow.xaml Lojinha.App/MainWindow.xaml.cs
git commit -m "feat: rebuild app shell with WPF-UI FluentWindow and NavigationView"
```

---

### Task 6: Categoria feature slice (delete + snackbar + restyle)

**Files:**
- Modify: `Lojinha.App/ViewModels/CategoryViewModel.cs`
- Modify: `Lojinha.App/Views/CategoriaView.xaml`

**Interfaces:**
- Consumes: `CategoryService.Delete(int id)` (Task 1), `ISnackbarService`/`IContentDialogService` (Task 5), `CountToVisibilityConverter` resource (Task 5).
- Produces: `CategoryViewModel.ExcluirCommand` (`IAsyncRelayCommand`, parameter `Category`) — consumed by `CategoriaView.xaml`'s delete column.

- [ ] **Step 1: Update `CategoryViewModel`**

Replace the full contents of `Lojinha.App/ViewModels/CategoryViewModel.cs` with:

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

public partial class CategoryViewModel : ObservableObject
{
    private readonly CategoryService _service;
    private readonly ISnackbarService _snackbar;
    private readonly IContentDialogService _dialogService;

    public ObservableCollection<Category> Categorias { get; } = new();

    [ObservableProperty]
    private string novoNome = string.Empty;

    public CategoryViewModel(CategoryService service, ISnackbarService snackbar, IContentDialogService dialogService)
    {
        _service = service;
        _snackbar = snackbar;
        _dialogService = dialogService;
        Carregar();
    }

    private void Carregar()
    {
        Categorias.Clear();
        foreach (var categoria in _service.GetAll())
        {
            Categorias.Add(categoria);
        }
    }

    [RelayCommand]
    private void Adicionar()
    {
        try
        {
            _service.Add(NovoNome);
            NovoNome = string.Empty;
            Carregar();
            _snackbar.Show("Sucesso", "Categoria adicionada.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private async Task Excluir(Category categoria)
    {
        var options = new SimpleContentDialogCreateOptions
        {
            Title = "Excluir categoria",
            Content = $"Tem certeza que deseja excluir '{categoria.Nome}'?",
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
            _service.Delete(categoria.Id);
            Carregar();
            _snackbar.Show("Sucesso", $"Categoria '{categoria.Nome}' excluída.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }
}
```

- [ ] **Step 2: Restyle `CategoriaView.xaml`**

Replace the full contents of `Lojinha.App/Views/CategoriaView.xaml` with:

```xml
<UserControl x:Class="Lojinha.App.Views.CategoriaView"
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
                <ui:TextBox Width="280" PlaceholderText="Nome da categoria"
                            Text="{Binding NovoNome, UpdateSourceTrigger=PropertyChanged}" />
                <ui:Button Content="Adicionar" Margin="12,0,0,0" Appearance="Primary"
                           Icon="{ui:SymbolIcon Symbol=Add24}"
                           Command="{Binding AdicionarCommand}" />
            </StackPanel>
        </ui:Card>

        <Grid Grid.Row="1">
            <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center"
                        Visibility="{Binding Categorias.Count, Converter={StaticResource CountToVisibilityConverter}}">
                <ui:SymbolIcon Symbol="Tag24" FontSize="48" HorizontalAlignment="Center" Opacity="0.5" />
                <TextBlock Text="Nenhuma categoria cadastrada ainda" Margin="0,8,0,0" Opacity="0.7"
                           HorizontalAlignment="Center" />
            </StackPanel>

            <DataGrid ItemsSource="{Binding Categorias}" AutoGenerateColumns="False" IsReadOnly="True"
                      Visibility="{Binding Categorias.Count, Converter={StaticResource CountToVisibilityConverter}, ConverterParameter=Invert}">
                <DataGrid.Columns>
                    <DataGridTextColumn Header="Id" Binding="{Binding Id}" Width="60" />
                    <DataGridTextColumn Header="Nome" Binding="{Binding Nome}" Width="*" />
                    <DataGridTemplateColumn Header="" Width="60">
                        <DataGridTemplateColumn.CellTemplate>
                            <DataTemplate>
                                <ui:Button Appearance="Danger" Icon="{ui:SymbolIcon Symbol=Delete24}"
                                           Command="{Binding DataContext.ExcluirCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                           CommandParameter="{Binding}" />
                            </DataTemplate>
                        </DataGridTemplateColumn.CellTemplate>
                    </DataGridTemplateColumn>
                </DataGrid.Columns>
            </DataGrid>
        </Grid>
    </Grid>
</UserControl>
```

`CategoriaView.xaml.cs` is unchanged (still just `InitializeComponent()`).

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 4: Manual smoke check**

Run: `dotnet run --project Lojinha.App`
Expected: on the Categorias tab — grid starts empty with the "Nenhuma categoria cadastrada ainda" placeholder; typing a name and clicking "Adicionar" makes a green success snackbar appear and the row shows up in the grid (placeholder disappears); clicking the trash icon on a row opens a confirmation dialog, and confirming removes the row and shows a success snackbar.

- [ ] **Step 5: Commit**

```bash
git add Lojinha.App/ViewModels/CategoryViewModel.cs Lojinha.App/Views/CategoriaView.xaml
git commit -m "feat: add delete-with-confirmation and Fluent restyle to Categoria screen"
```

---

### Task 7: Fornecedor feature slice (delete + snackbar + restyle)

**Files:**
- Modify: `Lojinha.App/ViewModels/SupplierViewModel.cs`
- Modify: `Lojinha.App/Views/FornecedorView.xaml`

**Interfaces:**
- Consumes: `SupplierService.Delete(int id)` (Task 2), `ISnackbarService`/`IContentDialogService` (Task 5), `CountToVisibilityConverter` (Task 5).
- Produces: `SupplierViewModel.ExcluirCommand` (`IAsyncRelayCommand`, parameter `Supplier`).

- [ ] **Step 1: Update `SupplierViewModel`**

Replace the full contents of `Lojinha.App/ViewModels/SupplierViewModel.cs` with:

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

public partial class SupplierViewModel : ObservableObject
{
    private readonly SupplierService _service;
    private readonly ISnackbarService _snackbar;
    private readonly IContentDialogService _dialogService;

    public ObservableCollection<Supplier> Fornecedores { get; } = new();

    [ObservableProperty]
    private string novoNome = string.Empty;

    [ObservableProperty]
    private string? novoContato;

    public SupplierViewModel(SupplierService service, ISnackbarService snackbar, IContentDialogService dialogService)
    {
        _service = service;
        _snackbar = snackbar;
        _dialogService = dialogService;
        Carregar();
    }

    private void Carregar()
    {
        Fornecedores.Clear();
        foreach (var fornecedor in _service.GetAll())
        {
            Fornecedores.Add(fornecedor);
        }
    }

    [RelayCommand]
    private void Adicionar()
    {
        try
        {
            _service.Add(NovoNome, NovoContato);
            NovoNome = string.Empty;
            NovoContato = null;
            Carregar();
            _snackbar.Show("Sucesso", "Fornecedor adicionado.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private async Task Excluir(Supplier fornecedor)
    {
        var options = new SimpleContentDialogCreateOptions
        {
            Title = "Excluir fornecedor",
            Content = $"Tem certeza que deseja excluir '{fornecedor.Nome}'?",
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
            _service.Delete(fornecedor.Id);
            Carregar();
            _snackbar.Show("Sucesso", $"Fornecedor '{fornecedor.Nome}' excluído.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }
}
```

- [ ] **Step 2: Restyle `FornecedorView.xaml`**

Replace the full contents of `Lojinha.App/Views/FornecedorView.xaml` with:

```xml
<UserControl x:Class="Lojinha.App.Views.FornecedorView"
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
                <ui:TextBox Width="220" PlaceholderText="Nome"
                            Text="{Binding NovoNome, UpdateSourceTrigger=PropertyChanged}" />
                <ui:TextBox Width="220" Margin="12,0,0,0" PlaceholderText="Contato"
                            Text="{Binding NovoContato, UpdateSourceTrigger=PropertyChanged}" />
                <ui:Button Content="Adicionar" Margin="12,0,0,0" Appearance="Primary"
                           Icon="{ui:SymbolIcon Symbol=Add24}"
                           Command="{Binding AdicionarCommand}" />
            </StackPanel>
        </ui:Card>

        <Grid Grid.Row="1">
            <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center"
                        Visibility="{Binding Fornecedores.Count, Converter={StaticResource CountToVisibilityConverter}}">
                <ui:SymbolIcon Symbol="BuildingShop24" FontSize="48" HorizontalAlignment="Center" Opacity="0.5" />
                <TextBlock Text="Nenhum fornecedor cadastrado ainda" Margin="0,8,0,0" Opacity="0.7"
                           HorizontalAlignment="Center" />
            </StackPanel>

            <DataGrid ItemsSource="{Binding Fornecedores}" AutoGenerateColumns="False" IsReadOnly="True"
                      Visibility="{Binding Fornecedores.Count, Converter={StaticResource CountToVisibilityConverter}, ConverterParameter=Invert}">
                <DataGrid.Columns>
                    <DataGridTextColumn Header="Id" Binding="{Binding Id}" Width="60" />
                    <DataGridTextColumn Header="Nome" Binding="{Binding Nome}" Width="*" />
                    <DataGridTextColumn Header="Contato" Binding="{Binding Contato}" Width="*" />
                    <DataGridTemplateColumn Header="" Width="60">
                        <DataGridTemplateColumn.CellTemplate>
                            <DataTemplate>
                                <ui:Button Appearance="Danger" Icon="{ui:SymbolIcon Symbol=Delete24}"
                                           Command="{Binding DataContext.ExcluirCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                           CommandParameter="{Binding}" />
                            </DataTemplate>
                        </DataGridTemplateColumn.CellTemplate>
                    </DataGridTemplateColumn>
                </DataGrid.Columns>
            </DataGrid>
        </Grid>
    </Grid>
</UserControl>
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 4: Manual smoke check**

Run: `dotnet run --project Lojinha.App`
Expected: on the Fornecedores tab — same empty-state/add/delete/snackbar behavior as Categorias, with Nome+Contato fields.

- [ ] **Step 5: Commit**

```bash
git add Lojinha.App/ViewModels/SupplierViewModel.cs Lojinha.App/Views/FornecedorView.xaml
git commit -m "feat: add delete-with-confirmation and Fluent restyle to Fornecedor screen"
```

---

### Task 8: Produto feature slice (delete + snackbar + restyle)

**Files:**
- Modify: `Lojinha.App/ViewModels/ProductViewModel.cs`
- Modify: `Lojinha.App/Views/ProdutoView.xaml`

**Interfaces:**
- Consumes: `ProductService.Delete(int id)` (Task 3), `ISnackbarService`/`IContentDialogService` (Task 5), `CountToVisibilityConverter` (Task 5).
- Produces: `ProductViewModel.ExcluirCommand` (`IAsyncRelayCommand`, parameter `Product`).

- [ ] **Step 1: Update `ProductViewModel`**

Replace the full contents of `Lojinha.App/ViewModels/ProductViewModel.cs` with:

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

public partial class ProductViewModel : ObservableObject
{
    private readonly ProductService _productService;
    private readonly CategoryService _categoryService;
    private readonly ISnackbarService _snackbar;
    private readonly IContentDialogService _dialogService;

    public ObservableCollection<Product> Produtos { get; } = new();
    public ObservableCollection<Category> Categorias { get; } = new();
    public TipoVenda[] TiposVenda { get; } = Enum.GetValues<TipoVenda>();

    [ObservableProperty]
    private string nome = string.Empty;

    [ObservableProperty]
    private string codigoBarras = string.Empty;

    [ObservableProperty]
    private Category? categoriaSelecionada;

    [ObservableProperty]
    private TipoVenda tipoVendaSelecionado;

    [ObservableProperty]
    private decimal precoCusto;

    [ObservableProperty]
    private decimal precoVenda;

    [ObservableProperty]
    private decimal estoqueMinimo;

    [ObservableProperty]
    private string termoBusca = string.Empty;

    public ProductViewModel(ProductService productService, CategoryService categoryService, ISnackbarService snackbar, IContentDialogService dialogService)
    {
        _productService = productService;
        _categoryService = categoryService;
        _snackbar = snackbar;
        _dialogService = dialogService;
        CarregarCategorias();
        Buscar();
    }

    private void CarregarCategorias()
    {
        Categorias.Clear();
        foreach (var categoria in _categoryService.GetAll())
        {
            Categorias.Add(categoria);
        }
    }

    partial void OnTermoBuscaChanged(string value)
    {
        Buscar();
    }

    [RelayCommand]
    private void Buscar()
    {
        Produtos.Clear();
        foreach (var produto in _productService.Search(TermoBusca))
        {
            Produtos.Add(produto);
        }
    }

    [RelayCommand]
    private void Adicionar()
    {
        if (CategoriaSelecionada is null)
        {
            _snackbar.Show("Erro", "Selecione uma categoria.", ControlAppearance.Danger);
            return;
        }

        try
        {
            _productService.Add(Nome, CodigoBarras, CategoriaSelecionada.Id, TipoVendaSelecionado, PrecoCusto, PrecoVenda, EstoqueMinimo);
            Nome = string.Empty;
            CodigoBarras = string.Empty;
            PrecoCusto = 0;
            PrecoVenda = 0;
            EstoqueMinimo = 0;
            Buscar();
            _snackbar.Show("Sucesso", "Produto adicionado.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private async Task Excluir(Product produto)
    {
        var options = new SimpleContentDialogCreateOptions
        {
            Title = "Excluir produto",
            Content = $"Tem certeza que deseja excluir '{produto.Nome}'? Os lotes de estoque vinculados também serão removidos.",
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
            _productService.Delete(produto.Id);
            Buscar();
            _snackbar.Show("Sucesso", $"Produto '{produto.Nome}' excluído.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }
}
```

- [ ] **Step 2: Restyle `ProdutoView.xaml`**

Replace the full contents of `Lojinha.App/Views/ProdutoView.xaml` with:

```xml
<UserControl x:Class="Lojinha.App.Views.ProdutoView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
             mc:Ignorable="d">
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <ui:Card Grid.Row="0" Margin="0,0,0,16">
            <StackPanel>
                <WrapPanel>
                    <ui:TextBox Width="200" Margin="0,0,8,8" PlaceholderText="Nome"
                                Text="{Binding Nome, UpdateSourceTrigger=PropertyChanged}" />
                    <ui:TextBox Width="150" Margin="0,0,8,8" PlaceholderText="Código de barras"
                                Text="{Binding CodigoBarras, UpdateSourceTrigger=PropertyChanged}" />
                    <ComboBox Width="150" Margin="0,0,8,8" ItemsSource="{Binding Categorias}" DisplayMemberPath="Nome"
                              SelectedItem="{Binding CategoriaSelecionada}" />
                    <ComboBox Width="110" Margin="0,0,8,8" ItemsSource="{Binding TiposVenda}"
                              SelectedItem="{Binding TipoVendaSelecionado}" />
                </WrapPanel>
                <WrapPanel>
                    <ui:TextBox Width="110" Margin="0,0,8,8" PlaceholderText="Preço custo"
                                Text="{Binding PrecoCusto, UpdateSourceTrigger=PropertyChanged}" />
                    <ui:TextBox Width="110" Margin="0,0,8,8" PlaceholderText="Preço venda"
                                Text="{Binding PrecoVenda, UpdateSourceTrigger=PropertyChanged}" />
                    <ui:TextBox Width="110" Margin="0,0,8,8" PlaceholderText="Estoque mínimo"
                                Text="{Binding EstoqueMinimo, UpdateSourceTrigger=PropertyChanged}" />
                    <ui:Button Content="Adicionar" Appearance="Primary" Icon="{ui:SymbolIcon Symbol=Add24}"
                               Command="{Binding AdicionarCommand}" />
                </WrapPanel>
            </StackPanel>
        </ui:Card>

        <ui:TextBox Grid.Row="1" Margin="0,0,0,16" PlaceholderText="Buscar por nome ou código"
                    Text="{Binding TermoBusca, UpdateSourceTrigger=PropertyChanged}" />

        <Grid Grid.Row="2">
            <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center"
                        Visibility="{Binding Produtos.Count, Converter={StaticResource CountToVisibilityConverter}}">
                <ui:SymbolIcon Symbol="Box24" FontSize="48" HorizontalAlignment="Center" Opacity="0.5" />
                <TextBlock Text="Nenhum produto cadastrado ainda" Margin="0,8,0,0" Opacity="0.7"
                           HorizontalAlignment="Center" />
            </StackPanel>

            <DataGrid ItemsSource="{Binding Produtos}" AutoGenerateColumns="False" IsReadOnly="True"
                      Visibility="{Binding Produtos.Count, Converter={StaticResource CountToVisibilityConverter}, ConverterParameter=Invert}">
                <DataGrid.Columns>
                    <DataGridTextColumn Header="Id" Binding="{Binding Id}" Width="50" />
                    <DataGridTextColumn Header="Nome" Binding="{Binding Nome}" Width="*" />
                    <DataGridTextColumn Header="Código" Binding="{Binding CodigoBarras}" Width="120" />
                    <DataGridTextColumn Header="Categoria" Binding="{Binding Category.Nome}" Width="120" />
                    <DataGridTextColumn Header="Tipo" Binding="{Binding TipoVenda}" Width="80" />
                    <DataGridTextColumn Header="Custo" Binding="{Binding PrecoCusto}" Width="80" />
                    <DataGridTextColumn Header="Venda" Binding="{Binding PrecoVenda}" Width="80" />
                    <DataGridTextColumn Header="Est. Mín." Binding="{Binding EstoqueMinimo}" Width="80" />
                    <DataGridTemplateColumn Header="" Width="60">
                        <DataGridTemplateColumn.CellTemplate>
                            <DataTemplate>
                                <ui:Button Appearance="Danger" Icon="{ui:SymbolIcon Symbol=Delete24}"
                                           Command="{Binding DataContext.ExcluirCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                           CommandParameter="{Binding}" />
                            </DataTemplate>
                        </DataGridTemplateColumn.CellTemplate>
                    </DataGridTemplateColumn>
                </DataGrid.Columns>
            </DataGrid>
        </Grid>
    </Grid>
</UserControl>
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 4: Manual smoke check**

Run: `dotnet run --project Lojinha.App`
Expected: on the Produtos tab — same empty-state/add/delete/snackbar behavior; deleting a product whose category is shown asks for confirmation and mentions its lots will be removed too.

- [ ] **Step 5: Commit**

```bash
git add Lojinha.App/ViewModels/ProductViewModel.cs Lojinha.App/Views/ProdutoView.xaml
git commit -m "feat: add delete-with-confirmation and Fluent restyle to Produto screen"
```

---

### Task 9: Estoque feature slice (new screen)

**Files:**
- Create: `Lojinha.App/ViewModels/StockViewModel.cs`
- Create: `Lojinha.App/Views/EstoqueView.xaml`
- Create: `Lojinha.App/Views/EstoqueView.xaml.cs`
- Modify: `Lojinha.App/ViewModels/MainViewModel.cs`
- Modify: `Lojinha.App/App.xaml.cs`
- Modify: `Lojinha.App/MainWindow.xaml`
- Modify: `Lojinha.App/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `StockService` (`GetCurrentStock`, `AddLot`, `GetLowStockProducts`, `GetExpiringLots`, `GetExpiredLots`, `DeleteLot` — Task 4), `ProductService.GetAll()`, `SupplierService.GetAll()`, `ISnackbarService`/`IContentDialogService` (Task 5), `CountToVisibilityConverter` (Task 5).
- Produces: `StockViewModel` with `Produtos`, `Fornecedores`, `EstoqueAtual` (`ObservableCollection<EstoqueAtualItem>`), `EstoqueBaixo` (`ObservableCollection<EstoqueBaixoItem>`), `Vencimentos` (`ObservableCollection<VencimentoItem>`), `AdicionarLoteCommand`, `ExcluirLoteCommand` (parameter `VencimentoItem`). `MainViewModel.Estoque` property (type `StockViewModel`).

- [ ] **Step 1: Create `StockViewModel`**

Create `Lojinha.App/ViewModels/StockViewModel.cs`:

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

public record EstoqueAtualItem(string Produto, decimal Quantidade);

public record EstoqueBaixoItem(string Produto, decimal QuantidadeAtual, decimal EstoqueMinimo);

public record VencimentoItem(int LoteId, string Produto, decimal QuantidadeRestante, DateTime? DataValidade, bool Vencido);

public partial class StockViewModel : ObservableObject
{
    private readonly StockService _stockService;
    private readonly ProductService _productService;
    private readonly SupplierService _supplierService;
    private readonly ISnackbarService _snackbar;
    private readonly IContentDialogService _dialogService;

    public ObservableCollection<Product> Produtos { get; } = new();
    public ObservableCollection<Supplier> Fornecedores { get; } = new();
    public ObservableCollection<EstoqueAtualItem> EstoqueAtual { get; } = new();
    public ObservableCollection<EstoqueBaixoItem> EstoqueBaixo { get; } = new();
    public ObservableCollection<VencimentoItem> Vencimentos { get; } = new();

    [ObservableProperty]
    private Product? produtoSelecionado;

    [ObservableProperty]
    private decimal quantidade;

    [ObservableProperty]
    private Supplier? fornecedorSelecionado;

    [ObservableProperty]
    private DateTime? dataValidade;

    public StockViewModel(StockService stockService, ProductService productService, SupplierService supplierService, ISnackbarService snackbar, IContentDialogService dialogService)
    {
        _stockService = stockService;
        _productService = productService;
        _supplierService = supplierService;
        _snackbar = snackbar;
        _dialogService = dialogService;
        CarregarCombos();
        AtualizarPaineis();
    }

    private void CarregarCombos()
    {
        Produtos.Clear();
        foreach (var produto in _productService.GetAll())
        {
            Produtos.Add(produto);
        }

        Fornecedores.Clear();
        foreach (var fornecedor in _supplierService.GetAll())
        {
            Fornecedores.Add(fornecedor);
        }
    }

    private void AtualizarPaineis()
    {
        EstoqueAtual.Clear();
        foreach (var produto in _productService.GetAll())
        {
            EstoqueAtual.Add(new EstoqueAtualItem(produto.Nome, _stockService.GetCurrentStock(produto.Id)));
        }

        EstoqueBaixo.Clear();
        foreach (var produto in _stockService.GetLowStockProducts())
        {
            EstoqueBaixo.Add(new EstoqueBaixoItem(produto.Nome, _stockService.GetCurrentStock(produto.Id), produto.EstoqueMinimo));
        }

        Vencimentos.Clear();
        foreach (var lote in _stockService.GetExpiredLots())
        {
            Vencimentos.Add(new VencimentoItem(lote.Id, lote.Product?.Nome ?? "-", lote.QuantidadeRestante, lote.DataValidade, true));
        }
        foreach (var lote in _stockService.GetExpiringLots())
        {
            Vencimentos.Add(new VencimentoItem(lote.Id, lote.Product?.Nome ?? "-", lote.QuantidadeRestante, lote.DataValidade, false));
        }
    }

    [RelayCommand]
    private void AdicionarLote()
    {
        if (ProdutoSelecionado is null)
        {
            _snackbar.Show("Erro", "Selecione um produto.", ControlAppearance.Danger);
            return;
        }

        try
        {
            _stockService.AddLot(ProdutoSelecionado.Id, Quantidade, DataValidade, FornecedorSelecionado?.Id);
            Quantidade = 0;
            DataValidade = null;
            AtualizarPaineis();
            _snackbar.Show("Sucesso", "Lote adicionado.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private async Task ExcluirLote(VencimentoItem item)
    {
        var options = new SimpleContentDialogCreateOptions
        {
            Title = "Excluir lote",
            Content = $"Tem certeza que deseja excluir o lote de '{item.Produto}'?",
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
            _stockService.DeleteLot(item.LoteId);
            AtualizarPaineis();
            _snackbar.Show("Sucesso", "Lote excluído.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }
}
```

- [ ] **Step 2: Create `EstoqueView.xaml`**

Create `Lojinha.App/Views/EstoqueView.xaml`:

```xml
<UserControl x:Class="Lojinha.App.Views.EstoqueView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
             mc:Ignorable="d">
    <ScrollViewer Margin="20" VerticalScrollBarVisibility="Auto">
        <StackPanel>
            <ui:Card Margin="0,0,0,16">
                <StackPanel>
                    <TextBlock Text="Entrada de lote" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />
                    <WrapPanel>
                        <ComboBox Width="220" Margin="0,0,8,8" ItemsSource="{Binding Produtos}" DisplayMemberPath="Nome"
                                  SelectedItem="{Binding ProdutoSelecionado}" />
                        <ui:TextBox Width="120" Margin="0,0,8,8" PlaceholderText="Quantidade"
                                    Text="{Binding Quantidade, UpdateSourceTrigger=PropertyChanged}" />
                        <ComboBox Width="200" Margin="0,0,8,8" ItemsSource="{Binding Fornecedores}" DisplayMemberPath="Nome"
                                  SelectedItem="{Binding FornecedorSelecionado}" />
                        <DatePicker Width="140" Margin="0,0,8,8" SelectedDate="{Binding DataValidade}" />
                        <ui:Button Content="Adicionar lote" Appearance="Primary" Icon="{ui:SymbolIcon Symbol=Add24}"
                                   Command="{Binding AdicionarLoteCommand}" />
                    </WrapPanel>
                </StackPanel>
            </ui:Card>

            <ui:Card Margin="0,0,0,16">
                <StackPanel>
                    <TextBlock Text="Estoque atual" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />
                    <DataGrid ItemsSource="{Binding EstoqueAtual}" AutoGenerateColumns="False" IsReadOnly="True" MaxHeight="240">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="Produto" Binding="{Binding Produto}" Width="*" />
                            <DataGridTextColumn Header="Quantidade" Binding="{Binding Quantidade}" Width="120" />
                        </DataGrid.Columns>
                    </DataGrid>
                </StackPanel>
            </ui:Card>

            <ui:Card Margin="0,0,0,16">
                <StackPanel>
                    <TextBlock Text="Estoque baixo" FontWeight="Bold" FontSize="16" Foreground="OrangeRed" Margin="0,0,0,12" />
                    <StackPanel Visibility="{Binding EstoqueBaixo.Count, Converter={StaticResource CountToVisibilityConverter}}">
                        <TextBlock Text="Nenhum produto abaixo do estoque mínimo." Opacity="0.7" />
                    </StackPanel>
                    <DataGrid ItemsSource="{Binding EstoqueBaixo}" AutoGenerateColumns="False" IsReadOnly="True" MaxHeight="200"
                              Visibility="{Binding EstoqueBaixo.Count, Converter={StaticResource CountToVisibilityConverter}, ConverterParameter=Invert}">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="Produto" Binding="{Binding Produto}" Width="*" />
                            <DataGridTextColumn Header="Atual" Binding="{Binding QuantidadeAtual}" Width="100" />
                            <DataGridTextColumn Header="Mínimo" Binding="{Binding EstoqueMinimo}" Width="100" />
                        </DataGrid.Columns>
                    </DataGrid>
                </StackPanel>
            </ui:Card>

            <ui:Card>
                <StackPanel>
                    <TextBlock Text="Vencimentos" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />
                    <StackPanel Visibility="{Binding Vencimentos.Count, Converter={StaticResource CountToVisibilityConverter}}">
                        <TextBlock Text="Nenhum lote vencendo ou vencido." Opacity="0.7" />
                    </StackPanel>
                    <DataGrid ItemsSource="{Binding Vencimentos}" AutoGenerateColumns="False" IsReadOnly="True" MaxHeight="240"
                              Visibility="{Binding Vencimentos.Count, Converter={StaticResource CountToVisibilityConverter}, ConverterParameter=Invert}">
                        <DataGrid.RowStyle>
                            <Style TargetType="DataGridRow">
                                <Style.Triggers>
                                    <DataTrigger Binding="{Binding Vencido}" Value="True">
                                        <Setter Property="Foreground" Value="Red" />
                                    </DataTrigger>
                                </Style.Triggers>
                            </Style>
                        </DataGrid.RowStyle>
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="Produto" Binding="{Binding Produto}" Width="*" />
                            <DataGridTextColumn Header="Quantidade" Binding="{Binding QuantidadeRestante}" Width="100" />
                            <DataGridTextColumn Header="Validade" Binding="{Binding DataValidade, StringFormat=dd/MM/yyyy}" Width="110" />
                            <DataGridTemplateColumn Header="" Width="60">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <ui:Button Appearance="Danger" Icon="{ui:SymbolIcon Symbol=Delete24}"
                                                   Command="{Binding DataContext.ExcluirLoteCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                                   CommandParameter="{Binding}" />
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
                        </DataGrid.Columns>
                    </DataGrid>
                </StackPanel>
            </ui:Card>
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

- [ ] **Step 3: Create `EstoqueView.xaml.cs`**

Create `Lojinha.App/Views/EstoqueView.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace Lojinha.App.Views;

public partial class EstoqueView : UserControl
{
    public EstoqueView()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 4: Add `Estoque` to `MainViewModel`**

Replace the full contents of `Lojinha.App/ViewModels/MainViewModel.cs` with:

```csharp
namespace Lojinha.App.ViewModels;

public class MainViewModel
{
    public CategoryViewModel Categorias { get; }
    public SupplierViewModel Fornecedores { get; }
    public ProductViewModel Produtos { get; }
    public StockViewModel Estoque { get; }

    public MainViewModel(CategoryViewModel categorias, SupplierViewModel fornecedores, ProductViewModel produtos, StockViewModel estoque)
    {
        Categorias = categorias;
        Fornecedores = fornecedores;
        Produtos = produtos;
        Estoque = estoque;
    }
}
```

- [ ] **Step 5: Register `StockViewModel` in `App.xaml.cs`**

In `Lojinha.App/App.xaml.cs`, in `ConfigureServices`, add this line right after `services.AddScoped<ProductViewModel>();`:

```csharp
        services.AddScoped<StockViewModel>();
```

- [ ] **Step 6: Add the Estoque nav item to `MainWindow.xaml`**

In `Lojinha.App/MainWindow.xaml`, inside `<ui:NavigationView.MenuItems>`, add a 4th item after the "Produtos" `NavigationViewItem`:

```xml
                <ui:NavigationViewItem Content="Estoque" TargetPageTag="estoque">
                    <ui:NavigationViewItem.Icon>
                        <ui:SymbolIcon Symbol="BoxMultiple24" />
                    </ui:NavigationViewItem.Icon>
                </ui:NavigationViewItem>
```

- [ ] **Step 7: Wire Estoque navigation in `MainWindow.xaml.cs`**

In `Lojinha.App/MainWindow.xaml.cs`:

1. Add `using Lojinha.App.Views;` is already present; add a field next to the other three views:

```csharp
    private readonly EstoqueView _estoqueView = new();
```

2. Add the `"estoque"` case to the `switch` inside `NavigateTo`:

```csharp
        (FrameworkElement view, object dataContext) = tag switch
        {
            "categorias" => (_categoriaView, (object)_viewModel.Categorias),
            "fornecedores" => (_fornecedorView, _viewModel.Fornecedores),
            "produtos" => (_produtoView, _viewModel.Produtos),
            "estoque" => (_estoqueView, _viewModel.Estoque),
            _ => (_categoriaView, _viewModel.Categorias)
        };
```

- [ ] **Step 8: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 9: Manual smoke check**

Run: `dotnet run --project Lojinha.App`
Expected: a 4th "Estoque" sidebar item appears. Clicking it shows: entry form for a lot (product combo, quantity, optional supplier combo, optional date picker) that adds a lot and shows a success snackbar; "Estoque atual" table updates with the new quantity; if the product's `EstoqueMinimo` is above its current stock it shows in "Estoque baixo"; adding a lot with a near/past expiry date makes it show in "Vencimentos" (past-due rows in red); clicking the trash icon on a "Vencimentos" row asks for confirmation and removes the lot.

- [ ] **Step 10: Commit**

```bash
git add Lojinha.App/ViewModels/StockViewModel.cs Lojinha.App/Views/EstoqueView.xaml Lojinha.App/Views/EstoqueView.xaml.cs Lojinha.App/ViewModels/MainViewModel.cs Lojinha.App/App.xaml.cs Lojinha.App/MainWindow.xaml Lojinha.App/MainWindow.xaml.cs
git commit -m "feat: add Estoque screen (lot entry, current stock, low-stock and expiry alerts)"
```

---

### Task 10: Final integration check

**Files:** none (verification only).

- [ ] **Step 1: Full test suite**

Run: `dotnet test`
Expected: PASS, 18 tests total, 0 failures.

- [ ] **Step 2: Full build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 3: End-to-end manual walkthrough**

Run: `dotnet run --project Lojinha.App` and, in one session: add a category, add a fornecedor, add a product using that category, go to Estoque and add a lot for that product with a validade 3 days from today, confirm it appears in "Vencimentos", toggle dark mode and confirm all 4 screens stay legible, delete the lot, delete the product, delete the fornecedor, delete the category — confirming each delete via the dialog and observing the success snackbar each time.

- [ ] **Step 4: Push**

```bash
git push
```
