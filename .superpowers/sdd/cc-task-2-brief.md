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

