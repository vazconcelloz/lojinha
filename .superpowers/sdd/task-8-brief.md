### Task 8: Estoque — restrict lot entry/delete to Admin

**Files:**
- Modify: `Lojinha.App/ViewModels/StockViewModel.cs`
- Modify: `Lojinha.App/Views/EstoqueView.xaml`

**Interfaces:**
- Consumes: `SessionService.CurrentUser` (Task 3).
- Produces: `StockViewModel.PodeGerenciarEstoque` (`bool`, computed).

- [ ] **Step 1: Update `StockViewModel`**

In `Lojinha.App/ViewModels/StockViewModel.cs`:

1. Add `using Lojinha.App.Services;` to the usings.
2. Add a `SessionService _session` field, injected via the constructor:

```csharp
    private readonly StockService _stockService;
    private readonly ProductService _productService;
    private readonly SupplierService _supplierService;
    private readonly ISnackbarService _snackbar;
    private readonly IContentDialogService _dialogService;
    private readonly SessionService _session;
```

```csharp
    public StockViewModel(StockService stockService, ProductService productService, SupplierService supplierService, ISnackbarService snackbar, IContentDialogService dialogService, SessionService session)
    {
        _stockService = stockService;
        _productService = productService;
        _supplierService = supplierService;
        _snackbar = snackbar;
        _dialogService = dialogService;
        _session = session;
        CarregarCombos();
        AtualizarPaineis();
    }
```

3. Add the computed property, right after the `[ObservableProperty] private DateTime? dataValidade;` block:

```csharp
    public bool PodeGerenciarEstoque => _session.CurrentUser?.Papel == PapelUsuario.Admin;
```

(This requires `using Lojinha.Data.Models;`, already present in this file for `Product`/`Supplier`.)

4. Update `Refresh()` to re-raise it:

```csharp
    public void Refresh()
    {
        CarregarCombos();
        AtualizarPaineis();
        OnPropertyChanged(nameof(PodeGerenciarEstoque));
    }
```

- [ ] **Step 2: Update `EstoqueView.xaml`**

In `Lojinha.App/Views/EstoqueView.xaml`, wrap the "Entrada de lote" `ui:Card` (the first one, containing the lot-entry form) with a visibility binding — replace:

```xml
            <ui:Card Margin="0,0,0,16">
                <StackPanel>
                    <TextBlock Text="Entrada de lote" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />
```

with:

```xml
            <ui:Card Margin="0,0,0,16" Visibility="{Binding PodeGerenciarEstoque, Converter={StaticResource BooleanToVisibilityConverter}}">
                <StackPanel>
                    <TextBlock Text="Entrada de lote" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />
```

Then, on the "Vencimentos" card's delete button (the last `ui:Button` in the file, inside the `Vencimentos` `DataGrid`'s template column), add the same visibility binding — replace:

```xml
                                        <ui:Button Appearance="Danger" Icon="{ui:SymbolIcon Symbol=Delete24}"
                                                   Command="{Binding DataContext.ExcluirLoteCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                                   CommandParameter="{Binding}" />
```

with:

```xml
                                        <ui:Button Appearance="Danger" Icon="{ui:SymbolIcon Symbol=Delete24}"
                                                   Visibility="{Binding DataContext.PodeGerenciarEstoque, RelativeSource={RelativeSource AncestorType=DataGrid}, Converter={StaticResource BooleanToVisibilityConverter}}"
                                                   Command="{Binding DataContext.ExcluirLoteCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                                   CommandParameter="{Binding}" />
```

(Unlike Vendas' "Cancelar" button, this delete button only has one condition to check — no `MultiBinding` needed here.)

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 54 tests total.

- [ ] **Step 5: Manual smoke check**

Run: `dotnet run --project Lojinha.App`, log in as Admin, go to Estoque — confirm "Entrada de lote" card and the Vencimentos delete buttons are visible; add a lot to confirm the form still works. Log out, log in as a Vendedor, go to Estoque — confirm "Entrada de lote" is gone and the delete buttons on Vencimentos rows (if any lots are near/past expiry) don't appear, while "Estoque atual"/"Estoque baixo"/"Vencimentos" tables themselves remain visible (view-only).

- [ ] **Step 6: Commit**

```bash
git add Lojinha.App/ViewModels/StockViewModel.cs Lojinha.App/Views/EstoqueView.xaml
git commit -m "feat: restrict Estoque lot entry/delete to Admin"
```

---

