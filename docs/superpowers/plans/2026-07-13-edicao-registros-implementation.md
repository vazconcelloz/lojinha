# Edição de Registros Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add editing (Update) to Categoria, Fornecedor, and Produto — today these three screens only support create and delete.

**Architecture:** Each service gets an `Update` method mirroring the existing `Add`/`Delete` validation style. Each screen's ViewModel gains an `EditandoId` (nullable) state: clicking a new "Editar" grid button populates the existing add-form fields and switches it into edit mode (Adicionar hidden, Salvar+Cancelar shown); Salvar calls `Update` and Cancelar discards without saving. `BooleanToVisibilityConverter` (currently declared locally only in `VendaView.xaml`) is promoted to a shared `App.xaml` resource since four screens now need boolean-driven visibility.

**Tech Stack:** .NET 8, WPF, WPF-UI 4.3.0, CommunityToolkit.Mvvm, EF Core 8 + SQLite, xUnit.

## Global Constraints

- Every `Update` method uses the exact same validation rules as its sibling `Add` method (Portuguese `ArgumentException`/`InvalidOperationException` messages), plus a not-found guard (`InvalidOperationException`).
- `ProductService.Update`'s barcode-uniqueness check excludes the product being edited (`p.Id != id`) — saving a product without changing its barcode must not throw.
- `ProductService.Update` allows changing `TipoVenda` freely, no restriction based on existing stock/sales.
- Salvar does not show a confirmation dialog (only Excluir, being destructive, does).
- `WindowBackdropType` stays `None` (unrelated to this plan, just don't touch it).
- No automated UI tests in this plan — frontend tasks are verified by `dotnet build` + a manual smoke run.
- All new/changed UI copy is in Portuguese, consistent with the rest of the app.

---

### Task 1: `CategoryService.Update`

**Files:**
- Modify: `Lojinha.Services/CategoryService.cs`
- Test: `Lojinha.Services.Tests/CategoryServiceTests.cs`

**Interfaces:**
- Produces: `CategoryService.Update(int id, string nome)` — throws `ArgumentException` if `nome` is empty, `InvalidOperationException` if the category doesn't exist; otherwise updates `Nome`.

- [ ] **Step 1: Write the failing tests**

Add these three `[Fact]` methods inside `Lojinha.Services.Tests/CategoryServiceTests.cs`'s `CategoryServiceTests` class, after `Delete_ThrowsWhenCategoryHasProducts`:

```csharp
    [Fact]
    public void Update_ChangesName()
    {
        var category = _service.Add("Bebidas");

        _service.Update(category.Id, "Bebidas Alcoólicas");

        Assert.Equal("Bebidas Alcoólicas", _service.GetAll().First().Nome);
    }

    [Fact]
    public void Update_ThrowsWhenCategoryNotFound()
    {
        Assert.Throws<InvalidOperationException>(() => _service.Update(999, "Nome"));
    }

    [Fact]
    public void Update_ThrowsWhenNameIsEmpty()
    {
        var category = _service.Add("Bebidas");

        Assert.Throws<ArgumentException>(() => _service.Update(category.Id, ""));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~CategoryServiceTests"`
Expected: build error (`CategoryService` has no `Update` method) or FAIL.

- [ ] **Step 3: Implement `Update`**

In `Lojinha.Services/CategoryService.cs`, add this method after `Add`:

```csharp
    public void Update(int id, string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Nome é obrigatório.", nameof(nome));
        }

        var category = _context.Categories.Find(id);
        if (category is null)
        {
            throw new InvalidOperationException("Categoria não encontrada.");
        }

        category.Nome = nome;
        _context.SaveChanges();
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~CategoryServiceTests"`
Expected: PASS, 7 tests total for this class.

- [ ] **Step 5: Commit**

```bash
git add Lojinha.Services/CategoryService.cs Lojinha.Services.Tests/CategoryServiceTests.cs
git commit -m "feat: add CategoryService.Update"
```

---

### Task 2: `SupplierService.Update`

**Files:**
- Modify: `Lojinha.Services/SupplierService.cs`
- Test: `Lojinha.Services.Tests/SupplierServiceTests.cs`

**Interfaces:**
- Produces: `SupplierService.Update(int id, string nome, string? contato)` — throws `ArgumentException` if `nome` is empty, `InvalidOperationException` if the supplier doesn't exist; otherwise updates `Nome`/`Contato`.

- [ ] **Step 1: Write the failing tests**

Add these three `[Fact]` methods inside `Lojinha.Services.Tests/SupplierServiceTests.cs`'s `SupplierServiceTests` class, after `Delete_RemovesSupplier`:

```csharp
    [Fact]
    public void Update_ChangesNameAndContact()
    {
        var supplier = _service.Add("Padaria Insumos LTDA", "(11) 99999-0000");

        _service.Update(supplier.Id, "Padaria Insumos ME", "(11) 98888-1111");

        var atualizado = _service.GetAll().First();
        Assert.Equal("Padaria Insumos ME", atualizado.Nome);
        Assert.Equal("(11) 98888-1111", atualizado.Contato);
    }

    [Fact]
    public void Update_ThrowsWhenSupplierNotFound()
    {
        Assert.Throws<InvalidOperationException>(() => _service.Update(999, "Nome", null));
    }

    [Fact]
    public void Update_ThrowsWhenNameIsEmpty()
    {
        var supplier = _service.Add("Padaria Insumos LTDA", null);

        Assert.Throws<ArgumentException>(() => _service.Update(supplier.Id, "", null));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~SupplierServiceTests"`
Expected: build error (`SupplierService` has no `Update` method) or FAIL.

- [ ] **Step 3: Implement `Update`**

In `Lojinha.Services/SupplierService.cs`, add after `Add`:

```csharp
    public void Update(int id, string nome, string? contato)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Nome é obrigatório.", nameof(nome));
        }

        var supplier = _context.Suppliers.Find(id);
        if (supplier is null)
        {
            throw new InvalidOperationException("Fornecedor não encontrado.");
        }

        supplier.Nome = nome;
        supplier.Contato = contato;
        _context.SaveChanges();
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~SupplierServiceTests"`
Expected: PASS, 6 tests total for this class.

- [ ] **Step 5: Commit**

```bash
git add Lojinha.Services/SupplierService.cs Lojinha.Services.Tests/SupplierServiceTests.cs
git commit -m "feat: add SupplierService.Update"
```

---

### Task 3: `ProductService.Update`

**Files:**
- Modify: `Lojinha.Services/ProductService.cs`
- Test: `Lojinha.Services.Tests/ProductServiceTests.cs`

**Interfaces:**
- Produces: `ProductService.Update(int id, string nome, string codigoBarras, int categoryId, TipoVenda tipoVenda, decimal precoCusto, decimal precoVenda, decimal estoqueMinimo)` — throws `ArgumentException` if `nome` empty or `precoVenda <= 0`, `InvalidOperationException` if `codigoBarras` belongs to a different product or if the product doesn't exist; otherwise updates every field.

- [ ] **Step 1: Write the failing tests**

Add these four `[Fact]` methods inside `Lojinha.Services.Tests/ProductServiceTests.cs`'s `ProductServiceTests` class, after `Delete_ThrowsWhenProductHasSales`:

```csharp
    [Fact]
    public void Update_ChangesAllFields()
    {
        var product = _service.Add("Coca-Cola 2L", "789000000001", _category.Id, TipoVenda.Unidade, 5m, 8m, 10m);
        var novaCategoria = new Category { Nome = "Refrigerantes" };
        _context.Categories.Add(novaCategoria);
        _context.SaveChanges();

        _service.Update(product.Id, "Coca-Cola 2L Zero", "789000000002", novaCategoria.Id, TipoVenda.Peso, 6m, 9m, 12m);

        var atualizado = _service.GetAll().First();
        Assert.Equal("Coca-Cola 2L Zero", atualizado.Nome);
        Assert.Equal("789000000002", atualizado.CodigoBarras);
        Assert.Equal(novaCategoria.Id, atualizado.CategoryId);
        Assert.Equal(TipoVenda.Peso, atualizado.TipoVenda);
        Assert.Equal(6m, atualizado.PrecoCusto);
        Assert.Equal(9m, atualizado.PrecoVenda);
        Assert.Equal(12m, atualizado.EstoqueMinimo);
    }

    [Fact]
    public void Update_ThrowsWhenProductNotFound()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _service.Update(999, "Nome", "789000000001", _category.Id, TipoVenda.Unidade, 5m, 8m, 10m));
    }

    [Fact]
    public void Update_ThrowsWhenBarcodeBelongsToAnotherProduct()
    {
        var product1 = _service.Add("Coca-Cola 2L", "789000000001", _category.Id, TipoVenda.Unidade, 5m, 8m, 10m);
        _service.Add("Guaraná 2L", "789000000002", _category.Id, TipoVenda.Unidade, 4m, 7m, 10m);

        Assert.Throws<InvalidOperationException>(() =>
            _service.Update(product1.Id, "Coca-Cola 2L", "789000000002", _category.Id, TipoVenda.Unidade, 5m, 8m, 10m));
    }

    [Fact]
    public void Update_AllowsSavingWithSameBarcode()
    {
        var product = _service.Add("Coca-Cola 2L", "789000000001", _category.Id, TipoVenda.Unidade, 5m, 8m, 10m);

        _service.Update(product.Id, "Coca-Cola 2L Editado", "789000000001", _category.Id, TipoVenda.Unidade, 5m, 8m, 10m);

        Assert.Equal("Coca-Cola 2L Editado", _service.GetAll().First().Nome);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ProductServiceTests"`
Expected: build error (`ProductService` has no `Update` method) or FAIL.

- [ ] **Step 3: Implement `Update`**

In `Lojinha.Services/ProductService.cs`, add after `Add`:

```csharp
    public void Update(int id, string nome, string codigoBarras, int categoryId, TipoVenda tipoVenda, decimal precoCusto, decimal precoVenda, decimal estoqueMinimo)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Nome é obrigatório.", nameof(nome));
        }

        if (precoVenda <= 0)
        {
            throw new ArgumentException("Preço de venda deve ser maior que zero.", nameof(precoVenda));
        }

        if (_context.Products.Any(p => p.CodigoBarras == codigoBarras && p.Id != id))
        {
            throw new InvalidOperationException($"Já existe um produto com o código de barras '{codigoBarras}'.");
        }

        var product = _context.Products.Find(id);
        if (product is null)
        {
            throw new InvalidOperationException("Produto não encontrado.");
        }

        product.Nome = nome;
        product.CodigoBarras = codigoBarras;
        product.CategoryId = categoryId;
        product.TipoVenda = tipoVenda;
        product.PrecoCusto = precoCusto;
        product.PrecoVenda = precoVenda;
        product.EstoqueMinimo = estoqueMinimo;
        _context.SaveChanges();
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ProductServiceTests"`
Expected: PASS, 9 tests total for this class.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 42 tests total.

- [ ] **Step 6: Commit**

```bash
git add Lojinha.Services/ProductService.cs Lojinha.Services.Tests/ProductServiceTests.cs
git commit -m "feat: add ProductService.Update"
```

---

### Task 4: Promote `BooleanToVisibilityConverter` to a shared `App.xaml` resource

**Files:**
- Modify: `Lojinha.App/App.xaml`
- Modify: `Lojinha.App/Views/VendaView.xaml`

**Interfaces:**
- Produces: `BooleanToVisibilityConverter` (WPF's built-in `System.Windows.Controls.BooleanToVisibilityConverter`, resource key `"BooleanToVisibilityConverter"`) registered globally in `App.xaml`, alongside the existing `CountToVisibilityConverter` — consumed by Tasks 5-7's edit-mode visibility bindings, and by `VendaView.xaml`'s existing `PodeCancelar` binding (updated in this task to use the global resource instead of its own local one).

- [ ] **Step 1: Add the converter to `App.xaml`**

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
            <BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter" />
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

- [ ] **Step 2: Remove the now-redundant local declaration from `VendaView.xaml`**

In `Lojinha.App/Views/VendaView.xaml`, remove this block entirely (it's currently right after the opening `<UserControl ...>` tag, before `<ScrollViewer ...>`):

```xml
    <UserControl.Resources>
        <BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter" />
    </UserControl.Resources>
```

`VendaView.xaml`'s existing `Visibility="{Binding PodeCancelar, Converter={StaticResource BooleanToVisibilityConverter}}"` binding keeps working unchanged — it now resolves the same key from the global `App.xaml` resources instead of the view's own local ones (WPF resource lookup falls back from element to application scope automatically).

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 4: Manual smoke check**

Run: `dotnet run --project Lojinha.App`
Expected: on the Vendas screen, the sale-history "Cancelar" button still shows/hides correctly based on whether a sale is already cancelled (same behavior as before this task — this step exists only to confirm the global resource correctly replaces the local one, not to test new behavior). Close the app when done.

- [ ] **Step 5: Commit**

```bash
git add Lojinha.App/App.xaml Lojinha.App/Views/VendaView.xaml
git commit -m "refactor: promote BooleanToVisibilityConverter to a shared App.xaml resource"
```

---

### Task 5: Categoria edit UI

**Files:**
- Modify: `Lojinha.App/ViewModels/CategoryViewModel.cs`
- Modify: `Lojinha.App/Views/CategoriaView.xaml`

**Interfaces:**
- Consumes: `CategoryService.Update(int, string)` (Task 1), `BooleanToVisibilityConverter` (Task 4).
- Produces: `CategoryViewModel.EditandoId` (`int?`), `EmEdicao` (`bool`, computed), `EditarCommand` (parameter `Category`), `SalvarCommand`, `CancelarCommand`.

- [ ] **Step 1: Replace `CategoryViewModel.cs`**

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

    [ObservableProperty]
    private int? editandoId;

    public bool EmEdicao => EditandoId is not null;

    public CategoryViewModel(CategoryService service, ISnackbarService snackbar, IContentDialogService dialogService)
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
        Categorias.Clear();
        foreach (var categoria in _service.GetAll())
        {
            Categorias.Add(categoria);
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
    private void Editar(Category categoria)
    {
        EditandoId = categoria.Id;
        NovoNome = categoria.Nome;
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
            _service.Update(EditandoId.Value, NovoNome);
            NovoNome = string.Empty;
            EditandoId = null;
            Carregar();
            _snackbar.Show("Sucesso", "Categoria atualizada.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private void Cancelar()
    {
        NovoNome = string.Empty;
        EditandoId = null;
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

- [ ] **Step 2: Replace `CategoriaView.xaml`**

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

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 4: Manual smoke check**

Run: `dotnet run --project Lojinha.App`
Expected: on the Categorias tab, clicking the pencil ("Editar") icon on a row fills the top field with that category's name, hides "Adicionar", and shows "Salvar"+"Cancelar" instead; editing the name and clicking "Salvar" updates the row in the grid, shows a success snackbar, and returns to the normal "Adicionar" state; clicking "Editar" then "Cancelar" discards the change and also returns to normal, leaving the category untouched.

- [ ] **Step 5: Commit**

```bash
git add Lojinha.App/ViewModels/CategoryViewModel.cs Lojinha.App/Views/CategoriaView.xaml
git commit -m "feat: add edit flow to Categoria screen"
```

---

### Task 6: Fornecedor edit UI

**Files:**
- Modify: `Lojinha.App/ViewModels/SupplierViewModel.cs`
- Modify: `Lojinha.App/Views/FornecedorView.xaml`

**Interfaces:**
- Consumes: `SupplierService.Update(int, string, string?)` (Task 2), `BooleanToVisibilityConverter` (Task 4).
- Produces: `SupplierViewModel.EditandoId` (`int?`), `EmEdicao` (`bool`), `EditarCommand` (parameter `Supplier`), `SalvarCommand`, `CancelarCommand`.

- [ ] **Step 1: Replace `SupplierViewModel.cs`**

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

    [ObservableProperty]
    private int? editandoId;

    public bool EmEdicao => EditandoId is not null;

    public SupplierViewModel(SupplierService service, ISnackbarService snackbar, IContentDialogService dialogService)
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
        Fornecedores.Clear();
        foreach (var fornecedor in _service.GetAll())
        {
            Fornecedores.Add(fornecedor);
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
    private void Editar(Supplier fornecedor)
    {
        EditandoId = fornecedor.Id;
        NovoNome = fornecedor.Nome;
        NovoContato = fornecedor.Contato;
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
            _service.Update(EditandoId.Value, NovoNome, NovoContato);
            NovoNome = string.Empty;
            NovoContato = null;
            EditandoId = null;
            Carregar();
            _snackbar.Show("Sucesso", "Fornecedor atualizado.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private void Cancelar()
    {
        NovoNome = string.Empty;
        NovoContato = null;
        EditandoId = null;
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

- [ ] **Step 2: Replace `FornecedorView.xaml`**

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

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 4: Manual smoke check**

Run: `dotnet run --project Lojinha.App`
Expected: on the Fornecedores tab — same edit/save/cancel behavior as Categorias, with Nome+Contato fields both loading and saving correctly.

- [ ] **Step 5: Commit**

```bash
git add Lojinha.App/ViewModels/SupplierViewModel.cs Lojinha.App/Views/FornecedorView.xaml
git commit -m "feat: add edit flow to Fornecedor screen"
```

---

### Task 7: Produto edit UI

**Files:**
- Modify: `Lojinha.App/ViewModels/ProductViewModel.cs`
- Modify: `Lojinha.App/Views/ProdutoView.xaml`

**Interfaces:**
- Consumes: `ProductService.Update(int, string, string, int, TipoVenda, decimal, decimal, decimal)` (Task 3), `BooleanToVisibilityConverter` (Task 4).
- Produces: `ProductViewModel.EditandoId` (`int?`), `EmEdicao` (`bool`), `EditarCommand` (parameter `Product`), `SalvarCommand`, `CancelarCommand`.

- [ ] **Step 1: Replace `ProductViewModel.cs`**

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

    [ObservableProperty]
    private int? editandoId;

    public bool EmEdicao => EditandoId is not null;

    public ProductViewModel(ProductService productService, CategoryService categoryService, ISnackbarService snackbar, IContentDialogService dialogService)
    {
        _productService = productService;
        _categoryService = categoryService;
        _snackbar = snackbar;
        _dialogService = dialogService;
        CarregarCategorias();
        Buscar();
    }

    public void Refresh()
    {
        CarregarCategorias();
        Buscar();
    }

    private void CarregarCategorias()
    {
        var categoriaSelecionadaId = CategoriaSelecionada?.Id;

        Categorias.Clear();
        foreach (var categoria in _categoryService.GetAll())
        {
            Categorias.Add(categoria);
        }

        CategoriaSelecionada = categoriaSelecionadaId is null
            ? null
            : Categorias.FirstOrDefault(c => c.Id == categoriaSelecionadaId);
    }

    partial void OnTermoBuscaChanged(string value)
    {
        Buscar();
    }

    partial void OnEditandoIdChanged(int? value)
    {
        OnPropertyChanged(nameof(EmEdicao));
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
            LimparFormulario();
            Buscar();
            _snackbar.Show("Sucesso", "Produto adicionado.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private void Editar(Product produto)
    {
        EditandoId = produto.Id;
        Nome = produto.Nome;
        CodigoBarras = produto.CodigoBarras;
        CategoriaSelecionada = Categorias.FirstOrDefault(c => c.Id == produto.CategoryId);
        TipoVendaSelecionado = produto.TipoVenda;
        PrecoCusto = produto.PrecoCusto;
        PrecoVenda = produto.PrecoVenda;
        EstoqueMinimo = produto.EstoqueMinimo;
    }

    [RelayCommand]
    private void Salvar()
    {
        if (EditandoId is null)
        {
            return;
        }

        if (CategoriaSelecionada is null)
        {
            _snackbar.Show("Erro", "Selecione uma categoria.", ControlAppearance.Danger);
            return;
        }

        try
        {
            _productService.Update(EditandoId.Value, Nome, CodigoBarras, CategoriaSelecionada.Id, TipoVendaSelecionado, PrecoCusto, PrecoVenda, EstoqueMinimo);
            EditandoId = null;
            LimparFormulario();
            Buscar();
            _snackbar.Show("Sucesso", "Produto atualizado.", ControlAppearance.Success);
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
        Nome = string.Empty;
        CodigoBarras = string.Empty;
        PrecoCusto = 0;
        PrecoVenda = 0;
        EstoqueMinimo = 0;
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

`LimparFormulario()` extracts the field-reset logic shared by `Adicionar`, `Salvar`, and `Cancelar` (3 call sites, unlike Categoria/Fornecedor's 1-2 fields, which don't warrant a helper). It intentionally does **not** touch `CategoriaSelecionada`/`TipoVendaSelecionado`, matching the original `Adicionar`'s behavior of leaving those selections in place for convenience when adding several similar products in a row.

- [ ] **Step 2: Replace `ProdutoView.xaml`**

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
                               Visibility="{Binding EmEdicao, Converter={StaticResource BooleanToVisibilityConverter}, ConverterParameter=Invert}"
                               Command="{Binding AdicionarCommand}" />
                    <ui:Button Content="Salvar" Appearance="Primary" Icon="{ui:SymbolIcon Symbol=Save24}"
                               Visibility="{Binding EmEdicao, Converter={StaticResource BooleanToVisibilityConverter}}"
                               Command="{Binding SalvarCommand}" />
                    <ui:Button Content="Cancelar" Margin="8,0,0,0" Icon="{ui:SymbolIcon Symbol=Dismiss24}"
                               Visibility="{Binding EmEdicao, Converter={StaticResource BooleanToVisibilityConverter}}"
                               Command="{Binding CancelarCommand}" />
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

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 4: Manual smoke check**

Run: `dotnet run --project Lojinha.App`
Expected: on the Produtos tab, clicking "Editar" on a row fills all 7 fields (Nome, Código, Categoria, Tipo, Custo, Venda, Est. Mín.) with the row's current values; changing the barcode to one already used by a different product and clicking "Salvar" shows the "já existe" error and keeps the form open to fix; changing the barcode back (or leaving it unchanged) and saving updates the row and returns to normal "Adicionar" mode; "Cancelar" discards without saving.

- [ ] **Step 5: Commit**

```bash
git add Lojinha.App/ViewModels/ProductViewModel.cs Lojinha.App/Views/ProdutoView.xaml
git commit -m "feat: add edit flow to Produto screen"
```

---

### Task 8: Final integration check

**Files:** none (verification only).

- [ ] **Step 1: Full test suite**

Run: `dotnet test`
Expected: PASS, 42 tests total, 0 failures.

- [ ] **Step 2: Full build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 3: End-to-end manual walkthrough**

Run: `dotnet run --project Lojinha.App` and, in one session: edit an existing category's name and save; edit a fornecedor's contact and save; edit a produto's price and barcode and save; click "Editar" on a produto, then "Cancelar" — confirm nothing changed. Toggle dark mode and confirm the Salvar/Cancelar buttons stay legible on all three screens. Try to save a produto edit with an empty name — confirm the validation error shows and the form isn't cleared.

- [ ] **Step 4: Push**

```bash
git push
```
