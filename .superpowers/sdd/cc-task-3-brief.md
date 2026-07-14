### Task 3: `TurnoViewModel`

**Files:**
- Create: `Lojinha.App/ViewModels/TurnoViewModel.cs`
- Modify: `Lojinha.App/App.xaml.cs`

**Interfaces:**
- Consumes: `CaixaService` (Task 2); `SessionService`, `IAuthorizationService`, `ISnackbarService`, `PapelUsuario` (existing).
- Produces: `TurnoViewModel.SessaoAberta` (`bool`), `SessaoAtual` (`CaixaSessao?`), `ValorAberturaEntrada`/`ValorContadoEntrada`/`ValorMovimentoEntrada` (`decimal`), `TipoMovimentoSelecionado` (`TipoMovimentoCaixa`), `TiposMovimento` (`TipoMovimentoCaixa[]`), `Movimentos` (`ObservableCollection<MovimentoCaixa>`), `AbrirCaixaCommand`/`RegistrarMovimentoCommand`/`FecharCaixaCommand`, `Refresh()` — consumed by Task 4 (`SalesViewModel.Turno`) and Task 6 (`VendaView.xaml`).

- [ ] **Step 1: Create `TurnoViewModel`**

Create `Lojinha.App/ViewModels/TurnoViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lojinha.App.Services;
using Lojinha.Data.Models;
using Lojinha.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace Lojinha.App.ViewModels;

public partial class TurnoViewModel : ObservableObject
{
    private readonly CaixaService _caixaService;
    private readonly SessionService _session;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISnackbarService _snackbar;

    public ObservableCollection<MovimentoCaixa> Movimentos { get; } = new();
    public TipoMovimentoCaixa[] TiposMovimento { get; } = Enum.GetValues<TipoMovimentoCaixa>();

    [ObservableProperty]
    private CaixaSessao? sessaoAtual;

    [ObservableProperty]
    private decimal valorAberturaEntrada;

    [ObservableProperty]
    private decimal valorContadoEntrada;

    [ObservableProperty]
    private decimal valorMovimentoEntrada;

    [ObservableProperty]
    private TipoMovimentoCaixa tipoMovimentoSelecionado;

    public bool SessaoAberta => SessaoAtual is not null;

    public TurnoViewModel(CaixaService caixaService, SessionService session, IAuthorizationService authorizationService, ISnackbarService snackbar)
    {
        _caixaService = caixaService;
        _session = session;
        _authorizationService = authorizationService;
        _snackbar = snackbar;
        Carregar();
    }

    public void Refresh()
    {
        Carregar();
    }

    private void Carregar()
    {
        SessaoAtual = _caixaService.GetSessaoAberta();
        Movimentos.Clear();
        if (SessaoAtual is not null)
        {
            foreach (var movimento in _caixaService.GetMovimentos(SessaoAtual.Id))
            {
                Movimentos.Add(movimento);
            }
        }
    }

    partial void OnSessaoAtualChanged(CaixaSessao? value)
    {
        OnPropertyChanged(nameof(SessaoAberta));
    }

    [RelayCommand]
    private void AbrirCaixa()
    {
        try
        {
            _caixaService.AbrirCaixa(ValorAberturaEntrada, _session.CurrentUser?.NomeUsuario ?? string.Empty);
            ValorAberturaEntrada = 0;
            Carregar();
            _snackbar.Show("Sucesso", "Caixa aberto.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private void RegistrarMovimento()
    {
        string? autorizadoPor;

        if (_session.CurrentUser?.Papel == PapelUsuario.Admin)
        {
            autorizadoPor = _session.CurrentUser.NomeUsuario;
        }
        else
        {
            autorizadoPor = _authorizationService.AutorizarDesconto();
            if (autorizadoPor is null)
            {
                _snackbar.Show("Erro", "Movimento não autorizado.", ControlAppearance.Danger);
                return;
            }
        }

        try
        {
            _caixaService.RegistrarMovimento(TipoMovimentoSelecionado, ValorMovimentoEntrada, autorizadoPor, null);
            ValorMovimentoEntrada = 0;
            Carregar();
            _snackbar.Show("Sucesso", "Movimento registrado.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private void FecharCaixa()
    {
        try
        {
            var sessao = _caixaService.FecharCaixa(ValorContadoEntrada, _session.CurrentUser?.NomeUsuario ?? string.Empty);
            ValorContadoEntrada = 0;
            Carregar();
            _snackbar.Show("Sucesso", $"Caixa fechado. Diferença: {sessao.Diferenca:C}", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }
}
```

- [ ] **Step 2: Register `CaixaService` and `TurnoViewModel` in `App.xaml.cs`**

In `Lojinha.App/App.xaml.cs`, replace:

```csharp
        services.AddScoped<SalesService>();
        services.AddScoped<UserService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();

        services.AddScoped<CategoryViewModel>();
        services.AddScoped<SupplierViewModel>();
        services.AddScoped<ProductViewModel>();
        services.AddScoped<StockViewModel>();
        services.AddScoped<SalesViewModel>();
        services.AddScoped<UserViewModel>();
        services.AddScoped<MainViewModel>();
```

with:

```csharp
        services.AddScoped<SalesService>();
        services.AddScoped<UserService>();
        services.AddScoped<CaixaService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();

        services.AddScoped<CategoryViewModel>();
        services.AddScoped<SupplierViewModel>();
        services.AddScoped<ProductViewModel>();
        services.AddScoped<StockViewModel>();
        services.AddScoped<TurnoViewModel>();
        services.AddScoped<SalesViewModel>();
        services.AddScoped<UserViewModel>();
        services.AddScoped<MainViewModel>();
```

(`TurnoViewModel` is registered before `SalesViewModel` because Task 4 makes `SalesViewModel`'s constructor depend on it — order in this list is cosmetic for DI resolution, but matches the dependency direction for readability.)

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 81 tests total (unchanged — no automated tests for this UI-only task, matching this project's convention).

- [ ] **Step 5: Manual smoke check**

Run: `dotnet run --project Lojinha.App` in the background, confirm the process starts and stays alive (`tasklist //FI "IMAGENAME eq Lojinha.App.exe"`), then terminate it (`taskkill //F //IM Lojinha.App.exe`). `TurnoViewModel` isn't referenced by `SalesViewModel` or the UI yet (Task 4/6 wire it in) — this step only confirms the new DI registrations don't break app startup.

- [ ] **Step 6: Commit**

```bash
git add Lojinha.App/ViewModels/TurnoViewModel.cs Lojinha.App/App.xaml.cs
git commit -m "feat: add TurnoViewModel for cash-session management"
```

---

