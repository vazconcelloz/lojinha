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

