# Desconto e Troco Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add per-item and per-sale discounts (currency or percentage) to the Vendas screen, plus received-amount/change tracking for cash sales, with Vendedor-applied discounts requiring on-the-spot Admin authorization.

**Architecture:** `SalesService.RegisterSale` gains discount/valor-recebido inputs and validation. A new `AutorizacaoWindow` (mirroring the existing `LoginWindow` pattern) provides the supervisor-override check for non-Admin users, surfaced through a new `IAuthorizationService`. `SalesViewModel`'s cart item type (`ItemCarrinho`) changes from an immutable record to an observable class so inline per-item discount edits update totals live.

**Tech Stack:** .NET 8, WPF, WPF-UI 4.3.0, CommunityToolkit.Mvvm, EF Core 8 + SQLite, xUnit.

## Global Constraints

- Every service method that fails throws `ArgumentException`/`InvalidOperationException` with a Portuguese, user-facing message, consistent with existing services.
- `SalesService` does not re-validate that `autorizadoPor` names an actual Admin — role gating is a UI-layer concern in this codebase (see `PodeCancelarVenda`/`PodeGerenciarEstoque`); the `SalesViewModel` is the trust boundary that only ever passes a name obtained from a genuine authorization.
- Every `ui:PasswordBox.Password` binding must include `Mode=TwoWay` explicitly. The WPF-UI `PasswordBox.Password` dependency property does not declare `BindsTwoWayByDefault`, so omitting `Mode=TwoWay` silently drops typed input — this exact bug was just fixed on `LoginWindow`/`UsuarioView` and must not regress on the new `AutorizacaoWindow`.
- No automated UI tests in this plan (per established project convention) — frontend tasks are verified by `dotnet build` + a manual smoke run.
- All new/changed UI copy is in Portuguese.
- Discount calculation order: item discounts apply first, each against its own item's subtotal (`Quantidade * PrecoUnitario`). The resulting item subtotals are summed into a cart subtotal, and the sale-level discount applies against that cart subtotal. Never compute both levels off the original pre-discount total.

---

### Task 1: `TipoDesconto` enum, `Sale`/`SaleItem` model changes, migration

**Files:**
- Create: `Lojinha.Data/Models/TipoDesconto.cs`
- Modify: `Lojinha.Data/Models/Sale.cs`
- Modify: `Lojinha.Data/Models/SaleItem.cs`
- Modify: `Lojinha.Data/LojinhaDbContext.cs`
- Migration: generated under `Lojinha.Data/Migrations/`

**Interfaces:**
- Produces: `TipoDesconto` enum (`Valor`, `Percentual`); `Sale.DescontoValor` (`decimal`), `Sale.ValorRecebido` (`decimal?`), `Sale.Troco` (`decimal?`), `Sale.AutorizadoPor` (`string?`); `SaleItem.DescontoValor` (`decimal`) — consumed by Task 2 (`SalesService`) and Task 4 (`SalesViewModel`).

- [ ] **Step 1: Create the `TipoDesconto` enum**

Create `Lojinha.Data/Models/TipoDesconto.cs`:

```csharp
namespace Lojinha.Data.Models;

public enum TipoDesconto
{
    Valor,
    Percentual
}
```

- [ ] **Step 2: Add discount/payment fields to `Sale`**

In `Lojinha.Data/Models/Sale.cs`, add these properties after `UsuarioNome`:

```csharp
    public decimal DescontoValor { get; set; }
    public decimal? ValorRecebido { get; set; }
    public decimal? Troco { get; set; }
    public string? AutorizadoPor { get; set; }
```

- [ ] **Step 3: Add discount field to `SaleItem`**

In `Lojinha.Data/Models/SaleItem.cs`, add this property after `PrecoUnitario` (before the existing `Subtotal` computed property):

```csharp
    public decimal DescontoValor { get; set; }
```

Then add a computed property after the existing `Subtotal` property:

```csharp
    public decimal SubtotalComDesconto => Subtotal - DescontoValor;
```

- [ ] **Step 4: Wire precision configuration into `LojinhaDbContext`**

In `Lojinha.Data/LojinhaDbContext.cs`, inside `OnModelCreating`, immediately after the existing block:

```csharp
        modelBuilder.Entity<SaleItem>()
            .Property(i => i.PrecoUnitario)
            .HasPrecision(10, 2);
```

add:

```csharp
        modelBuilder.Entity<SaleItem>()
            .Property(i => i.DescontoValor)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Sale>()
            .Property(s => s.DescontoValor)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Sale>()
            .Property(s => s.ValorRecebido)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Sale>()
            .Property(s => s.Troco)
            .HasPrecision(10, 2);
```

- [ ] **Step 5: Generate the EF Core migration**

Run: `dotnet ef migrations add AddDescontoTroco --project Lojinha.Data`
Expected: no errors; two new files appear under `Lojinha.Data/Migrations/` (a new `..._AddDescontoTroco.cs` migration + matching `.Designer.cs`), and `Lojinha.Data/Migrations/LojinhaDbContextModelSnapshot.cs` is updated with the new columns.

- [ ] **Step 6: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 7: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 54 tests total (unchanged — this task adds no tests, just schema).

- [ ] **Step 8: Commit**

```bash
git add Lojinha.Data/Models/TipoDesconto.cs Lojinha.Data/Models/Sale.cs Lojinha.Data/Models/SaleItem.cs Lojinha.Data/LojinhaDbContext.cs Lojinha.Data/Migrations
git commit -m "feat: add TipoDesconto enum and Sale/SaleItem discount and troco fields"
```

---

### Task 2: `SalesService.RegisterSale` — discount, valor recebido, troco, autorização

**Files:**
- Modify: `Lojinha.Services/SalesService.cs`
- Test: `Lojinha.Services.Tests/SalesServiceTests.cs`
- Modify: `Lojinha.App/ViewModels/SalesViewModel.cs` (minimal compatibility fix, see Step 7 — full wiring lands in Task 4)

**Interfaces:**
- Consumes: `Sale.DescontoValor`/`ValorRecebido`/`Troco`/`AutorizadoPor`, `SaleItem.DescontoValor` (Task 1).
- Produces: `SalesService.RegisterSale(IEnumerable<(int ProductId, decimal Quantidade, decimal DescontoItem)> itens, FormaPagamento formaPagamento, string? usuarioNome = null, decimal descontoVenda = 0, decimal? valorRecebido = null, string? autorizadoPor = null)` — consumed by Task 4 (`SalesViewModel.FinalizarVenda`).

