### Task 7: Vendas — track `UsuarioNome`, hide "Cancelar" for Vendedor

**Files:**
- Modify: `Lojinha.Services/SalesService.cs`
- Test: `Lojinha.Services.Tests/SalesServiceTests.cs`
- Modify: `Lojinha.App/ViewModels/SalesViewModel.cs`
- Modify: `Lojinha.App/Views/VendaView.xaml`
- Create: `Lojinha.App/Converters/BooleanAndToVisibilityConverter.cs`
- Modify: `Lojinha.App/App.xaml`

**Interfaces:**
- Consumes: `SessionService.CurrentUser` (Task 3).
- Produces: `SalesService.RegisterSale(..., string? usuarioNome = null)` (new optional parameter, existing call sites unaffected), `SalesViewModel.PodeCancelarVenda` (`bool`, computed), `BooleanAndToVisibilityConverter` (resource key `"BooleanAndToVisibilityConverter"`).

- [ ] **Step 1: Write the failing test for `UsuarioNome` tracking**

Add this `[Fact]` inside `Lojinha.Services.Tests/SalesServiceTests.cs`'s `SalesServiceTests` class, after `GetSaleHistory_OrdersByDataHoraDescending`:

```csharp
    [Fact]
    public void RegisterSale_StoresUsuarioNomeWhenProvided()
    {
        var product = CreateProduct();
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        var sale = _service.RegisterSale(new[] { (product.Id, 1m) }, FormaPagamento.Dinheiro, "vendedor1");

        Assert.Equal("vendedor1", sale.UsuarioNome);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~SalesServiceTests"`
Expected: build error (`RegisterSale` doesn't accept a third argument yet).

- [ ] **Step 3: Add the optional parameter to `SalesService.RegisterSale`**

In `Lojinha.Services/SalesService.cs`, change the `RegisterSale` method signature and the `Sale` object construction:

```csharp
    public Sale RegisterSale(IEnumerable<(int ProductId, decimal Quantidade)> itens, FormaPagamento formaPagamento, string? usuarioNome = null)
```

and inside the method, where `sale` is constructed:

```csharp
        var sale = new Sale
        {
            DataHora = DateTime.Now,
            FormaPagamento = formaPagamento,
            Cancelada = false,
            UsuarioNome = usuarioNome
        };
```

The default value `= null` means the 10 existing calls to `RegisterSale` (two-argument form, in both `SalesServiceTests.cs` and `SalesViewModel.cs`) keep compiling unchanged.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~SalesServiceTests"`
Expected: PASS, 11 tests total for this class.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 53 tests total.

- [ ] **Step 6: Create `BooleanAndToVisibilityConverter`**

Create `Lojinha.App/Converters/BooleanAndToVisibilityConverter.cs`:

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Lojinha.App.Converters;

public class BooleanAndToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = values.All(v => v is bool b && b);
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
```

This combines two independent booleans into one `Visibility` — needed because the "Cancelar" button in Vendas' histórico must be hidden when EITHER the sale is already cancelled (`VendaHistoricoItem.PodeCancelar`, existing) OR the current user isn't an Admin (`SalesViewModel.PodeCancelarVenda`, new below) — two booleans from two different `DataContext`s that a single `Converter`/`ConverterParameter` binding can't combine.

- [ ] **Step 7: Register the converter in `App.xaml`**

In `Lojinha.App/App.xaml`, add this line right after `<BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter" />`:

```xml
            <converters:BooleanAndToVisibilityConverter x:Key="BooleanAndToVisibilityConverter" />
```

- [ ] **Step 8: Update `SalesViewModel`**

In `Lojinha.App/ViewModels/SalesViewModel.cs`:

1. Add `using Lojinha.App.Services;` to the usings.
2. Add a `SessionService _session` field, injected via the constructor:

```csharp
    private readonly SalesService _salesService;
    private readonly ProductService _productService;
    private readonly ISnackbarService _snackbar;
    private readonly IContentDialogService _dialogService;
    private readonly SessionService _session;
```

```csharp
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

3. Add the computed property, right after `Total`:

```csharp
    public bool PodeCancelarVenda => _session.CurrentUser?.Papel == PapelUsuario.Admin;
```

4. Update `Refresh()` to re-raise it (needed because this ViewModel is reused across a logout/login cycle — see this plan's Global Constraints):

```csharp
    public void Refresh()
    {
        CarregarProdutos();
        CarregarHistorico();
        OnPropertyChanged(nameof(PodeCancelarVenda));
    }
```

5. Update `FinalizarVenda` to pass the current user's name:

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
            var itens = Carrinho.Select(i => (i.ProductId, i.Quantidade));
            _salesService.RegisterSale(itens, FormaPagamentoSelecionada, _session.CurrentUser?.NomeUsuario);
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

6. Add `UsuarioNome` to the `VendaHistoricoItem` record and to `CarregarHistorico`'s mapping:

```csharp
public record VendaHistoricoItem(int SaleId, DateTime DataHora, decimal Total, FormaPagamento FormaPagamento, bool Cancelada, string? UsuarioNome)
{
    public string Status => Cancelada ? "Cancelada" : "Concluída";
    public bool PodeCancelar => !Cancelada;
}
```

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

- [ ] **Step 9: Update `VendaView.xaml`**

In `Lojinha.App/Views/VendaView.xaml`, add a new column to the histórico `DataGrid`, right after the `"Status"` column and before the action `DataGridTemplateColumn`:

```xml
                            <DataGridTextColumn Header="Vendedor" Binding="{Binding UsuarioNome}" Width="120" />
```

Then replace the "Cancelar" button's `Visibility` binding:

```xml
                                        <ui:Button Content="Cancelar" Appearance="Danger" Icon="{ui:SymbolIcon Symbol=Dismiss24}"
                                                   Visibility="{Binding PodeCancelar, Converter={StaticResource BooleanToVisibilityConverter}}"
                                                   Command="{Binding DataContext.CancelarVendaCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                                   CommandParameter="{Binding}" />
```

with:

```xml
                                        <ui:Button Content="Cancelar" Appearance="Danger" Icon="{ui:SymbolIcon Symbol=Dismiss24}"
                                                   Command="{Binding DataContext.CancelarVendaCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                                   CommandParameter="{Binding}">
                                            <ui:Button.Visibility>
                                                <MultiBinding Converter="{StaticResource BooleanAndToVisibilityConverter}">
                                                    <Binding Path="PodeCancelar" />
                                                    <Binding Path="DataContext.PodeCancelarVenda" RelativeSource="{RelativeSource AncestorType=DataGrid}" />
                                                </MultiBinding>
                                            </ui:Button.Visibility>
                                        </ui:Button>
```

- [ ] **Step 10: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 11: Manual smoke check**

Run: `dotnet run --project Lojinha.App`, log in as Admin, register a sale — confirm the histórico's "Vendedor" column shows the Admin's username and the "Cancelar" button is visible (and still respects the already-cancelled-hides-the-button behavior from before). Log out, log in as a Vendedor, register a sale — confirm "Vendedor" shows that account's name, and confirm the "Cancelar" button does NOT appear on any row in the histórico (including the Vendedor's own new sale), since only Admin can cancel.

- [ ] **Step 12: Commit**

```bash
git add Lojinha.Services/SalesService.cs Lojinha.Services.Tests/SalesServiceTests.cs Lojinha.App/ViewModels/SalesViewModel.cs Lojinha.App/Views/VendaView.xaml Lojinha.App/Converters/BooleanAndToVisibilityConverter.cs Lojinha.App/App.xaml
git commit -m "feat: track which user registered each sale, restrict cancel to Admin"
```

---

