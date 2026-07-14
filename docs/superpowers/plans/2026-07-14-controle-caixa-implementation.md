# Controle de Caixa Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add cash-register session control (abertura/fechamento de turno, sangria, suprimento, conferência) to the Caixa screen, and gate `FinalizarVenda` on having an open session.

**Architecture:** A new `CaixaSessao`/`MovimentoCaixa` data model (joined to `Sale` by time range, no FK on `Sale`), a `CaixaService` mirroring the existing service conventions, and a dedicated `TurnoViewModel` exposed as `SalesViewModel.Turno`. The existing `Caixa`/`Histórico` tab toggle (currently a `bool`) becomes a 3-way `AbaCaixa` enum with a new `EnumToVisibilityConverter`, adding a third "Turno" tab to `VendaView.xaml`. Sangria/suprimento reuse the existing `AutorizacaoWindow`/`IAuthorizationService` supervisor-override flow.

**Tech Stack:** .NET 8, WPF, WPF-UI 4.3.0, CommunityToolkit.Mvvm, EF Core 8 + SQLite, xUnit.

## Global Constraints

- Every service method that fails throws `ArgumentException`/`InvalidOperationException` with a Portuguese, user-facing message, consistent with existing services.
- `CaixaService.RegistrarMovimento` does not re-validate that `autorizadoPor` names an actual Admin — role gating is a UI-layer concern in this codebase (same trust boundary as `SalesService.RegisterSale`'s `autorizadoPor`).
- `Sale`/`SaleItem` are not modified. A session's sales are found by `Sale.DataHora` falling between `CaixaSessao.DataAbertura` and the closing timestamp — there is at most one open session at a time, so this is unambiguous.
- Cancelled sales are excluded from the expected-cash calculation (assumed refunded). Only `FormaPagamento.Dinheiro` sales count toward it.
- No automated UI tests in this plan (per established project convention) — frontend tasks are verified by `dotnet build` + a manual smoke run.
- All new/changed UI copy is in Portuguese.

---

### Task 1: `CaixaSessao`/`MovimentoCaixa` models, `DbContext` wiring, migration

**Files:**
- Create: `Lojinha.Data/Models/TipoMovimentoCaixa.cs`
- Create: `Lojinha.Data/Models/CaixaSessao.cs`
- Create: `Lojinha.Data/Models/MovimentoCaixa.cs`
- Modify: `Lojinha.Data/LojinhaDbContext.cs`
- Migration: generated under `Lojinha.Data/Migrations/`

**Interfaces:**
- Produces: `TipoMovimentoCaixa` enum (`Sangria`, `Suprimento`); `CaixaSessao` (`Id`, `DataAbertura`, `ValorAbertura`, `UsuarioAbertura`, `DataFechamento`, `ValorContado`, `ValorEsperado`, `Diferenca`, `UsuarioFechamento`, `Movimentos`); `MovimentoCaixa` (`Id`, `CaixaSessaoId`, `CaixaSessao`, `Tipo`, `Valor`, `DataHora`, `AutorizadoPor`, `Observacao`); `LojinhaDbContext.CaixaSessoes`/`MovimentosCaixa` `DbSet`s — consumed by Task 2 (`CaixaService`).

- [ ] **Step 1: Create the `TipoMovimentoCaixa` enum**

Create `Lojinha.Data/Models/TipoMovimentoCaixa.cs`:

```csharp
namespace Lojinha.Data.Models;

public enum TipoMovimentoCaixa
{
    Sangria,
    Suprimento
}
```

- [ ] **Step 2: Create the `CaixaSessao` model**

Create `Lojinha.Data/Models/CaixaSessao.cs`:

```csharp
namespace Lojinha.Data.Models;

public class CaixaSessao
{
    public int Id { get; set; }
    public DateTime DataAbertura { get; set; }
    public decimal ValorAbertura { get; set; }
    public required string UsuarioAbertura { get; set; }
    public DateTime? DataFechamento { get; set; }
    public decimal? ValorContado { get; set; }
    public decimal? ValorEsperado { get; set; }
    public decimal? Diferenca { get; set; }
    public string? UsuarioFechamento { get; set; }

    public ICollection<MovimentoCaixa> Movimentos { get; set; } = new List<MovimentoCaixa>();
}
```

- [ ] **Step 3: Create the `MovimentoCaixa` model**

Create `Lojinha.Data/Models/MovimentoCaixa.cs`:

```csharp
namespace Lojinha.Data.Models;

public class MovimentoCaixa
{
    public int Id { get; set; }
    public int CaixaSessaoId { get; set; }
    public CaixaSessao? CaixaSessao { get; set; }
    public TipoMovimentoCaixa Tipo { get; set; }
    public decimal Valor { get; set; }
    public DateTime DataHora { get; set; }
    public required string AutorizadoPor { get; set; }
    public string? Observacao { get; set; }
}
```

- [ ] **Step 4: Wire the new models into `LojinhaDbContext`**

In `Lojinha.Data/LojinhaDbContext.cs`, replace:

```csharp
    public DbSet<User> Users => Set<User>();
```

with:

```csharp
    public DbSet<User> Users => Set<User>();
    public DbSet<CaixaSessao> CaixaSessoes => Set<CaixaSessao>();
    public DbSet<MovimentoCaixa> MovimentosCaixa => Set<MovimentoCaixa>();
```

Then, inside `OnModelCreating`, replace:

```csharp
        modelBuilder.Entity<User>()
            .HasIndex(u => u.NomeUsuario)
            .IsUnique();
    }
```

with:

```csharp
        modelBuilder.Entity<User>()
            .HasIndex(u => u.NomeUsuario)
            .IsUnique();

        modelBuilder.Entity<CaixaSessao>()
            .Property(c => c.ValorAbertura)
            .HasPrecision(10, 2);

        modelBuilder.Entity<CaixaSessao>()
            .Property(c => c.ValorContado)
            .HasPrecision(10, 2);

        modelBuilder.Entity<CaixaSessao>()
            .Property(c => c.ValorEsperado)
            .HasPrecision(10, 2);

        modelBuilder.Entity<CaixaSessao>()
            .Property(c => c.Diferenca)
            .HasPrecision(10, 2);

        modelBuilder.Entity<MovimentoCaixa>()
            .Property(m => m.Valor)
            .HasPrecision(10, 2);

        modelBuilder.Entity<MovimentoCaixa>()
            .HasOne(m => m.CaixaSessao)
            .WithMany(c => c.Movimentos)
            .HasForeignKey(m => m.CaixaSessaoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
```

- [ ] **Step 5: Generate the EF Core migration**

Run: `dotnet ef migrations add AddCaixaSessao --project Lojinha.Data`
Expected: no errors; two new files appear under `Lojinha.Data/Migrations/` (a new `..._AddCaixaSessao.cs` migration + matching `.Designer.cs`), and `Lojinha.Data/Migrations/LojinhaDbContextModelSnapshot.cs` is updated with the new tables.

- [ ] **Step 6: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 7: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 62 tests total (unchanged — this task adds no tests, just schema).

- [ ] **Step 8: Commit**

```bash
git add Lojinha.Data/Models/TipoMovimentoCaixa.cs Lojinha.Data/Models/CaixaSessao.cs Lojinha.Data/Models/MovimentoCaixa.cs Lojinha.Data/LojinhaDbContext.cs Lojinha.Data/Migrations
git commit -m "feat: add CaixaSessao/MovimentoCaixa models and migration"
```

---

### Task 2: `CaixaService`

**Files:**
- Create: `Lojinha.Services/CaixaService.cs`
- Test: `Lojinha.Services.Tests/CaixaServiceTests.cs`

**Interfaces:**
- Consumes: `CaixaSessao`/`MovimentoCaixa`/`TipoMovimentoCaixa` (Task 1); `Sale`/`FormaPagamento` (existing).
- Produces: `CaixaService.AbrirCaixa(decimal, string) : CaixaSessao`, `RegistrarMovimento(TipoMovimentoCaixa, decimal, string, string?) : MovimentoCaixa`, `FecharCaixa(decimal, string) : CaixaSessao`, `GetSessaoAberta() : CaixaSessao?`, `GetMovimentos(int) : IEnumerable<MovimentoCaixa>` — consumed by Task 3 (`TurnoViewModel`).

- [ ] **Step 1: Write the failing tests**

Create `Lojinha.Services.Tests/CaixaServiceTests.cs`:

```csharp
using Lojinha.Data;
using Lojinha.Data.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Lojinha.Services.Tests;

public class CaixaServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LojinhaDbContext _context;
    private readonly CaixaService _service;
    private readonly StockService _stockService;
    private readonly SalesService _salesService;
    private readonly Category _category;
    private readonly Product _product;

    public CaixaServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LojinhaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LojinhaDbContext(options);
        _context.Database.EnsureCreated();

        _service = new CaixaService(_context);
        _stockService = new StockService(_context);
        _salesService = new SalesService(_context, _stockService);

        _category = new Category { Nome = "Bebidas" };
        _context.Categories.Add(_category);
        _context.SaveChanges();

        _product = new Product
        {
            Nome = "Coca-Cola 2L",
            CodigoBarras = "789000000001",
            CategoryId = _category.Id,
            TipoVenda = TipoVenda.Unidade,
            PrecoCusto = 5m,
            PrecoVenda = 10m,
            EstoqueMinimo = 0
        };
        _context.Products.Add(_product);
        _context.SaveChanges();
        _stockService.AddLot(_product.Id, quantidade: 100, dataValidade: null, supplierId: null);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void AbrirCaixa_Succeeds_WhenNoneOpen()
    {
        var sessao = _service.AbrirCaixa(100m, "admin1");

        Assert.Equal(100m, sessao.ValorAbertura);
        Assert.Equal("admin1", sessao.UsuarioAbertura);
        Assert.Null(sessao.DataFechamento);
    }

    [Fact]
    public void AbrirCaixa_ThrowsWhenAlreadyOpen()
    {
        _service.AbrirCaixa(100m, "admin1");

        Assert.Throws<InvalidOperationException>(() => _service.AbrirCaixa(50m, "admin1"));
    }

    [Fact]
    public void AbrirCaixa_ThrowsWhenValorNegativo()
    {
        Assert.Throws<ArgumentException>(() => _service.AbrirCaixa(-10m, "admin1"));
    }

    [Fact]
    public void GetSessaoAberta_ReturnsNullWhenNoneOpen()
    {
        Assert.Null(_service.GetSessaoAberta());
    }

    [Fact]
    public void RegistrarMovimento_ThrowsWhenNoneOpen()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _service.RegistrarMovimento(TipoMovimentoCaixa.Sangria, 20m, "admin1", null));
    }

    [Fact]
    public void RegistrarMovimento_ThrowsWhenValorNaoPositivo()
    {
        _service.AbrirCaixa(100m, "admin1");

        Assert.Throws<ArgumentException>(() =>
            _service.RegistrarMovimento(TipoMovimentoCaixa.Sangria, 0m, "admin1", null));
    }

    [Fact]
    public void RegistrarMovimento_Sangria_Succeeds()
    {
        var sessao = _service.AbrirCaixa(100m, "admin1");

        var movimento = _service.RegistrarMovimento(TipoMovimentoCaixa.Sangria, 30m, "admin1", "Depósito banco");

        Assert.Equal(sessao.Id, movimento.CaixaSessaoId);
        Assert.Equal(TipoMovimentoCaixa.Sangria, movimento.Tipo);
        Assert.Equal(30m, movimento.Valor);
        Assert.Equal("admin1", movimento.AutorizadoPor);
        Assert.Equal("Depósito banco", movimento.Observacao);
    }

    [Fact]
    public void RegistrarMovimento_Suprimento_Succeeds()
    {
        _service.AbrirCaixa(100m, "admin1");

        var movimento = _service.RegistrarMovimento(TipoMovimentoCaixa.Suprimento, 50m, "admin1", null);

        Assert.Equal(TipoMovimentoCaixa.Suprimento, movimento.Tipo);
        Assert.Equal(50m, movimento.Valor);
    }

    [Fact]
    public void RegistrarMovimento_DoesNotRevalidateAutorizadoPorRole()
    {
        _service.AbrirCaixa(100m, "admin1");

        var movimento = _service.RegistrarMovimento(TipoMovimentoCaixa.Sangria, 10m, "qualquer-nome", null);

        Assert.Equal("qualquer-nome", movimento.AutorizadoPor);
    }

    [Fact]
    public void FecharCaixa_ThrowsWhenNoneOpen()
    {
        Assert.Throws<InvalidOperationException>(() => _service.FecharCaixa(100m, "admin1"));
    }

    [Fact]
    public void FecharCaixa_ThrowsWhenValorContadoNegativo()
    {
        _service.AbrirCaixa(100m, "admin1");

        Assert.Throws<ArgumentException>(() => _service.FecharCaixa(-1m, "admin1"));
    }

    [Fact]
    public void FecharCaixa_ComputesValorEsperadoAndDiferenca_NoMovimentosOrVendas()
    {
        _service.AbrirCaixa(100m, "admin1");

        var sessao = _service.FecharCaixa(100m, "admin1");

        Assert.Equal(100m, sessao.ValorEsperado);
        Assert.Equal(0m, sessao.Diferenca);
        Assert.NotNull(sessao.DataFechamento);
        Assert.Equal("admin1", sessao.UsuarioFechamento);
    }

    [Fact]
    public void FecharCaixa_IncludesDinheiroSalesAndExcludesOtherPayments()
    {
        _service.AbrirCaixa(100m, "admin1");
        _salesService.RegisterSale(new[] { (_product.Id, 2m, 0m) }, FormaPagamento.Dinheiro, valorRecebido: 20m);
        _salesService.RegisterSale(new[] { (_product.Id, 1m, 0m) }, FormaPagamento.Cartao);

        var sessao = _service.FecharCaixa(120m, "admin1");

        Assert.Equal(120m, sessao.ValorEsperado);
        Assert.Equal(0m, sessao.Diferenca);
    }

    [Fact]
    public void FecharCaixa_ExcludesCancelledSales()
    {
        _service.AbrirCaixa(100m, "admin1");
        var venda = _salesService.RegisterSale(new[] { (_product.Id, 2m, 0m) }, FormaPagamento.Dinheiro, valorRecebido: 20m);
        _salesService.CancelSale(venda.Id);

        var sessao = _service.FecharCaixa(100m, "admin1");

        Assert.Equal(100m, sessao.ValorEsperado);
    }

    [Fact]
    public void FecharCaixa_AppliesSuprimentoAndSangria()
    {
        _service.AbrirCaixa(100m, "admin1");
        _service.RegistrarMovimento(TipoMovimentoCaixa.Suprimento, 50m, "admin1", null);
        _service.RegistrarMovimento(TipoMovimentoCaixa.Sangria, 30m, "admin1", null);

        var sessao = _service.FecharCaixa(120m, "admin1");

        Assert.Equal(120m, sessao.ValorEsperado);
        Assert.Equal(0m, sessao.Diferenca);
    }

    [Fact]
    public void FecharCaixa_RecordsPositiveDiferenca_WhenContadoExceedsEsperado()
    {
        _service.AbrirCaixa(100m, "admin1");

        var sessao = _service.FecharCaixa(110m, "admin1");

        Assert.Equal(10m, sessao.Diferenca);
    }

    [Fact]
    public void GetSessaoAberta_ReturnsNullAfterClosing()
    {
        _service.AbrirCaixa(100m, "admin1");
        _service.FecharCaixa(100m, "admin1");

        Assert.Null(_service.GetSessaoAberta());
    }

    [Fact]
    public void AbrirCaixa_SucceedsAgainAfterPreviousClosed()
    {
        _service.AbrirCaixa(100m, "admin1");
        _service.FecharCaixa(100m, "admin1");

        var novaSessao = _service.AbrirCaixa(50m, "admin2");

        Assert.Equal(50m, novaSessao.ValorAbertura);
    }

    [Fact]
    public void GetMovimentos_OrdersByDataHoraDescending()
    {
        _service.AbrirCaixa(100m, "admin1");
        _service.RegistrarMovimento(TipoMovimentoCaixa.Suprimento, 10m, "admin1", null);
        _service.RegistrarMovimento(TipoMovimentoCaixa.Sangria, 5m, "admin1", null);

        var movimentos = _service.GetMovimentos(_service.GetSessaoAberta()!.Id).ToList();

        Assert.Equal(2, movimentos.Count);
        Assert.Equal(TipoMovimentoCaixa.Sangria, movimentos.First().Tipo);
    }
}
```

- [ ] **Step 2: Run tests to verify the expected build failure**

Run: `dotnet test --filter "FullyQualifiedName~CaixaServiceTests"`
Expected: build FAILS — `CaixaServiceTests.cs` references `CaixaService`, which doesn't exist yet. This compile error is the expected RED state.

- [ ] **Step 3: Implement `CaixaService`**

Create `Lojinha.Services/CaixaService.cs`:

```csharp
using Lojinha.Data;
using Lojinha.Data.Models;

namespace Lojinha.Services;

public class CaixaService
{
    private readonly LojinhaDbContext _context;

    public CaixaService(LojinhaDbContext context)
    {
        _context = context;
    }

    public CaixaSessao? GetSessaoAberta()
    {
        return _context.CaixaSessoes.FirstOrDefault(c => c.DataFechamento == null);
    }

    public CaixaSessao AbrirCaixa(decimal valorAbertura, string usuarioNome)
    {
        if (GetSessaoAberta() is not null)
        {
            throw new InvalidOperationException("Já existe um caixa aberto.");
        }

        if (valorAbertura < 0)
        {
            throw new ArgumentException("Valor de abertura não pode ser negativo.", nameof(valorAbertura));
        }

        var sessao = new CaixaSessao
        {
            DataAbertura = DateTime.Now,
            ValorAbertura = valorAbertura,
            UsuarioAbertura = usuarioNome
        };

        _context.CaixaSessoes.Add(sessao);
        _context.SaveChanges();

        return sessao;
    }

    public MovimentoCaixa RegistrarMovimento(TipoMovimentoCaixa tipo, decimal valor, string autorizadoPor, string? observacao)
    {
        var sessao = GetSessaoAberta()
            ?? throw new InvalidOperationException("Nenhum caixa aberto.");

        if (valor <= 0)
        {
            throw new ArgumentException("Valor do movimento deve ser maior que zero.", nameof(valor));
        }

        var movimento = new MovimentoCaixa
        {
            CaixaSessaoId = sessao.Id,
            Tipo = tipo,
            Valor = valor,
            DataHora = DateTime.Now,
            AutorizadoPor = autorizadoPor,
            Observacao = observacao
        };

        _context.MovimentosCaixa.Add(movimento);
        _context.SaveChanges();

        return movimento;
    }

    public CaixaSessao FecharCaixa(decimal valorContado, string usuarioNome)
    {
        var sessao = GetSessaoAberta()
            ?? throw new InvalidOperationException("Nenhum caixa aberto para fechar.");

        if (valorContado < 0)
        {
            throw new ArgumentException("Valor contado não pode ser negativo.", nameof(valorContado));
        }

        var dataFechamento = DateTime.Now;

        var vendasDinheiro = _context.Sales
            .Where(s => s.FormaPagamento == FormaPagamento.Dinheiro
                && !s.Cancelada
                && s.DataHora >= sessao.DataAbertura
                && s.DataHora <= dataFechamento)
            .Sum(s => (decimal?)s.Total) ?? 0;

        var movimentos = _context.MovimentosCaixa
            .Where(m => m.CaixaSessaoId == sessao.Id)
            .ToList();
        var suprimentos = movimentos.Where(m => m.Tipo == TipoMovimentoCaixa.Suprimento).Sum(m => m.Valor);
        var sangrias = movimentos.Where(m => m.Tipo == TipoMovimentoCaixa.Sangria).Sum(m => m.Valor);

        var valorEsperado = sessao.ValorAbertura + vendasDinheiro + suprimentos - sangrias;

        sessao.DataFechamento = dataFechamento;
        sessao.ValorContado = valorContado;
        sessao.ValorEsperado = valorEsperado;
        sessao.Diferenca = valorContado - valorEsperado;
        sessao.UsuarioFechamento = usuarioNome;

        _context.SaveChanges();

        return sessao;
    }

    public IEnumerable<MovimentoCaixa> GetMovimentos(int sessaoId)
    {
        return _context.MovimentosCaixa
            .Where(m => m.CaixaSessaoId == sessaoId)
            .OrderByDescending(m => m.DataHora)
            .ToList();
    }
}
```

- [ ] **Step 4: Run tests to verify GREEN**

Run: `dotnet test --filter "FullyQualifiedName~CaixaServiceTests"`
Expected: PASS, 19/19 for the class.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 81 tests total (62 existing + 19 new).

- [ ] **Step 6: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 7: Commit**

```bash
git add Lojinha.Services/CaixaService.cs Lojinha.Services.Tests/CaixaServiceTests.cs
git commit -m "feat: add CaixaService for cash-session open/close and movements"
```

---

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

### Task 5: `EnumToVisibilityConverter`

**Files:**
- Create: `Lojinha.App/Converters/EnumToVisibilityConverter.cs`
- Modify: `Lojinha.App/App.xaml`

**Interfaces:**
- Produces: `EnumToVisibilityConverter` registered as the `EnumToVisibilityConverter` resource key — consumed by Task 6 (`VendaView.xaml`).

- [ ] **Step 1: Create the converter**

Create `Lojinha.App/Converters/EnumToVisibilityConverter.cs`:

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Lojinha.App.Converters;

public class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
        {
            return Visibility.Collapsed;
        }

        var visible = string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
```

- [ ] **Step 2: Register it as an `App.xaml` resource**

In `Lojinha.App/App.xaml`, replace:

```xml
            <converters:BooleanAndToVisibilityConverter x:Key="BooleanAndToVisibilityConverter" />
```

with:

```xml
            <converters:BooleanAndToVisibilityConverter x:Key="BooleanAndToVisibilityConverter" />
            <converters:EnumToVisibilityConverter x:Key="EnumToVisibilityConverter" />
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 81 tests total (unchanged).

- [ ] **Step 5: Commit**

```bash
git add Lojinha.App/Converters/EnumToVisibilityConverter.cs Lojinha.App/App.xaml
git commit -m "feat: add EnumToVisibilityConverter for the 3-way Caixa tab toggle"
```

---

### Task 6: `VendaView.xaml` — Turno tab and enum-based tab switching

**Files:**
- Modify: `Lojinha.App/Views/VendaView.xaml`

**Interfaces:**
- Consumes: `SalesViewModel.AbaAtiva`/`MostrarTurnoCommand`/`Turno` (Task 4); `EnumToVisibilityConverter` (Task 5); `TurnoViewModel`'s full member set (Task 3).

- [ ] **Step 1: Replace the entire file**

Replace the entire contents of `Lojinha.App/Views/VendaView.xaml` with:

```xml
<UserControl x:Class="Lojinha.App.Views.VendaView"
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

        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,12">
            <ui:Button Content="Caixa" Margin="0,0,8,0" Command="{Binding MostrarCaixaCommand}" />
            <ui:Button Content="Histórico" Margin="0,0,8,0" Command="{Binding MostrarHistoricoCommand}" />
            <ui:Button Content="Turno" Command="{Binding MostrarTurnoCommand}" />
        </StackPanel>

        <Grid Grid.Row="1" Visibility="{Binding AbaAtiva, Converter={StaticResource EnumToVisibilityConverter}, ConverterParameter=Caixa}">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="2*" />
                <ColumnDefinition Width="1*" />
            </Grid.ColumnDefinitions>

            <StackPanel Grid.Column="0" Margin="0,0,16,0">
                <ui:Card Margin="0,0,0,16">
                    <StackPanel>
                        <TextBlock Text="Nova venda" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />
                        <WrapPanel>
                            <ui:TextBox Width="220" Margin="0,0,8,8" PlaceholderText="Buscar produto (nome ou código)"
                                        Text="{Binding TermoBusca, UpdateSourceTrigger=PropertyChanged}">
                                <ui:TextBox.InputBindings>
                                    <KeyBinding Key="Return" Command="{Binding EscanearCommand}" />
                                </ui:TextBox.InputBindings>
                            </ui:TextBox>
                            <ComboBox Width="220" Margin="0,0,8,8" ItemsSource="{Binding Produtos}" DisplayMemberPath="Nome"
                                      SelectedItem="{Binding ProdutoSelecionado}" />
                            <ui:TextBox Width="120" Margin="0,0,8,8" PlaceholderText="Quantidade"
                                        Text="{Binding Quantidade, UpdateSourceTrigger=PropertyChanged}" />
                            <ui:Button Content="Adicionar ao carrinho" Appearance="Primary" Icon="{ui:SymbolIcon Symbol=Add24}"
                                       Command="{Binding AdicionarAoCarrinhoCommand}" />
                        </WrapPanel>
                    </StackPanel>
                </ui:Card>

                <ui:Card>
                    <StackPanel>
                        <TextBlock Text="Carrinho" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />
                        <StackPanel Visibility="{Binding Carrinho.Count, Converter={StaticResource CountToVisibilityConverter}}">
                            <TextBlock Text="Carrinho vazio." Opacity="0.7" />
                        </StackPanel>
                        <DataGrid ItemsSource="{Binding Carrinho}" AutoGenerateColumns="False" IsReadOnly="True" MaxHeight="320"
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
                    </StackPanel>
                </ui:Card>
            </StackPanel>

            <ui:Card Grid.Column="1" VerticalAlignment="Top">
                <StackPanel>
                    <TextBlock Text="RESUMO DA VENDA" FontWeight="Bold" FontSize="14" Margin="0,0,0,16" />

                    <TextBlock Text="Desconto da venda" Margin="0,0,0,4" Opacity="0.7" />
                    <WrapPanel Margin="0,0,0,12">
                        <ComboBox Width="90" Margin="0,0,8,0" ItemsSource="{Binding TiposDesconto}"
                                  SelectedItem="{Binding TipoDescontoVenda}" />
                        <ui:TextBox Width="90" Text="{Binding DescontoVendaEntrada, UpdateSourceTrigger=PropertyChanged}" />
                    </WrapPanel>

                    <TextBlock Text="Forma de pagamento" Margin="0,0,0,4" Opacity="0.7" />
                    <ComboBox Margin="0,0,0,12" ItemsSource="{Binding FormasPagamento}"
                              SelectedItem="{Binding FormaPagamentoSelecionada}" />

                    <StackPanel Visibility="{Binding EhDinheiro, Converter={StaticResource BooleanToVisibilityConverter}}">
                        <TextBlock Text="Valor recebido" Margin="0,0,0,4" Opacity="0.7" />
                        <ui:TextBox Margin="0,0,0,12" Text="{Binding ValorRecebido, UpdateSourceTrigger=PropertyChanged}" />
                    </StackPanel>

                    <Separator Margin="0,0,0,12" />

                    <TextBlock Text="{Binding Total, StringFormat='{}{0:C}'}" FontWeight="Bold" FontSize="30"
                               HorizontalAlignment="Center" Margin="0,0,0,8" />

                    <TextBlock Text="{Binding Troco, StringFormat='Troco: {0:C}'}" HorizontalAlignment="Center"
                               Opacity="0.7" Margin="0,0,0,16"
                               Visibility="{Binding EhDinheiro, Converter={StaticResource BooleanToVisibilityConverter}}" />

                    <ui:Button Content="Finalizar venda" Appearance="Primary" HorizontalAlignment="Stretch"
                               Icon="{ui:SymbolIcon Symbol=ReceiptMoney24}"
                               Command="{Binding FinalizarVendaCommand}" />
                </StackPanel>
            </ui:Card>
        </Grid>

        <ui:Card Grid.Row="1" Visibility="{Binding AbaAtiva, Converter={StaticResource EnumToVisibilityConverter}, ConverterParameter=Historico}">
            <StackPanel>
                <TextBlock Text="Histórico de vendas" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />
                <StackPanel Visibility="{Binding Historico.Count, Converter={StaticResource CountToVisibilityConverter}}">
                    <TextBlock Text="Nenhuma venda registrada ainda." Opacity="0.7" />
                </StackPanel>
                <DataGrid ItemsSource="{Binding Historico}" AutoGenerateColumns="False" IsReadOnly="True" MaxHeight="500"
                          Visibility="{Binding Historico.Count, Converter={StaticResource CountToVisibilityConverter}, ConverterParameter=Invert}">
                    <DataGrid.RowStyle>
                        <Style TargetType="DataGridRow">
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding Cancelada}" Value="True">
                                    <Setter Property="Foreground" Value="Gray" />
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </DataGrid.RowStyle>
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Data" Binding="{Binding DataHora, StringFormat='dd/MM/yyyy HH:mm'}" Width="140" />
                        <DataGridTextColumn Header="Total" Binding="{Binding Total, StringFormat=C}" Width="100" />
                        <DataGridTextColumn Header="Pagamento" Binding="{Binding FormaPagamento}" Width="100" />
                        <DataGridTextColumn Header="Status" Binding="{Binding Status}" Width="100" />
                        <DataGridTextColumn Header="Vendedor" Binding="{Binding UsuarioNome}" Width="120" />
                        <DataGridTextColumn Header="Desconto" Binding="{Binding DescontoValor, StringFormat=C}" Width="90" />
                        <DataGridTextColumn Header="Troco" Binding="{Binding Troco, StringFormat=C}" Width="90" />
                        <DataGridTemplateColumn Header="" Width="140">
                            <DataGridTemplateColumn.CellTemplate>
                                <DataTemplate>
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
                                </DataTemplate>
                            </DataGridTemplateColumn.CellTemplate>
                        </DataGridTemplateColumn>
                    </DataGrid.Columns>
                </DataGrid>
            </StackPanel>
        </ui:Card>

        <ui:Card Grid.Row="1" Visibility="{Binding AbaAtiva, Converter={StaticResource EnumToVisibilityConverter}, ConverterParameter=Turno}">
            <StackPanel>
                <TextBlock Text="Turno de caixa" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />

                <StackPanel Visibility="{Binding Turno.SessaoAberta, Converter={StaticResource BooleanToVisibilityConverter}, ConverterParameter=Invert}">
                    <TextBlock Text="Nenhum caixa aberto." Opacity="0.7" Margin="0,0,0,12" />
                    <WrapPanel>
                        <ui:TextBox Width="150" Margin="0,0,8,0" PlaceholderText="Valor de abertura"
                                    Text="{Binding Turno.ValorAberturaEntrada, UpdateSourceTrigger=PropertyChanged}" />
                        <ui:Button Content="Abrir caixa" Appearance="Primary" Icon="{ui:SymbolIcon Symbol=Wallet24}"
                                   Command="{Binding Turno.AbrirCaixaCommand}" />
                    </WrapPanel>
                </StackPanel>

                <StackPanel Visibility="{Binding Turno.SessaoAberta, Converter={StaticResource BooleanToVisibilityConverter}}">
                    <TextBlock Text="{Binding Turno.SessaoAtual.DataAbertura, StringFormat='Aberto desde: {0:dd/MM/yyyy HH:mm}'}" Margin="0,0,0,4" />
                    <TextBlock Text="{Binding Turno.SessaoAtual.ValorAbertura, StringFormat='Valor de abertura: {0:C}'}" Margin="0,0,0,12" Opacity="0.7" />

                    <TextBlock Text="Sangria / suprimento" FontWeight="Bold" Margin="0,0,0,8" />
                    <WrapPanel Margin="0,0,0,12">
                        <ComboBox Width="120" Margin="0,0,8,0" ItemsSource="{Binding Turno.TiposMovimento}"
                                  SelectedItem="{Binding Turno.TipoMovimentoSelecionado}" />
                        <ui:TextBox Width="120" Margin="0,0,8,0" PlaceholderText="Valor"
                                    Text="{Binding Turno.ValorMovimentoEntrada, UpdateSourceTrigger=PropertyChanged}" />
                        <ui:Button Content="Registrar" Icon="{ui:SymbolIcon Symbol=ArrowSwap24}"
                                   Command="{Binding Turno.RegistrarMovimentoCommand}" />
                    </WrapPanel>

                    <DataGrid ItemsSource="{Binding Turno.Movimentos}" AutoGenerateColumns="False" IsReadOnly="True" MaxHeight="200" Margin="0,0,0,16">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="Data" Binding="{Binding DataHora, StringFormat='dd/MM/yyyy HH:mm'}" Width="140" />
                            <DataGridTextColumn Header="Tipo" Binding="{Binding Tipo}" Width="100" />
                            <DataGridTextColumn Header="Valor" Binding="{Binding Valor, StringFormat=C}" Width="100" />
                            <DataGridTextColumn Header="Autorizado por" Binding="{Binding AutorizadoPor}" Width="140" />
                            <DataGridTextColumn Header="Observação" Binding="{Binding Observacao}" Width="*" />
                        </DataGrid.Columns>
                    </DataGrid>

                    <TextBlock Text="Fechar caixa" FontWeight="Bold" Margin="0,0,0,8" />
                    <WrapPanel>
                        <ui:TextBox Width="150" Margin="0,0,8,0" PlaceholderText="Valor contado"
                                    Text="{Binding Turno.ValorContadoEntrada, UpdateSourceTrigger=PropertyChanged}" />
                        <ui:Button Content="Fechar caixa" Appearance="Danger" Icon="{ui:SymbolIcon Symbol=LockClosed24}"
                                   Command="{Binding Turno.FecharCaixaCommand}" />
                    </WrapPanel>
                </StackPanel>
            </StackPanel>
        </ui:Card>
    </Grid>
</UserControl>
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 81 tests total (unchanged — XAML-only task).

- [ ] **Step 4: Manual smoke check**

Run: `dotnet run --project Lojinha.App` in the background, confirm the process starts and stays alive, then terminate it. Full interactive verification (opening/closing a session, registering sangria/suprimento, the `FinalizarVenda` gate) happens in Task 7's end-to-end walkthrough.

- [ ] **Step 5: Commit**

```bash
git add Lojinha.App/Views/VendaView.xaml
git commit -m "feat: add Turno tab UI for cash-session control"
```

---

### Task 7: Final integration check

**Files:** none (verification only).

- [ ] **Step 1: Full test suite**

Run: `dotnet test`
Expected: PASS, 81 tests total, 0 failures.

- [ ] **Step 2: Full build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 3: End-to-end manual walkthrough**

Run: `dotnet run --project Lojinha.App` and, in one session:

1. Log in as Admin. Go to Caixa, try to add an item and finalize a sale — confirm it's blocked with `"Abra o caixa antes de registrar uma venda."` (no session open yet).
2. Click the "Turno" tab — confirm it shows "Nenhum caixa aberto." and an abertura form. Enter a valor de abertura (e.g. 100) and click "Abrir caixa" — confirm it switches to the open-session view showing "Aberto desde" and the abertura value.
3. Go back to "Caixa", finalize a Dinheiro sale — confirm it now succeeds (the gate no longer blocks).
4. Go to "Turno", register a Suprimento (e.g. 50) — confirm it appears in the movimentos grid immediately, with no authorization prompt (Admin self-authorizes).
5. Register a Sangria (e.g. 30) — same check.
6. Click "Fechar caixa" with a valor contado matching `ValorAbertura + vendas Dinheiro + Suprimento - Sangria` — confirm the snackbar reports `Diferença: R$ 0,00` and the Turno tab reverts to the "Nenhum caixa aberto." state.
7. Log out, log in as a Vendedor. Open a new session (should work without Admin involvement). Try to register a Sangria — confirm the `AutorizacaoWindow` appears; cancel it — confirm the movement is NOT registered (`"Movimento não autorizado."`); retry with valid Admin credentials — confirm it registers with `AutorizadoPor` showing the Admin's name, not the Vendedor's, and the Vendedor's own session stays logged in throughout.
8. As the same Vendedor, register a Dinheiro sale, then close the caixa with a deliberately wrong valor contado (e.g. off by 5) — confirm the reported `Diferenca` is exactly ±5.
9. Confirm a fresh caixa can be opened again immediately after closing (no lingering "already open" error).

- [ ] **Step 4: Push**

```bash
git push
```