- [ ] **Step 1: Update existing test call sites to the new 3-element item tuple**

In `Lojinha.Services.Tests/SalesServiceTests.cs`, every existing call to `_service.RegisterSale` passes 2-element item tuples (`(product.Id, 3m)`). Update each to a 3-element tuple with `0m` as the (unused, for these tests) discount, and add a `valorRecebido` for every existing Dinheiro call (the new validation requires it). Apply these exact replacements:

Replace:
```csharp
        Assert.Throws<ArgumentException>(() => _service.RegisterSale(Array.Empty<(int, decimal)>(), FormaPagamento.Dinheiro));
```
with:
```csharp
        Assert.Throws<ArgumentException>(() => _service.RegisterSale(Array.Empty<(int, decimal, decimal)>(), FormaPagamento.Dinheiro));
```

Replace:
```csharp
        Assert.Throws<ArgumentException>(() => _service.RegisterSale(new[] { (product.Id, 0m) }, FormaPagamento.Dinheiro));
```
with:
```csharp
        Assert.Throws<ArgumentException>(() => _service.RegisterSale(new[] { (product.Id, 0m, 0m) }, FormaPagamento.Dinheiro));
```

Replace:
```csharp
        var sale = _service.RegisterSale(new[] { (product.Id, 3m) }, FormaPagamento.Dinheiro);

        Assert.Equal(24m, sale.Total);
```
with:
```csharp
        var sale = _service.RegisterSale(new[] { (product.Id, 3m, 0m) }, FormaPagamento.Dinheiro, valorRecebido: 24m);

        Assert.Equal(24m, sale.Total);
```

Replace:
```csharp
        var sale = _service.RegisterSale(new[] { (product1.Id, 2m), (product2.Id, 4m) }, FormaPagamento.Cartao);
```
with:
```csharp
        var sale = _service.RegisterSale(new[] { (product1.Id, 2m, 0m), (product2.Id, 4m, 0m) }, FormaPagamento.Cartao);
```

Replace:
```csharp
        Assert.Throws<InvalidOperationException>(() =>
            _service.RegisterSale(new[] { (product1.Id, 2m), (product2.Id, 5m) }, FormaPagamento.Pix));
```
with:
```csharp
        Assert.Throws<InvalidOperationException>(() =>
            _service.RegisterSale(new[] { (product1.Id, 2m, 0m), (product2.Id, 5m, 0m) }, FormaPagamento.Pix));
```

Replace:
```csharp
        _service.RegisterSale(new[] { (product.Id, 1m) }, FormaPagamento.Dinheiro);

        product.PrecoVenda = 20m;
```
with:
```csharp
        _service.RegisterSale(new[] { (product.Id, 1m, 0m) }, FormaPagamento.Dinheiro, valorRecebido: 8m);

        product.PrecoVenda = 20m;
```

Replace:
```csharp
        var sale = _service.RegisterSale(new[] { (product.Id, 4m) }, FormaPagamento.Dinheiro);

        _service.CancelSale(sale.Id);
```
with:
```csharp
        var sale = _service.RegisterSale(new[] { (product.Id, 4m, 0m) }, FormaPagamento.Dinheiro, valorRecebido: 32m);

        _service.CancelSale(sale.Id);
```

Replace:
```csharp
        var sale = _service.RegisterSale(new[] { (product.Id, 1m) }, FormaPagamento.Dinheiro);
        _service.CancelSale(sale.Id);

        Assert.Throws<InvalidOperationException>(() => _service.CancelSale(sale.Id));
```
with:
```csharp
        var sale = _service.RegisterSale(new[] { (product.Id, 1m, 0m) }, FormaPagamento.Dinheiro, valorRecebido: 8m);
        _service.CancelSale(sale.Id);

        Assert.Throws<InvalidOperationException>(() => _service.CancelSale(sale.Id));
```

Replace:
```csharp
        var sale1 = _service.RegisterSale(new[] { (product.Id, 1m) }, FormaPagamento.Dinheiro);
        var sale2 = _service.RegisterSale(new[] { (product.Id, 1m) }, FormaPagamento.Cartao);
```
with:
```csharp
        var sale1 = _service.RegisterSale(new[] { (product.Id, 1m, 0m) }, FormaPagamento.Dinheiro, valorRecebido: 8m);
        var sale2 = _service.RegisterSale(new[] { (product.Id, 1m, 0m) }, FormaPagamento.Cartao);
```

Replace:
```csharp
        var sale = _service.RegisterSale(new[] { (product.Id, 1m) }, FormaPagamento.Dinheiro, "vendedor1");

        Assert.Equal("vendedor1", sale.UsuarioNome);
```
with:
```csharp
        var sale = _service.RegisterSale(new[] { (product.Id, 1m, 0m) }, FormaPagamento.Dinheiro, "vendedor1", valorRecebido: 8m);

        Assert.Equal("vendedor1", sale.UsuarioNome);
```

- [ ] **Step 2: Add new failing tests for discount/troco/authorization behavior**

Append these to `SalesServiceTests.cs`, before the closing `}` of the class:

