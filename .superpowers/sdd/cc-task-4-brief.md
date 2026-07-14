### Task 4: `SalesViewModel` — `AbaCaixa` tab state, `Turno` composition, `FinalizarVenda` gate

**Files:**
- Create: `Lojinha.App/ViewModels/AbaCaixa.cs`
- Modify: `Lojinha.App/ViewModels/SalesViewModel.cs`

**Interfaces:**
- Consumes: `TurnoViewModel` (Task 3).
- Produces: `AbaCaixa` enum (`Caixa`, `Historico`, `Turno`); `SalesViewModel.AbaAtiva` (`AbaCaixa`), `Turno` (`TurnoViewModel`), `MostrarTurnoCommand` — consumed by Task 6 (`VendaView.xaml`).

- [ ] **Step 1: Create the `AbaCaixa` enum**

Create `Lojinha.App/ViewModels/AbaCaixa.cs`:

```csharp
namespace Lojinha.App.ViewModels;

public enum AbaCaixa
{
    Caixa,
    Historico,
    Turno
}
```

- [ ] **Step 2: Add the `Turno` property**

In `Lojinha.App/ViewModels/SalesViewModel.cs`, replace:

```csharp
    private readonly SalesService _salesService;
    private readonly ProductService _productService;
    private readonly ISnackbarService _snackbar;
    private readonly IContentDialogService _dialogService;
    private readonly SessionService _session;
    private readonly IAuthorizationService _authorizationService;
```

with:

```csharp
    private readonly SalesService _salesService;
    private readonly ProductService _productService;
    private readonly ISnackbarService _snackbar;
    private readonly IContentDialogService _dialogService;
    private readonly SessionService _session;
    private readonly IAuthorizationService _authorizationService;

    public TurnoViewModel Turno { get; }
```

- [ ] **Step 3: Replace `MostrandoHistorico` with `AbaAtiva`**

Replace:

```csharp
    [ObservableProperty]
    private bool mostrandoHistorico;
```

with:

```csharp
    [ObservableProperty]
    private AbaCaixa abaAtiva = AbaCaixa.Caixa;
```

- [ ] **Step 4: Wire `TurnoViewModel` into the constructor**

Replace:

```csharp
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
```

with:

```csharp
    public SalesViewModel(SalesService salesService, ProductService productService, ISnackbarService snackbar, IContentDialogService dialogService, SessionService session, IAuthorizationService authorizationService, TurnoViewModel turno)
    {
        _salesService = salesService;
        _productService = productService;
        _snackbar = snackbar;
        _dialogService = dialogService;
        _session = session;
        _authorizationService = authorizationService;
        Turno = turno;
        Carrinho.CollectionChanged += OnCarrinhoChanged;
        CarregarProdutos();
        CarregarHistorico();
    }
```

- [ ] **Step 5: Update the tab commands and add `MostrarTurno`**

Replace:

```csharp
    [RelayCommand]
    private void MostrarCaixa()
    {
        MostrandoHistorico = false;
    }

    [RelayCommand]
    private void MostrarHistorico()
    {
        MostrandoHistorico = true;
    }
```

with:

```csharp
    [RelayCommand]
    private void MostrarCaixa()
    {
        AbaAtiva = AbaCaixa.Caixa;
    }

    [RelayCommand]
    private void MostrarHistorico()
    {
        AbaAtiva = AbaCaixa.Historico;
    }

    [RelayCommand]
    private void MostrarTurno()
    {
        AbaAtiva = AbaCaixa.Turno;
        Turno.Refresh();
    }
```

- [ ] **Step 6: Gate `FinalizarVenda` on an open session**

Replace:

```csharp
    [RelayCommand]
    private void FinalizarVenda()
    {
        if (Carrinho.Count == 0)
        {
            _snackbar.Show("Erro", "Adicione ao menos um item à venda.", ControlAppearance.Danger);
            return;
        }
```

with:

```csharp
    [RelayCommand]
    private void FinalizarVenda()
    {
        if (!Turno.SessaoAberta)
        {
            _snackbar.Show("Erro", "Abra o caixa antes de registrar uma venda.", ControlAppearance.Danger);
            return;
        }

        if (Carrinho.Count == 0)
        {
            _snackbar.Show("Erro", "Adicione ao menos um item à venda.", ControlAppearance.Danger);
            return;
        }
```

- [ ] **Step 7: Refresh `Turno` on screen navigation-in**

Replace:

```csharp
    public void Refresh()
    {
        CarregarProdutos();
        CarregarHistorico();
        OnPropertyChanged(nameof(PodeCancelarVenda));
    }
```

with:

```csharp
    public void Refresh()
    {
        CarregarProdutos();
        CarregarHistorico();
        Turno.Refresh();
        OnPropertyChanged(nameof(PodeCancelarVenda));
    }
```

- [ ] **Step 8: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 9: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 81 tests total (unchanged — `SalesViewModel` has no automated test coverage in this project, matching established convention).

- [ ] **Step 10: Manual smoke check**

Run: `dotnet run --project Lojinha.App` in the background, confirm the process starts and stays alive, then terminate it. The Turno tab isn't in the XAML yet (Task 6), so this only confirms `SalesViewModel`'s constructor/property wiring doesn't throw at startup.

- [ ] **Step 11: Commit**

```bash
git add Lojinha.App/ViewModels/AbaCaixa.cs Lojinha.App/ViewModels/SalesViewModel.cs
git commit -m "feat: gate FinalizarVenda on open caixa session, add AbaCaixa tab state"
```

---