```csharp
    [Fact]
    public void RegisterSale_ThrowsWhenItemDescontoExceedsSubtotal()
    {
        var product = CreateProduct(precoVenda: 8m);
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        Assert.Throws<ArgumentException>(() =>
            _service.RegisterSale(new[] { (product.Id, 1m, 9m) }, FormaPagamento.Cartao));
    }

    [Fact]
    public void RegisterSale_ThrowsWhenDescontoVendaExceedsSubtotal()
    {
        var product = CreateProduct(precoVenda: 8m);
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        Assert.Throws<ArgumentException>(() =>
            _service.RegisterSale(new[] { (product.Id, 1m, 0m) }, FormaPagamento.Cartao, descontoVenda: 9m));
    }

    [Fact]
    public void RegisterSale_AppliesItemAndVendaDesconto_ComputesCorrectTotal()
    {
        var product = CreateProduct(precoVenda: 10m);
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        var sale = _service.RegisterSale(new[] { (product.Id, 2m, 4m) }, FormaPagamento.Cartao, descontoVenda: 3m);

        Assert.Equal(3m, sale.DescontoValor);
        Assert.Equal(4m, sale.Items.Single().DescontoValor);
        Assert.Equal(13m, sale.Total);
    }

    [Fact]
    public void RegisterSale_Dinheiro_ThrowsWhenValorRecebidoMissing()
    {
        var product = CreateProduct(precoVenda: 8m);
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        Assert.Throws<ArgumentException>(() =>
            _service.RegisterSale(new[] { (product.Id, 1m, 0m) }, FormaPagamento.Dinheiro));
    }

    [Fact]
    public void RegisterSale_Dinheiro_ThrowsWhenValorRecebidoBelowTotal()
    {
        var product = CreateProduct(precoVenda: 8m);
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        Assert.Throws<ArgumentException>(() =>
            _service.RegisterSale(new[] { (product.Id, 1m, 0m) }, FormaPagamento.Dinheiro, valorRecebido: 7m));
    }

    [Fact]
    public void RegisterSale_Dinheiro_ComputesTroco()
    {
        var product = CreateProduct(precoVenda: 8m);
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        var sale = _service.RegisterSale(new[] { (product.Id, 1m, 0m) }, FormaPagamento.Dinheiro, valorRecebido: 10m);

        Assert.Equal(10m, sale.ValorRecebido);
        Assert.Equal(2m, sale.Troco);
    }

    [Fact]
    public void RegisterSale_NonDinheiro_IgnoresValorRecebido()
    {
        var product = CreateProduct(precoVenda: 8m);
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        var sale = _service.RegisterSale(new[] { (product.Id, 1m, 0m) }, FormaPagamento.Cartao, valorRecebido: 50m);

        Assert.Null(sale.ValorRecebido);
        Assert.Null(sale.Troco);
    }

    [Fact]
    public void RegisterSale_StoresAutorizadoPorWithoutRoleRevalidation()
    {
        var product = CreateProduct(precoVenda: 8m);
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        var sale = _service.RegisterSale(new[] { (product.Id, 1m, 2m) }, FormaPagamento.Cartao, autorizadoPor: "qualquer-nome");

        Assert.Equal("qualquer-nome", sale.AutorizadoPor);
    }
```

- [ ] **Step 3: Run tests to verify the expected build failure**

Run: `dotnet test --filter "FullyQualifiedName~SalesServiceTests"`
Expected: build FAILS — `SalesServiceTests.cs` now calls `RegisterSale` with 3-element tuples and new named parameters that `SalesService.RegisterSale` (still on its old 2-tuple signature) doesn't have. This compile error is the expected RED state.

- [ ] **Step 4: Implement the new `RegisterSale` signature and validation**

In `Lojinha.Services/SalesService.cs`, replace the entire `RegisterSale` method with:

```csharp
    public Sale RegisterSale(
        IEnumerable<(int ProductId, decimal Quantidade, decimal DescontoItem)> itens,
        FormaPagamento formaPagamento,
        string? usuarioNome = null,
        decimal descontoVenda = 0,
        decimal? valorRecebido = null,
        string? autorizadoPor = null)
    {
        var itensList = itens.ToList();
        if (itensList.Count == 0)
        {
            throw new ArgumentException("Adicione ao menos um item à venda.", nameof(itens));
        }

        foreach (var item in itensList)
        {
            if (item.Quantidade <= 0)
            {
                throw new ArgumentException("Quantidade deve ser maior que zero.", nameof(itens));
            }
        }

        var quantidadePorProduto = itensList
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantidade));

        var produtos = new Dictionary<int, Product>();
        foreach (var produtoId in quantidadePorProduto.Keys)
        {
            var produto = _context.Products.Find(produtoId)
                ?? throw new InvalidOperationException("Produto não encontrado.");
            produtos[produtoId] = produto;

            if (_stockService.GetCurrentStock(produtoId) < quantidadePorProduto[produtoId])
            {
                throw new InvalidOperationException($"Estoque insuficiente para '{produto.Nome}'. Disponível: {_stockService.GetCurrentStock(produtoId)}.");
            }
        }

        var sale = new Sale
        {
            DataHora = DateTime.Now,
            FormaPagamento = formaPagamento,
            Cancelada = false,
            UsuarioNome = usuarioNome
        };

        decimal subtotalCarrinho = 0;
        foreach (var item in itensList)
        {
            var produto = produtos[item.ProductId];
            var itemSubtotal = item.Quantidade * produto.PrecoVenda;

            if (item.DescontoItem < 0 || item.DescontoItem > itemSubtotal)
            {
                throw new ArgumentException("Desconto do item não pode ser maior que o subtotal.", nameof(itens));
            }

            var saleItem = new SaleItem
            {
                ProductId = item.ProductId,
                Quantidade = item.Quantidade,
                PrecoUnitario = produto.PrecoVenda,
                DescontoValor = item.DescontoItem
            };
            sale.Items.Add(saleItem);
            subtotalCarrinho += itemSubtotal - item.DescontoItem;
        }

        if (descontoVenda < 0 || descontoVenda > subtotalCarrinho)
        {
            throw new ArgumentException("Desconto da venda não pode ser maior que o subtotal.", nameof(descontoVenda));
        }

        sale.DescontoValor = descontoVenda;
        sale.Total = subtotalCarrinho - descontoVenda;
        sale.AutorizadoPor = autorizadoPor;

        if (formaPagamento == FormaPagamento.Dinheiro)
        {
            if (valorRecebido is null || valorRecebido < sale.Total)
            {
                throw new ArgumentException("Valor recebido é obrigatório e deve ser maior ou igual ao total.", nameof(valorRecebido));
            }
            sale.ValorRecebido = valorRecebido;
            sale.Troco = valorRecebido.Value - sale.Total;
        }

        _context.Sales.Add(sale);

        foreach (var item in itensList)
        {
            _stockService.DeductStock(item.ProductId, item.Quantidade);
        }

        _context.SaveChanges();

        return sale;
    }
```

- [ ] **Step 5: Run tests to verify GREEN**

Run: `dotnet test --filter "FullyQualifiedName~SalesServiceTests"`
Expected: PASS, 19/19 (11 existing + 8 new) for the class.

- [ ] **Step 6: Confirm the App-side build break**

Run: `dotnet build`
Expected: FAILS — `Lojinha.App/ViewModels/SalesViewModel.cs` reports a compile error on its `RegisterSale` call (tuple arity mismatch), because `FinalizarVenda` still calls the old 2-tuple overload. This is expected; Step 7 is the minimal fix that resolves it (`Lojinha.Services` and `Lojinha.Services.Tests` alone already build and pass at this point — only the `Lojinha.App` project is currently broken).

- [ ] **Step 7: Minimal compatibility fix to `SalesViewModel.FinalizarVenda`**

This is *not* the full discount/authorization UI wiring — that's Task 4. This step only keeps `Lojinha.App` building and functionally correct with zero discount support in the interim.

In `Lojinha.App/ViewModels/SalesViewModel.cs`, replace:

```csharp
        try
        {
            var itens = Carrinho.Select(i => (i.ProductId, i.Quantidade));
            _salesService.RegisterSale(itens, FormaPagamentoSelecionada, _session.CurrentUser?.NomeUsuario);
```

with:

```csharp
        try
        {
            var itens = Carrinho.Select(i => (i.ProductId, i.Quantidade, DescontoItem: 0m));
            var valorRecebido = FormaPagamentoSelecionada == FormaPagamento.Dinheiro ? Total : (decimal?)null;
            _salesService.RegisterSale(itens, FormaPagamentoSelecionada, _session.CurrentUser?.NomeUsuario, valorRecebido: valorRecebido);
```

- [ ] **Step 8: Run the full test suite again**

Run: `dotnet test`
Expected: PASS, 62 tests total.

- [ ] **Step 9: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 10: Commit**

```bash
git add Lojinha.Services/SalesService.cs Lojinha.Services.Tests/SalesServiceTests.cs Lojinha.App/ViewModels/SalesViewModel.cs
git commit -m "feat: add discount, valor recebido, and troco to SalesService.RegisterSale"
```

---

### Task 3: `AutorizacaoWindow` — supervisor-override authorization

**Files:**
- Create: `Lojinha.App/ViewModels/AutorizacaoViewModel.cs`
- Create: `Lojinha.App/AutorizacaoWindow.xaml`
- Create: `Lojinha.App/AutorizacaoWindow.xaml.cs`
- Create: `Lojinha.App/Services/AuthorizationService.cs`
- Modify: `Lojinha.App/App.xaml.cs`

**Interfaces:**
- Consumes: `UserService.Authenticate` (existing), `PapelUsuario.Admin` (existing).
- Produces: `IAuthorizationService.AutorizarDesconto() : string?` — consumed by Task 4 (`SalesViewModel.FinalizarVenda`).

- [ ] **Step 1: Create `AutorizacaoViewModel`**

Create `Lojinha.App/ViewModels/AutorizacaoViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lojinha.Data.Models;
using Lojinha.Services;

namespace Lojinha.App.ViewModels;

public partial class AutorizacaoViewModel : ObservableObject
{
    private readonly UserService _userService;

    [ObservableProperty]
    private string nomeUsuario = string.Empty;

    [ObservableProperty]
    private string senha = string.Empty;

    [ObservableProperty]
    private string mensagemErro = string.Empty;

    public string? NomeAutorizador { get; private set; }

    public event EventHandler? AutorizacaoConcedida;

    public AutorizacaoViewModel(UserService userService)
    {
        _userService = userService;
    }

    [RelayCommand]
    private void Autorizar()
    {
        MensagemErro = string.Empty;

        try
        {
            var usuario = _userService.Authenticate(NomeUsuario, Senha);

            if (usuario.Papel != PapelUsuario.Admin)
            {
                MensagemErro = "Apenas administradores podem autorizar desconto.";
                return;
            }

            NomeAutorizador = usuario.NomeUsuario;
            AutorizacaoConcedida?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MensagemErro = ex.Message;
        }
    }
}
```

- [ ] **Step 2: Create `AutorizacaoWindow.xaml`**

Create `Lojinha.App/AutorizacaoWindow.xaml`:

```xml
<ui:FluentWindow x:Class="Lojinha.App.AutorizacaoWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
        mc:Ignorable="d"
        Title="Lojinha" Height="320" Width="380"
        WindowStartupLocation="CenterScreen"
        ResizeMode="NoResize"
        ExtendsContentIntoTitleBar="True"
        WindowBackdropType="None">
    <Grid Margin="24">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <ui:TitleBar Grid.Row="0" Title="Lojinha" />

        <StackPanel Grid.Row="1" VerticalAlignment="Center">
            <TextBlock Text="Autorização do administrador"
                       FontWeight="Bold" FontSize="18" Margin="0,0,0,16" HorizontalAlignment="Center" />
            <TextBlock Text="Um desconto foi aplicado e precisa de autorização."
                       TextWrapping="Wrap" Opacity="0.7" Margin="0,0,0,16" HorizontalAlignment="Center" TextAlignment="Center" />

            <ui:TextBox PlaceholderText="Usuário do administrador" Margin="0,0,0,8"
                        Text="{Binding NomeUsuario, UpdateSourceTrigger=PropertyChanged}" />
            <ui:PasswordBox PlaceholderText="Senha" Margin="0,0,0,8"
                            Password="{Binding Senha, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />

            <TextBlock Text="{Binding MensagemErro}" Foreground="Red" Margin="0,0,0,8" TextWrapping="Wrap" />

            <ui:Button Content="Autorizar" Appearance="Primary" HorizontalAlignment="Stretch"
                       Command="{Binding AutorizarCommand}" />
        </StackPanel>
    </Grid>
</ui:FluentWindow>
```

- [ ] **Step 3: Create `AutorizacaoWindow.xaml.cs`**

Create `Lojinha.App/AutorizacaoWindow.xaml.cs`:

```csharp
using Lojinha.App.ViewModels;
using Wpf.Ui.Controls;

namespace Lojinha.App;

public partial class AutorizacaoWindow : FluentWindow
{
    public AutorizacaoWindow(AutorizacaoViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.AutorizacaoConcedida += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }

    public string? NomeAutorizador => (DataContext as AutorizacaoViewModel)?.NomeAutorizador;
}
```

- [ ] **Step 4: Create `AuthorizationService`**

Create `Lojinha.App/Services/AuthorizationService.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Lojinha.App.Services;

public interface IAuthorizationService
{
    string? AutorizarDesconto();
}

public class AuthorizationService : IAuthorizationService
{
    private readonly IServiceProvider _serviceProvider;

    public AuthorizationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public string? AutorizarDesconto()
    {
        var window = _serviceProvider.GetRequiredService<AutorizacaoWindow>();
        var autorizado = window.ShowDialog();
        return autorizado == true ? window.NomeAutorizador : null;
    }
}
```

- [ ] **Step 5: Register the new types in `App.xaml.cs`**

In `Lojinha.App/App.xaml.cs`, replace:

```csharp
        services.AddScoped<UserService>();
```

with:

```csharp
        services.AddScoped<UserService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
```

Then replace:

```csharp
        services.AddTransient<LoginViewModel>();
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
```

with:

```csharp
        services.AddTransient<LoginViewModel>();
        services.AddTransient<LoginWindow>();
        services.AddTransient<AutorizacaoViewModel>();
        services.AddTransient<AutorizacaoWindow>();
        services.AddTransient<MainWindow>();
```

- [ ] **Step 6: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 7: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 62 tests total (unchanged — no automated tests for this UI-only task, matching this project's convention for `LoginWindow`/`UsuarioView`).

- [ ] **Step 8: Manual smoke check**

Run: `dotnet run --project Lojinha.App` in the background, confirm the process starts and stays alive (`tasklist //FI "IMAGENAME eq Lojinha.App.exe"`), then terminate it (`taskkill //F //IM Lojinha.App.exe`). `AutorizacaoWindow` isn't reachable from the UI yet (Task 4 wires the trigger) — this step only confirms the DI registrations don't break app startup.

- [ ] **Step 9: Commit**

```bash
git add Lojinha.App/ViewModels/AutorizacaoViewModel.cs Lojinha.App/AutorizacaoWindow.xaml Lojinha.App/AutorizacaoWindow.xaml.cs Lojinha.App/Services/AuthorizationService.cs Lojinha.App/App.xaml.cs
git commit -m "feat: add AutorizacaoWindow for supervisor-override discount authorization"
```

---

### Task 4: `ItemCarrinho` becomes observable, `SalesViewModel` discount/troco/authorization wiring

**Files:**
- Modify: `Lojinha.App/ViewModels/SalesViewModel.cs`

**Interfaces:**
- Consumes: `TipoDesconto` (Task 1), `SalesService.RegisterSale` full signature (Task 2), `IAuthorizationService.AutorizarDesconto()` (Task 3).
- Produces: `ItemCarrinho.DescontoTipo`/`DescontoEntrada`/`DescontoAplicado`/`SubtotalComDesconto`; `SalesViewModel.TiposDesconto`/`TipoDescontoVenda`/`DescontoVendaEntrada`/`ValorRecebido`/`CarrinhoSubtotal`/`DescontoVendaAplicado`/`Total`/`EhDinheiro`/`Troco` — consumed by Task 5 (`VendaView.xaml`).

- [ ] **Step 1: Add the required usings**

In `Lojinha.App/ViewModels/SalesViewModel.cs`, replace the top of the file:

```csharp
using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lojinha.App.Services;
using Lojinha.Data.Models;
using Lojinha.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
```

with:

```csharp
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lojinha.App.Services;
using Lojinha.Data.Models;
using Lojinha.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
```

- [ ] **Step 2: Replace the `ItemCarrinho` record with an observable class**

Replace:

```csharp
public record ItemCarrinho(int ProductId, string Produto, decimal Quantidade, decimal PrecoUnitario)
{
    public decimal Subtotal => Quantidade * PrecoUnitario;
}
```

with:

```csharp
public partial class ItemCarrinho : ObservableObject
{
    public int ProductId { get; }
    public string Produto { get; }

    [ObservableProperty]
    private decimal quantidade;

    [ObservableProperty]
    private decimal precoUnitario;

    [ObservableProperty]
    private TipoDesconto descontoTipo = TipoDesconto.Valor;

    [ObservableProperty]
    private decimal descontoEntrada;

    public decimal Subtotal => Quantidade * PrecoUnitario;

    public decimal DescontoAplicado => DescontoTipo == TipoDesconto.Percentual
        ? Subtotal * DescontoEntrada / 100
        : DescontoEntrada;

    public decimal SubtotalComDesconto => Subtotal - DescontoAplicado;

    public ItemCarrinho(int productId, string produto, decimal quantidade, decimal precoUnitario)
    {
        ProductId = productId;
        Produto = produto;
        this.quantidade = quantidade;
        this.precoUnitario = precoUnitario;
    }

    partial void OnQuantidadeChanged(decimal value)
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(DescontoAplicado));
        OnPropertyChanged(nameof(SubtotalComDesconto));
    }

    partial void OnPrecoUnitarioChanged(decimal value)
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(DescontoAplicado));
        OnPropertyChanged(nameof(SubtotalComDesconto));
    }

    partial void OnDescontoTipoChanged(TipoDesconto value)
    {
        OnPropertyChanged(nameof(DescontoAplicado));
        OnPropertyChanged(nameof(SubtotalComDesconto));
    }

    partial void OnDescontoEntradaChanged(decimal value)
    {
        OnPropertyChanged(nameof(DescontoAplicado));
        OnPropertyChanged(nameof(SubtotalComDesconto));
    }
}
```

- [ ] **Step 3: Update `VendaHistoricoItem` with the new fields**

Replace:

```csharp
public record VendaHistoricoItem(int SaleId, DateTime DataHora, decimal Total, FormaPagamento FormaPagamento, bool Cancelada, string? UsuarioNome)
{
    public string Status => Cancelada ? "Cancelada" : "Concluída";
    public bool PodeCancelar => !Cancelada;
}
```

with:

```csharp
public record VendaHistoricoItem(int SaleId, DateTime DataHora, decimal Total, FormaPagamento FormaPagamento, bool Cancelada, string? UsuarioNome, decimal DescontoValor, decimal? ValorRecebido, decimal? Troco, string? AutorizadoPor)
{
    public string Status => Cancelada ? "Cancelada" : "Concluída";
    public bool PodeCancelar => !Cancelada;
}
```

- [ ] **Step 4: Add the discount/authorization field, properties, and change hooks**

Replace:

```csharp
    private readonly SalesService _salesService;
    private readonly ProductService _productService;
    private readonly ISnackbarService _snackbar;
    private readonly IContentDialogService _dialogService;
    private readonly SessionService _session;

    public ObservableCollection<Product> Produtos { get; } = new();
    public ObservableCollection<ItemCarrinho> Carrinho { get; } = new();
    public ObservableCollection<VendaHistoricoItem> Historico { get; } = new();
    public FormaPagamento[] FormasPagamento { get; } = Enum.GetValues<FormaPagamento>();

    [ObservableProperty]
    private string termoBusca = string.Empty;

    [ObservableProperty]
    private Product? produtoSelecionado;

    [ObservableProperty]
    private decimal quantidade;

    [ObservableProperty]
    private FormaPagamento formaPagamentoSelecionada;

    public decimal Total => Carrinho.Sum(i => i.Subtotal);

    public bool PodeCancelarVenda => _session.CurrentUser?.Papel == PapelUsuario.Admin;

    public SalesViewModel(SalesService salesService, ProductService productService, ISnackbarService snackbar, IContentDialogService dialogService, SessionService session)
    {
        _salesService = salesService;
        _productService = productService;
        _snackbar = snackbar;
        _dialogService = dialogService;
        _session = session;
        Carrinho.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Total));
        CarregarProdutos();
        CarregarHistorico();
    }
```

with:

```csharp
    private readonly SalesService _salesService;
    private readonly ProductService _productService;
    private readonly ISnackbarService _snackbar;
    private readonly IContentDialogService _dialogService;
    private readonly SessionService _session;
    private readonly IAuthorizationService _authorizationService;

    public ObservableCollection<Product> Produtos { get; } = new();
    public ObservableCollection<ItemCarrinho> Carrinho { get; } = new();
    public ObservableCollection<VendaHistoricoItem> Historico { get; } = new();
    public FormaPagamento[] FormasPagamento { get; } = Enum.GetValues<FormaPagamento>();
    public TipoDesconto[] TiposDesconto { get; } = Enum.GetValues<TipoDesconto>();

    [ObservableProperty]
    private string termoBusca = string.Empty;

    [ObservableProperty]
    private Product? produtoSelecionado;

    [ObservableProperty]
    private decimal quantidade;

    [ObservableProperty]
    private FormaPagamento formaPagamentoSelecionada;

    [ObservableProperty]
    private TipoDesconto tipoDescontoVenda = TipoDesconto.Valor;

    [ObservableProperty]
    private decimal descontoVendaEntrada;

    [ObservableProperty]
    private decimal valorRecebido;

    public decimal CarrinhoSubtotal => Carrinho.Sum(i => i.SubtotalComDesconto);

    public decimal DescontoVendaAplicado => TipoDescontoVenda == TipoDesconto.Percentual
        ? CarrinhoSubtotal * DescontoVendaEntrada / 100
        : DescontoVendaEntrada;

    public decimal Total => CarrinhoSubtotal - DescontoVendaAplicado;

    public bool EhDinheiro => FormaPagamentoSelecionada == FormaPagamento.Dinheiro;

    public decimal Troco => EhDinheiro ? Math.Max(0, ValorRecebido - Total) : 0;

    public bool PodeCancelarVenda => _session.CurrentUser?.Papel == PapelUsuario.Admin;

    public SalesViewModel(SalesService salesService, ProductService productService, ISnackbarService snackbar, IContentDialogService dialogService, SessionService session, IAuthorizationService authorizationService)
    {
        _salesService = salesService;
        _productService = productService;
        _snackbar = snackbar;
        _dialogService = dialogService;
        _session = session;
        _authorizationService = authorizationService;
        Carrinho.CollectionChanged += OnCarrinhoChanged;
        CarregarProdutos();
        CarregarHistorico();
    }

    private void OnCarrinhoChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ItemCarrinho item in e.OldItems)
            {
                item.PropertyChanged -= OnItemCarrinhoPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (ItemCarrinho item in e.NewItems)
            {
                item.PropertyChanged += OnItemCarrinhoPropertyChanged;
            }
        }

        RaiseTotaisChanged();
    }

    private void OnItemCarrinhoPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ItemCarrinho.SubtotalComDesconto))
        {
            RaiseTotaisChanged();
        }
    }

    private void RaiseTotaisChanged()
    {
        OnPropertyChanged(nameof(CarrinhoSubtotal));
        OnPropertyChanged(nameof(DescontoVendaAplicado));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(Troco));
    }

    partial void OnTipoDescontoVendaChanged(TipoDesconto value)
    {
        OnPropertyChanged(nameof(DescontoVendaAplicado));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(Troco));
    }

    partial void OnDescontoVendaEntradaChanged(decimal value)
    {
        OnPropertyChanged(nameof(DescontoVendaAplicado));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(Troco));
    }

    partial void OnValorRecebidoChanged(decimal value)
    {
        OnPropertyChanged(nameof(Troco));
    }

    partial void OnFormaPagamentoSelecionadaChanged(FormaPagamento value)
    {
        OnPropertyChanged(nameof(EhDinheiro));
        OnPropertyChanged(nameof(Troco));
    }
```

- [ ] **Step 5: Fix `Escanear`'s quantity-increment (no longer a record `with` expression)**

Replace:

```csharp
        var itemExistente = Carrinho.FirstOrDefault(i => i.ProductId == produto.Id);
        if (itemExistente is not null)
        {
            var index = Carrinho.IndexOf(itemExistente);
            Carrinho[index] = itemExistente with { Quantidade = itemExistente.Quantidade + quantidadeAdicionar };
        }
        else
        {
            Carrinho.Add(new ItemCarrinho(produto.Id, produto.Nome, quantidadeAdicionar, produto.PrecoVenda));
        }
```

with:

```csharp
        var itemExistente = Carrinho.FirstOrDefault(i => i.ProductId == produto.Id);
        if (itemExistente is not null)
        {
            itemExistente.Quantidade += quantidadeAdicionar;
        }
        else
        {
            Carrinho.Add(new ItemCarrinho(produto.Id, produto.Nome, quantidadeAdicionar, produto.PrecoVenda));
        }
```

- [ ] **Step 6: Update `CarregarHistorico` for the new `VendaHistoricoItem` fields**

Replace:

```csharp
    private void CarregarHistorico()
    {
        Historico.Clear();
        foreach (var venda in _salesService.GetSaleHistory())
        {
            Historico.Add(new VendaHistoricoItem(venda.Id, venda.DataHora, venda.Total, venda.FormaPagamento, venda.Cancelada, venda.UsuarioNome));
        }
    }
```

with:

```csharp
    private void CarregarHistorico()
    {
        Historico.Clear();
        foreach (var venda in _salesService.GetSaleHistory())
        {
            var descontoTotal = venda.DescontoValor + venda.Items.Sum(i => i.DescontoValor);
            Historico.Add(new VendaHistoricoItem(venda.Id, venda.DataHora, venda.Total, venda.FormaPagamento, venda.Cancelada, venda.UsuarioNome, descontoTotal, venda.ValorRecebido, venda.Troco, venda.AutorizadoPor));
        }
    }
```

`venda.Items` is available here because `SalesService.GetSaleHistory()` already does `.Include(s => s.Items)` — no service change needed.

- [ ] **Step 7: Rewrite `FinalizarVenda` with the authorization flow**

Replace the entire `FinalizarVenda` method (introduced by Task 2's Step 7, currently calling the service with `DescontoItem: 0m` and no `descontoVenda`/`autorizadoPor`):

```csharp
    [RelayCommand]
    private void FinalizarVenda()
    {
        if (Carrinho.Count == 0)
        {
            _snackbar.Show("Erro", "Adicione ao menos um item à venda.", ControlAppearance.Danger);
            return;
        }

        try
        {
            var itens = Carrinho.Select(i => (i.ProductId, i.Quantidade, DescontoItem: 0m));
            var valorRecebido = FormaPagamentoSelecionada == FormaPagamento.Dinheiro ? Total : (decimal?)null;
            _salesService.RegisterSale(itens, FormaPagamentoSelecionada, _session.CurrentUser?.NomeUsuario, valorRecebido: valorRecebido);
            Carrinho.Clear();
            CarregarHistorico();
            _snackbar.Show("Sucesso", "Venda registrada.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }
```

with:

```csharp
    [RelayCommand]
    private void FinalizarVenda()
    {
        if (Carrinho.Count == 0)
        {
            _snackbar.Show("Erro", "Adicione ao menos um item à venda.", ControlAppearance.Danger);
            return;
        }

        var temDesconto = Carrinho.Any(i => i.DescontoAplicado > 0) || DescontoVendaAplicado > 0;
        string? autorizadoPor = null;

        if (temDesconto)
        {
            if (_session.CurrentUser?.Papel == PapelUsuario.Admin)
            {
                autorizadoPor = _session.CurrentUser.NomeUsuario;
            }
            else
            {
                autorizadoPor = _authorizationService.AutorizarDesconto();
                if (autorizadoPor is null)
                {
                    _snackbar.Show("Erro", "Desconto não autorizado.", ControlAppearance.Danger);
                    return;
                }
            }
        }

        try
        {
            var itens = Carrinho.Select(i => (i.ProductId, i.Quantidade, i.DescontoAplicado));
            var valorRecebidoVenda = EhDinheiro ? ValorRecebido : (decimal?)null;
            _salesService.RegisterSale(itens, FormaPagamentoSelecionada, _session.CurrentUser?.NomeUsuario, DescontoVendaAplicado, valorRecebidoVenda, autorizadoPor);
            Carrinho.Clear();
            TipoDescontoVenda = TipoDesconto.Valor;
            DescontoVendaEntrada = 0;
            ValorRecebido = 0;
            CarregarHistorico();
            _snackbar.Show("Sucesso", "Venda registrada.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }
```

- [ ] **Step 8: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 9: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 62 tests total (unchanged — this task touches only `SalesViewModel`, which has no automated test coverage in this project, matching the established convention).

- [ ] **Step 10: Manual smoke check**

Run: `dotnet run --project Lojinha.App` in the background, confirm the process starts and stays alive, then terminate it. The new discount/troco fields aren't in the XAML yet (Task 5), so this only confirms `SalesViewModel`'s constructor and property wiring don't throw at startup.

- [ ] **Step 11: Commit**

```bash
git add Lojinha.App/ViewModels/SalesViewModel.cs
git commit -m "feat: wire discount, troco, and admin authorization into SalesViewModel"
```

---

### Task 5: `VendaView.xaml` — discount, valor recebido, and troco UI

**Files:**
- Modify: `Lojinha.App/Views/VendaView.xaml`

**Interfaces:**
- Consumes: all `SalesViewModel`/`ItemCarrinho` members from Task 4.

- [ ] **Step 1: Add per-item discount columns and item total to the cart grid**

In `Lojinha.App/Views/VendaView.xaml`, replace:

```xml
                    <DataGrid ItemsSource="{Binding Carrinho}" AutoGenerateColumns="False" IsReadOnly="True" MaxHeight="200"
                              Visibility="{Binding Carrinho.Count, Converter={StaticResource CountToVisibilityConverter}, ConverterParameter=Invert}">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="Produto" Binding="{Binding Produto}" Width="*" />
                            <DataGridTextColumn Header="Quantidade" Binding="{Binding Quantidade}" Width="100" />
                            <DataGridTextColumn Header="Preço unit." Binding="{Binding PrecoUnitario}" Width="100" />
                            <DataGridTextColumn Header="Subtotal" Binding="{Binding Subtotal}" Width="100" />
                            <DataGridTemplateColumn Header="" Width="60">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <ui:Button Appearance="Danger" Icon="{ui:SymbolIcon Symbol=Delete24}"
                                                   Command="{Binding DataContext.RemoverDoCarrinhoCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                                   CommandParameter="{Binding}" />
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
                        </DataGrid.Columns>
                    </DataGrid>
```

with:

```xml
                    <DataGrid ItemsSource="{Binding Carrinho}" AutoGenerateColumns="False" IsReadOnly="True" MaxHeight="200"
                              Visibility="{Binding Carrinho.Count, Converter={StaticResource CountToVisibilityConverter}, ConverterParameter=Invert}">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="Produto" Binding="{Binding Produto}" Width="*" />
                            <DataGridTextColumn Header="Quantidade" Binding="{Binding Quantidade}" Width="90" />
                            <DataGridTextColumn Header="Preço unit." Binding="{Binding PrecoUnitario}" Width="90" />
                            <DataGridTemplateColumn Header="Desconto" Width="150">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <StackPanel Orientation="Horizontal">
                                            <ComboBox Width="70" ItemsSource="{Binding DataContext.TiposDesconto, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                                      SelectedItem="{Binding DescontoTipo}" />
                                            <ui:TextBox Width="70" Margin="4,0,0,0"
                                                        Text="{Binding DescontoEntrada, UpdateSourceTrigger=PropertyChanged}" />
                                        </StackPanel>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
                            <DataGridTextColumn Header="Total item" Binding="{Binding SubtotalComDesconto, StringFormat=C}" Width="100" />
                            <DataGridTemplateColumn Header="" Width="60">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <ui:Button Appearance="Danger" Icon="{ui:SymbolIcon Symbol=Delete24}"
                                                   Command="{Binding DataContext.RemoverDoCarrinhoCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                                   CommandParameter="{Binding}" />
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
                        </DataGrid.Columns>
                    </DataGrid>
```

- [ ] **Step 2: Add sale-level discount, valor recebido, and troco fields**

Replace:

```xml
                    <WrapPanel Margin="0,12,0,0">
                        <ComboBox Width="160" Margin="0,0,8,0" ItemsSource="{Binding FormasPagamento}"
                                  SelectedItem="{Binding FormaPagamentoSelecionada}" />
                        <TextBlock Text="{Binding Total, StringFormat='Total: {0:C}'}" FontWeight="Bold" FontSize="16"
                                   VerticalAlignment="Center" Margin="12,0,0,0" />
                        <ui:Button Content="Finalizar venda" Appearance="Primary" Margin="12,0,0,0"
                                   Icon="{ui:SymbolIcon Symbol=ReceiptMoney24}"
                                   Command="{Binding FinalizarVendaCommand}" />
                    </WrapPanel>
```

with:

```xml
                    <WrapPanel Margin="0,12,0,0">
                        <ComboBox Width="90" Margin="0,0,4,8" ItemsSource="{Binding TiposDesconto}"
                                  SelectedItem="{Binding TipoDescontoVenda}" />
                        <ui:TextBox Width="90" Margin="0,0,8,8" PlaceholderText="Desconto"
                                    Text="{Binding DescontoVendaEntrada, UpdateSourceTrigger=PropertyChanged}" />
                        <ComboBox Width="160" Margin="0,0,8,8" ItemsSource="{Binding FormasPagamento}"
                                  SelectedItem="{Binding FormaPagamentoSelecionada}" />
                        <ui:TextBox Width="110" Margin="0,0,8,8" PlaceholderText="Valor recebido"
                                    Visibility="{Binding EhDinheiro, Converter={StaticResource BooleanToVisibilityConverter}}"
                                    Text="{Binding ValorRecebido, UpdateSourceTrigger=PropertyChanged}" />
                        <TextBlock Text="{Binding Troco, StringFormat='Troco: {0:C}'}" VerticalAlignment="Center" Margin="0,0,8,8"
                                   Visibility="{Binding EhDinheiro, Converter={StaticResource BooleanToVisibilityConverter}}" />
                        <TextBlock Text="{Binding Total, StringFormat='Total: {0:C}'}" FontWeight="Bold" FontSize="16"
                                   VerticalAlignment="Center" Margin="12,0,0,8" />
                        <ui:Button Content="Finalizar venda" Appearance="Primary" Margin="12,0,0,8"
                                   Icon="{ui:SymbolIcon Symbol=ReceiptMoney24}"
                                   Command="{Binding FinalizarVendaCommand}" />
                    </WrapPanel>
```

- [ ] **Step 3: Add discount and troco columns to the histórico grid**

Replace:

```xml
                            <DataGridTextColumn Header="Vendedor" Binding="{Binding UsuarioNome}" Width="120" />
```

with:

```xml
                            <DataGridTextColumn Header="Vendedor" Binding="{Binding UsuarioNome}" Width="120" />
                            <DataGridTextColumn Header="Desconto" Binding="{Binding DescontoValor, StringFormat=C}" Width="90" />
                            <DataGridTextColumn Header="Troco" Binding="{Binding Troco, StringFormat=C}" Width="90" />
```

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 62 tests total (unchanged — XAML-only task).

- [ ] **Step 6: Manual smoke check**

Run: `dotnet run --project Lojinha.App` in the background, confirm the process starts and stays alive, then terminate it. Full interactive verification (typing discounts, triggering the authorization prompt, checking troco) happens in Task 6's end-to-end walkthrough.

- [ ] **Step 7: Commit**

```bash
git add Lojinha.App/Views/VendaView.xaml
git commit -m "feat: add discount and troco fields to VendaView"
```

---

### Task 6: Final integration check

**Files:** none (verification only).

- [ ] **Step 1: Full test suite**

Run: `dotnet test`
Expected: PASS, 62 tests total, 0 failures.

- [ ] **Step 2: Full build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 3: End-to-end manual walkthrough**

Run: `dotnet run --project Lojinha.App` and, in one session:

1. Log in as Admin. Add a product to the cart, set an item-level discount (try both R$ and %), set a sale-level discount, select "Dinheiro", enter a valor recebido, finalize. Confirm: no authorization prompt appeared (Admin self-authorizes), the Total shown before finalizing matches `CarrinhoSubtotal - DescontoVendaAplicado`, and the histórico row shows the correct Desconto and Troco values.
2. Try registering a Dinheiro sale with a valor recebido smaller than the total — confirm the error snackbar `"Valor recebido é obrigatório e deve ser maior ou igual ao total."` and that no sale was registered.
3. Try setting an item discount larger than that item's own subtotal — confirm the error snackbar `"Desconto do item não pode ser maior que o subtotal."` and that no sale was registered.
4. Click "Sair"; log in as a Vendedor (create one via Usuários first if none exists). Add an item, apply any discount, click "Finalizar venda" — confirm the `AutorizacaoWindow` appears. Click "Autorizar" with blank/wrong credentials — confirm the generic auth error. Enter the Vendedor's own credentials — confirm `"Apenas administradores podem autorizar desconto."` Cancel the window (close it) — confirm the sale is *not* registered and the snackbar shows `"Desconto não autorizado."`
5. Retry the same sale, this time entering valid Admin credentials in the authorization window — confirm the sale registers, the histórico's `AutorizadoPor`-backed Desconto/Troco values are correct, and the Vendedor's own session is unaffected (still logged in as Vendedor afterward, not switched to Admin).
6. As the same Vendedor, register a Dinheiro sale with *no* discount at all — confirm no authorization prompt appears (only discounts trigger it, not the payment method) and Troco still computes correctly.

- [ ] **Step 4: Push**

```bash
git push
```
