# Módulo de Vendas Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Vendas (Sales) module to Lojinha: multi-item cart sales with automatic stock deduction, cancellation with stock restoration, and sale history — as a 5th screen in the existing WPF-UI Fluent shell.

**Architecture:** New `Sale`/`SaleItem` EF Core models feed a new `SalesService` that orchestrates stock deduction through the existing `StockService` (a deliberate exception to this codebase's "services only depend on `LojinhaDbContext`" convention — reusing `StockService.DeductStock`/`AddLot` avoids duplicating FIFO-deduction logic in two places). A new `SalesViewModel` + `VendaView.xaml` follow the exact MVVM/DI/navigation pattern already established by the Estoque screen.

**Tech Stack:** .NET 8, WPF, WPF-UI 4.3.0 (Fluent controls), CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection, EF Core 8 + SQLite, xUnit.

## Global Constraints

- `FormaPagamento` enum values are exactly `Dinheiro`, `Cartao`, `Pix` (spec, `docs/superpowers/specs/2026-07-13-modulo-vendas-design.md`).
- Money fields (`Sale.Total`, `SaleItem.PrecoUnitario`) use `HasPrecision(10, 2)`, matching `Product.PrecoCusto`/`PrecoVenda`. Quantity fields (`SaleItem.Quantidade`) use `HasPrecision(10, 3)`, matching `StockLot.Quantidade`.
- Every new/changed UI copy (button text, dialog text, snackbar text, empty-state text) is in Portuguese, consistent with the rest of the app.
- Every service method that fails throws `InvalidOperationException` (business-rule failures) or `ArgumentException` (invalid input) with a Portuguese, user-facing message — consistent with the existing services.
- `SalesService` depends on `LojinhaDbContext` **and** `StockService` (not just context) — an explicit, deliberate deviation from the other services' context-only pattern, done to reuse `StockService.DeductStock`/`AddLot` instead of duplicating lot-deduction logic. Do not "fix" this back to context-only.
- `MainWindow.xaml.cs` has three places that switch on the screen tag string (`NavigationViewItem_OnClick`'s `IsActive` assignments, `NavigateTo`'s view-selection switch, `RefreshViewModel`'s refresh switch) — adding a new screen requires updating all three. This is a known, accepted duplication (already flagged non-blocking in a prior review) — just don't miss one of the three when adding `"vendas"`.
- Every screen's ViewModel exposes a public `Refresh()` method, called by `MainWindow.xaml.cs`'s `RefreshViewModel` when the user navigates to that screen — this is what fixed a real bug (stale cross-screen data) in the previous module. `SalesViewModel` must implement `Refresh()` from the start.
- WPF-UI `SymbolIcon` names used below are verified against the actually-installed WPF-UI 4.3.0 package (via the public source at tag `4.3.0`): `ShoppingBag24`, `Add24` (already used elsewhere), `Delete24` (already used elsewhere), `Dismiss24`, `ReceiptMoney24`. Do not substitute icon names without similarly verifying they exist in this exact package version.
- No automated UI tests in this plan (per spec) — frontend tasks are verified by `dotnet build` + a manual smoke run described in each task.
- No Mica/Acrylic/blur, no changes to `WindowBackdropType` (unrelated to this plan — just don't touch it).

---

### Task 1: `Sale`/`SaleItem` data models, `DbContext` wiring, migration, and `ProductService.Delete` guard

**Files:**
- Create: `Lojinha.Data/Models/FormaPagamento.cs`
- Create: `Lojinha.Data/Models/Sale.cs`
- Create: `Lojinha.Data/Models/SaleItem.cs`
- Modify: `Lojinha.Data/LojinhaDbContext.cs`
- Modify: `Lojinha.Services/ProductService.cs`
- Test: `Lojinha.Services.Tests/ProductServiceTests.cs`
- Migration: generated under `Lojinha.Data/Migrations/`

**Interfaces:**
- Produces: `FormaPagamento` enum (`Dinheiro`, `Cartao`, `Pix`); `Sale` (`Id`, `DataHora`, `FormaPagamento`, `Total`, `Cancelada`, `DataCancelamento`, `Items`); `SaleItem` (`Id`, `SaleId`, `Sale`, `ProductId`, `Product`, `Quantidade`, `PrecoUnitario`, computed `Subtotal`); `LojinhaDbContext.Sales`/`SaleItems` `DbSet`s — consumed by Task 2 and Task 3.

- [ ] **Step 1: Create the `FormaPagamento` enum**

Create `Lojinha.Data/Models/FormaPagamento.cs`:

```csharp
namespace Lojinha.Data.Models;

public enum FormaPagamento
{
    Dinheiro,
    Cartao,
    Pix
}
```

- [ ] **Step 2: Create the `Sale` model**

Create `Lojinha.Data/Models/Sale.cs`:

```csharp
namespace Lojinha.Data.Models;

public class Sale
{
    public int Id { get; set; }
    public DateTime DataHora { get; set; }
    public FormaPagamento FormaPagamento { get; set; }
    public decimal Total { get; set; }
    public bool Cancelada { get; set; }
    public DateTime? DataCancelamento { get; set; }

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}
```

- [ ] **Step 3: Create the `SaleItem` model**

Create `Lojinha.Data/Models/SaleItem.cs`:

```csharp
namespace Lojinha.Data.Models;

public class SaleItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }

    public decimal Subtotal => Quantidade * PrecoUnitario;
}
```

- [ ] **Step 4: Wire `Sale`/`SaleItem` into `LojinhaDbContext`**

In `Lojinha.Data/LojinhaDbContext.cs`, add two `DbSet` properties after `StockLots`:

```csharp
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockLot> StockLots => Set<StockLot>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
```

Then, inside `OnModelCreating`, after the existing `StockLot`/`Supplier` relationship configuration (after the `modelBuilder.Entity<StockLot>().HasOne(s => s.Supplier)...` block, before the closing brace of the method), add:

```csharp
        modelBuilder.Entity<Sale>()
            .Property(s => s.Total)
            .HasPrecision(10, 2);

        modelBuilder.Entity<SaleItem>()
            .Property(i => i.Quantidade)
            .HasPrecision(10, 3);

        modelBuilder.Entity<SaleItem>()
            .Property(i => i.PrecoUnitario)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Sale>()
            .HasMany(s => s.Items)
            .WithOne(i => i.Sale)
            .HasForeignKey(i => i.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SaleItem>()
            .HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
```

`SaleItem.ProductId` is `Restrict` (not `Cascade`) deliberately: a product with sale history must not be silently deletable in a way that rewrites past sales' items/totals. Step 6 below adds the corresponding guard in `ProductService.Delete`.

- [ ] **Step 5: Generate the EF Core migration**

Run: `dotnet ef migrations add AddSales --project Lojinha.Data`
Expected: no errors; two new files appear under `Lojinha.Data/Migrations/` (a new `..._AddSales.cs` migration + matching `.Designer.cs`), and `Lojinha.Data/Migrations/LojinhaDbContextModelSnapshot.cs` is updated to include `Sales`/`SaleItems` tables.

- [ ] **Step 6: Write the failing test for the `ProductService.Delete` guard**

Add `using Lojinha.Data.Models;` is already present in `Lojinha.Services.Tests/ProductServiceTests.cs`. Add this `[Fact]` inside the `ProductServiceTests` class, after `Delete_RemovesProduct`:

```csharp
    [Fact]
    public void Delete_ThrowsWhenProductHasSales()
    {
        var product = _service.Add("Coca-Cola 2L", "789000000001", _category.Id, TipoVenda.Unidade, 5m, 8m, 10m);
        var sale = new Sale { DataHora = DateTime.Now, FormaPagamento = FormaPagamento.Dinheiro, Total = 8m };
        sale.Items.Add(new SaleItem { ProductId = product.Id, Quantidade = 1, PrecoUnitario = 8m });
        _context.Sales.Add(sale);
        _context.SaveChanges();

        Assert.Throws<InvalidOperationException>(() => _service.Delete(product.Id));
    }
```

- [ ] **Step 7: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ProductServiceTests"`
Expected: `Delete_ThrowsWhenProductHasSales` FAILs (no guard yet — `Delete` succeeds instead of throwing).

- [ ] **Step 8: Implement the guard in `ProductService.Delete`**

In `Lojinha.Services/ProductService.cs`, modify `Delete` to add the guard before removing the product:

```csharp
    public void Delete(int id)
    {
        var product = _context.Products.Find(id);
        if (product is null)
        {
            throw new InvalidOperationException("Produto não encontrado.");
        }

        if (_context.SaleItems.Any(si => si.ProductId == id))
        {
            throw new InvalidOperationException("Produto possui vendas registradas e não pode ser excluído.");
        }

        _context.Products.Remove(product);
        _context.SaveChanges();
    }
```

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ProductServiceTests"`
Expected: PASS, 5 tests total for this class.

- [ ] **Step 10: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 19 tests total (18 existing + 1 new).

- [ ] **Step 11: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 12: Commit**

```bash
git add Lojinha.Data/Models/FormaPagamento.cs Lojinha.Data/Models/Sale.cs Lojinha.Data/Models/SaleItem.cs Lojinha.Data/LojinhaDbContext.cs Lojinha.Data/Migrations Lojinha.Services/ProductService.cs Lojinha.Services.Tests/ProductServiceTests.cs
git commit -m "feat: add Sale/SaleItem models, migration, and ProductService in-use guard"
```

---

### Task 2: `StockService.DeductStock`

**Files:**
- Modify: `Lojinha.Services/StockService.cs`
- Test: `Lojinha.Services.Tests/StockServiceTests.cs`

**Interfaces:**
- Produces: `StockService.DeductStock(int productId, decimal quantidade)` — throws `ArgumentException` if `quantidade <= 0`, throws `InvalidOperationException` if the product's total available stock is less than `quantidade`; otherwise deducts from the product's lots ordered by `DataEntrada` ascending (oldest first) until the requested quantity is consumed. Consumed by Task 3's `SalesService`.

- [ ] **Step 1: Write the failing tests**

Add these three `[Fact]` methods inside `Lojinha.Services.Tests/StockServiceTests.cs`'s `StockServiceTests` class, after `DeleteLot_RemovesLot`:

```csharp
    [Fact]
    public void DeductStock_ConsumesOldestLotFirst()
    {
        var product = CreateProduct();
        var loteAntigo = new StockLot { ProductId = product.Id, Quantidade = 5, QuantidadeRestante = 5, DataEntrada = DateTime.Today.AddDays(-2) };
        var loteNovo = new StockLot { ProductId = product.Id, Quantidade = 5, QuantidadeRestante = 5, DataEntrada = DateTime.Today };
        _context.StockLots.AddRange(loteAntigo, loteNovo);
        _context.SaveChanges();

        _service.DeductStock(product.Id, 7);

        Assert.Equal(0, _context.StockLots.Find(loteAntigo.Id)!.QuantidadeRestante);
        Assert.Equal(3, _context.StockLots.Find(loteNovo.Id)!.QuantidadeRestante);
    }

    [Fact]
    public void DeductStock_ThrowsWhenInsufficientStock()
    {
        var product = CreateProduct();
        _service.AddLot(product.Id, quantidade: 5, dataValidade: null, supplierId: null);

        Assert.Throws<InvalidOperationException>(() => _service.DeductStock(product.Id, 10));
        Assert.Equal(5, _service.GetCurrentStock(product.Id));
    }

    [Fact]
    public void DeductStock_ThrowsWhenQuantidadeIsNotPositive()
    {
        var product = CreateProduct();
        _service.AddLot(product.Id, quantidade: 5, dataValidade: null, supplierId: null);

        Assert.Throws<ArgumentException>(() => _service.DeductStock(product.Id, 0));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~StockServiceTests"`
Expected: build error (`StockService` has no `DeductStock` method) or FAIL.

- [ ] **Step 3: Implement `DeductStock`**

In `Lojinha.Services/StockService.cs`, add this method after `DeleteLot`:

```csharp
    public void DeductStock(int productId, decimal quantidade)
    {
        if (quantidade <= 0)
        {
            throw new ArgumentException("Quantidade deve ser maior que zero.", nameof(quantidade));
        }

        var lotes = _context.StockLots
            .Where(l => l.ProductId == productId && l.QuantidadeRestante > 0)
            .OrderBy(l => l.DataEntrada)
            .ToList();

        var disponivel = lotes.Sum(l => l.QuantidadeRestante);
        if (disponivel < quantidade)
        {
            throw new InvalidOperationException("Estoque insuficiente para dar baixa.");
        }

        var restante = quantidade;
        foreach (var lote in lotes)
        {
            if (restante <= 0)
            {
                break;
            }

            var consumido = Math.Min(lote.QuantidadeRestante, restante);
            lote.QuantidadeRestante -= consumido;
            restante -= consumido;
        }

        _context.SaveChanges();
    }
```

The insufficient-stock check happens **before** any `StockLot` is mutated (computing `disponivel` from the fetched list, not from a running total during the mutation loop) — this guarantees that if the method throws, no entity has been modified yet, so a shared `DbContext` (as this app uses) can't end up with stray unsaved changes from a failed call.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~StockServiceTests"`
Expected: PASS, 10 tests total for this class.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 22 tests total.

- [ ] **Step 6: Commit**

```bash
git add Lojinha.Services/StockService.cs Lojinha.Services.Tests/StockServiceTests.cs
git commit -m "feat: add StockService.DeductStock (oldest-lot-first, mechanical)"
```

---

### Task 3: `SalesService` (register, cancel, history)

**Files:**
- Create: `Lojinha.Services/SalesService.cs`
- Test: `Lojinha.Services.Tests/SalesServiceTests.cs`

**Interfaces:**
- Consumes: `StockService.GetCurrentStock(int)`, `StockService.DeductStock(int, decimal)` (Task 2), `StockService.AddLot(int, decimal, DateTime?, int?)` (existing), `LojinhaDbContext.Sales`/`SaleItems`/`Products` (Task 1).
- Produces: `SalesService.RegisterSale(IEnumerable<(int ProductId, decimal Quantidade)> itens, FormaPagamento formaPagamento) : Sale`, `SalesService.CancelSale(int saleId)`, `SalesService.GetSaleHistory() : IEnumerable<Sale>` — consumed by Task 4's `SalesViewModel`.

- [ ] **Step 1: Write the failing tests**

Create `Lojinha.Services.Tests/SalesServiceTests.cs`:

```csharp
using Lojinha.Data;
using Lojinha.Data.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Lojinha.Services.Tests;

public class SalesServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LojinhaDbContext _context;
    private readonly SalesService _service;
    private readonly StockService _stockService;
    private readonly Category _category;

    public SalesServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LojinhaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LojinhaDbContext(options);
        _context.Database.EnsureCreated();

        _stockService = new StockService(_context);
        _service = new SalesService(_context, _stockService);

        _category = new Category { Nome = "Bebidas" };
        _context.Categories.Add(_category);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private Product CreateProduct(decimal precoVenda = 8m, string codigoBarras = "789000000001")
    {
        var product = new Product
        {
            Nome = "Coca-Cola 2L",
            CodigoBarras = codigoBarras,
            CategoryId = _category.Id,
            TipoVenda = TipoVenda.Unidade,
            PrecoCusto = 5m,
            PrecoVenda = precoVenda,
            EstoqueMinimo = 0
        };
        _context.Products.Add(product);
        _context.SaveChanges();
        return product;
    }

    [Fact]
    public void RegisterSale_ThrowsWhenCartIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => _service.RegisterSale(Array.Empty<(int, decimal)>(), FormaPagamento.Dinheiro));
    }

    [Fact]
    public void RegisterSale_ThrowsWhenQuantidadeIsNotPositive()
    {
        var product = CreateProduct();
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        Assert.Throws<ArgumentException>(() => _service.RegisterSale(new[] { (product.Id, 0m) }, FormaPagamento.Dinheiro));
    }

    [Fact]
    public void RegisterSale_SingleItem_DeductsStockAndComputesTotal()
    {
        var product = CreateProduct(precoVenda: 8m);
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        var sale = _service.RegisterSale(new[] { (product.Id, 3m) }, FormaPagamento.Dinheiro);

        Assert.Equal(24m, sale.Total);
        Assert.Equal(7m, _stockService.GetCurrentStock(product.Id));
    }

    [Fact]
    public void RegisterSale_MultiItem_DeductsAllAndSumsTotal()
    {
        var product1 = CreateProduct(precoVenda: 8m, codigoBarras: "789000000001");
        var product2 = CreateProduct(precoVenda: 5m, codigoBarras: "789000000002");
        _stockService.AddLot(product1.Id, quantidade: 10, dataValidade: null, supplierId: null);
        _stockService.AddLot(product2.Id, quantidade: 10, dataValidade: null, supplierId: null);

        var sale = _service.RegisterSale(new[] { (product1.Id, 2m), (product2.Id, 4m) }, FormaPagamento.Cartao);

        Assert.Equal(36m, sale.Total);
        Assert.Equal(8m, _stockService.GetCurrentStock(product1.Id));
        Assert.Equal(6m, _stockService.GetCurrentStock(product2.Id));
    }

    [Fact]
    public void RegisterSale_ThrowsWhenStockInsufficient_AndLeavesStockUnchanged()
    {
        var product1 = CreateProduct(precoVenda: 8m, codigoBarras: "789000000001");
        var product2 = CreateProduct(precoVenda: 5m, codigoBarras: "789000000002");
        _stockService.AddLot(product1.Id, quantidade: 10, dataValidade: null, supplierId: null);
        _stockService.AddLot(product2.Id, quantidade: 2, dataValidade: null, supplierId: null);

        Assert.Throws<InvalidOperationException>(() =>
            _service.RegisterSale(new[] { (product1.Id, 2m), (product2.Id, 5m) }, FormaPagamento.Pix));

        Assert.Equal(10m, _stockService.GetCurrentStock(product1.Id));
        Assert.Equal(2m, _stockService.GetCurrentStock(product2.Id));
        Assert.Empty(_service.GetSaleHistory());
    }

    [Fact]
    public void RegisterSale_SnapshotsPrecoUnitarioAtSaleTime()
    {
        var product = CreateProduct(precoVenda: 8m);
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        _service.RegisterSale(new[] { (product.Id, 1m) }, FormaPagamento.Dinheiro);

        product.PrecoVenda = 20m;
        _context.SaveChanges();

        var venda = _service.GetSaleHistory().First();
        Assert.Equal(8m, venda.Items.First().PrecoUnitario);
    }

    [Fact]
    public void CancelSale_RestoresStockViaNewLot()
    {
        var product = CreateProduct();
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);
        var sale = _service.RegisterSale(new[] { (product.Id, 4m) }, FormaPagamento.Dinheiro);

        _service.CancelSale(sale.Id);

        Assert.Equal(10m, _stockService.GetCurrentStock(product.Id));
        Assert.True(_service.GetSaleHistory().First(s => s.Id == sale.Id).Cancelada);
    }

    [Fact]
    public void CancelSale_ThrowsWhenAlreadyCancelled()
    {
        var product = CreateProduct();
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);
        var sale = _service.RegisterSale(new[] { (product.Id, 1m) }, FormaPagamento.Dinheiro);
        _service.CancelSale(sale.Id);

        Assert.Throws<InvalidOperationException>(() => _service.CancelSale(sale.Id));
    }

    [Fact]
    public void CancelSale_ThrowsWhenSaleNotFound()
    {
        Assert.Throws<InvalidOperationException>(() => _service.CancelSale(999));
    }

    [Fact]
    public void GetSaleHistory_OrdersByDataHoraDescending()
    {
        var product = CreateProduct();
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);
        var sale1 = _service.RegisterSale(new[] { (product.Id, 1m) }, FormaPagamento.Dinheiro);
        var sale2 = _service.RegisterSale(new[] { (product.Id, 1m) }, FormaPagamento.Cartao);

        var historico = _service.GetSaleHistory().ToList();

        Assert.Equal(sale2.Id, historico.First().Id);
        Assert.Equal(sale1.Id, historico.Last().Id);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~SalesServiceTests"`
Expected: build error (`SalesService` doesn't exist yet).

- [ ] **Step 3: Implement `SalesService`**

Create `Lojinha.Services/SalesService.cs`:

```csharp
using Lojinha.Data;
using Lojinha.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Lojinha.Services;

public class SalesService
{
    private readonly LojinhaDbContext _context;
    private readonly StockService _stockService;

    public SalesService(LojinhaDbContext context, StockService stockService)
    {
        _context = context;
        _stockService = stockService;
    }

    public Sale RegisterSale(IEnumerable<(int ProductId, decimal Quantidade)> itens, FormaPagamento formaPagamento)
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
            Cancelada = false
        };

        decimal total = 0;
        foreach (var item in itensList)
        {
            var produto = produtos[item.ProductId];
            var saleItem = new SaleItem
            {
                ProductId = item.ProductId,
                Quantidade = item.Quantidade,
                PrecoUnitario = produto.PrecoVenda
            };
            sale.Items.Add(saleItem);
            total += saleItem.Subtotal;
        }
        sale.Total = total;

        _context.Sales.Add(sale);

        foreach (var item in itensList)
        {
            _stockService.DeductStock(item.ProductId, item.Quantidade);
        }

        _context.SaveChanges();

        return sale;
    }

    public void CancelSale(int id)
    {
        var sale = _context.Sales
            .Include(s => s.Items)
            .FirstOrDefault(s => s.Id == id);

        if (sale is null)
        {
            throw new InvalidOperationException("Venda não encontrada.");
        }

        if (sale.Cancelada)
        {
            throw new InvalidOperationException("Venda já foi cancelada.");
        }

        sale.Cancelada = true;
        sale.DataCancelamento = DateTime.Now;

        foreach (var item in sale.Items)
        {
            _stockService.AddLot(item.ProductId, item.Quantidade, dataValidade: null, supplierId: null);
        }

        _context.SaveChanges();
    }

    public IEnumerable<Sale> GetSaleHistory()
    {
        return _context.Sales
            .Include(s => s.Items)
            .ThenInclude(i => i.Product)
            .OrderByDescending(s => s.DataHora)
            .ToList();
    }
}
```

Note: `_context.Sales.Add(sale)` stages the sale before the `DeductStock` loop runs; since `StockService.DeductStock` calls `_context.SaveChanges()` internally (same shared `DbContext` instance), the staged `Sale`/`SaleItem`s get flushed to the database as a side effect of the first `DeductStock` call. The final explicit `_context.SaveChanges()` is a harmless no-op safety net if that internal behavior ever changes. This is safe specifically because all stock-sufficiency validation already happened above, before anything was staged — nothing in this method can throw after staging begins.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~SalesServiceTests"`
Expected: PASS, 10 tests total for this class.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 32 tests total.

- [ ] **Step 6: Commit**

```bash
git add Lojinha.Services/SalesService.cs Lojinha.Services.Tests/SalesServiceTests.cs
git commit -m "feat: add SalesService (register, cancel, history)"
```

---

### Task 4: Vendas feature slice (new screen)

**Files:**
- Create: `Lojinha.App/ViewModels/SalesViewModel.cs`
- Create: `Lojinha.App/Views/VendaView.xaml`
- Create: `Lojinha.App/Views/VendaView.xaml.cs`
- Modify: `Lojinha.App/ViewModels/MainViewModel.cs`
- Modify: `Lojinha.App/App.xaml.cs`
- Modify: `Lojinha.App/MainWindow.xaml`
- Modify: `Lojinha.App/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `SalesService.RegisterSale`/`CancelSale`/`GetSaleHistory` (Task 3), `ProductService.Search(string)`/`GetAll()` (existing), `ISnackbarService`/`IContentDialogService` (existing DI singletons), `CountToVisibilityConverter` resource (existing).
- Produces: `SalesViewModel` with `Produtos`, `Carrinho` (`ObservableCollection<ItemCarrinho>`), `Historico` (`ObservableCollection<VendaHistoricoItem>`), `Total` (computed), `AdicionarAoCarrinhoCommand`, `RemoverDoCarrinhoCommand` (parameter `ItemCarrinho`), `FinalizarVendaCommand`, `CancelarVendaCommand` (parameter `VendaHistoricoItem`), public `Refresh()`. `MainViewModel.Vendas` property (type `SalesViewModel`).

- [ ] **Step 1: Create `SalesViewModel`**

Create `Lojinha.App/ViewModels/SalesViewModel.cs`:

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

public record ItemCarrinho(int ProductId, string Produto, decimal Quantidade, decimal PrecoUnitario)
{
    public decimal Subtotal => Quantidade * PrecoUnitario;
}

public record VendaHistoricoItem(int SaleId, DateTime DataHora, decimal Total, FormaPagamento FormaPagamento, bool Cancelada)
{
    public string Status => Cancelada ? "Cancelada" : "Concluída";
    public bool PodeCancelar => !Cancelada;
}

public partial class SalesViewModel : ObservableObject
{
    private readonly SalesService _salesService;
    private readonly ProductService _productService;
    private readonly ISnackbarService _snackbar;
    private readonly IContentDialogService _dialogService;

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

    public SalesViewModel(SalesService salesService, ProductService productService, ISnackbarService snackbar, IContentDialogService dialogService)
    {
        _salesService = salesService;
        _productService = productService;
        _snackbar = snackbar;
        _dialogService = dialogService;
        Carrinho.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Total));
        CarregarProdutos();
        CarregarHistorico();
    }

    public void Refresh()
    {
        CarregarProdutos();
        CarregarHistorico();
    }

    private void CarregarProdutos()
    {
        var produtoSelecionadoId = ProdutoSelecionado?.Id;

        Produtos.Clear();
        foreach (var produto in _productService.Search(TermoBusca))
        {
            Produtos.Add(produto);
        }

        ProdutoSelecionado = produtoSelecionadoId is null
            ? null
            : Produtos.FirstOrDefault(p => p.Id == produtoSelecionadoId);
    }

    private void CarregarHistorico()
    {
        Historico.Clear();
        foreach (var venda in _salesService.GetSaleHistory())
        {
            Historico.Add(new VendaHistoricoItem(venda.Id, venda.DataHora, venda.Total, venda.FormaPagamento, venda.Cancelada));
        }
    }

    partial void OnTermoBuscaChanged(string value)
    {
        CarregarProdutos();
    }

    [RelayCommand]
    private void AdicionarAoCarrinho()
    {
        if (ProdutoSelecionado is null)
        {
            _snackbar.Show("Erro", "Selecione um produto.", ControlAppearance.Danger);
            return;
        }

        if (Quantidade <= 0)
        {
            _snackbar.Show("Erro", "Quantidade deve ser maior que zero.", ControlAppearance.Danger);
            return;
        }

        Carrinho.Add(new ItemCarrinho(ProdutoSelecionado.Id, ProdutoSelecionado.Nome, Quantidade, ProdutoSelecionado.PrecoVenda));
        Quantidade = 0;
    }

    [RelayCommand]
    private void RemoverDoCarrinho(ItemCarrinho item)
    {
        Carrinho.Remove(item);
    }

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
            _salesService.RegisterSale(itens, FormaPagamentoSelecionada);
            Carrinho.Clear();
            CarregarHistorico();
            _snackbar.Show("Sucesso", "Venda registrada.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private async Task CancelarVenda(VendaHistoricoItem item)
    {
        var options = new SimpleContentDialogCreateOptions
        {
            Title = "Cancelar venda",
            Content = $"Tem certeza que deseja cancelar a venda de {item.DataHora:dd/MM/yyyy HH:mm}? O estoque vendido será devolvido.",
            PrimaryButtonText = "Cancelar venda",
            CloseButtonText = "Voltar",
        };

        var result = await _dialogService.ShowSimpleDialogAsync(options, CancellationToken.None);
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            _salesService.CancelSale(item.SaleId);
            CarregarHistorico();
            _snackbar.Show("Sucesso", "Venda cancelada.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }
}
```

`Total` is a computed property (not `[ObservableProperty]`); the `Carrinho.CollectionChanged` subscription in the constructor raises `PropertyChanged(nameof(Total))` on every add/remove/clear, which is what the XAML binding in Step 2 relies on to keep the displayed total in sync.

- [ ] **Step 2: Create `VendaView.xaml`**

Create `Lojinha.App/Views/VendaView.xaml`:

```xml
<UserControl x:Class="Lojinha.App.Views.VendaView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
             mc:Ignorable="d">
    <UserControl.Resources>
        <BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter" />
    </UserControl.Resources>
    <ScrollViewer Margin="20" VerticalScrollBarVisibility="Auto">
        <StackPanel>
            <ui:Card Margin="0,0,0,16">
                <StackPanel>
                    <TextBlock Text="Nova venda" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />
                    <WrapPanel>
                        <ui:TextBox Width="220" Margin="0,0,8,8" PlaceholderText="Buscar produto (nome ou código)"
                                    Text="{Binding TermoBusca, UpdateSourceTrigger=PropertyChanged}" />
                        <ComboBox Width="220" Margin="0,0,8,8" ItemsSource="{Binding Produtos}" DisplayMemberPath="Nome"
                                  SelectedItem="{Binding ProdutoSelecionado}" />
                        <ui:TextBox Width="120" Margin="0,0,8,8" PlaceholderText="Quantidade"
                                    Text="{Binding Quantidade, UpdateSourceTrigger=PropertyChanged}" />
                        <ui:Button Content="Adicionar ao carrinho" Appearance="Primary" Icon="{ui:SymbolIcon Symbol=Add24}"
                                   Command="{Binding AdicionarAoCarrinhoCommand}" />
                    </WrapPanel>
                </StackPanel>
            </ui:Card>

            <ui:Card Margin="0,0,0,16">
                <StackPanel>
                    <TextBlock Text="Carrinho" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />
                    <StackPanel Visibility="{Binding Carrinho.Count, Converter={StaticResource CountToVisibilityConverter}}">
                        <TextBlock Text="Carrinho vazio." Opacity="0.7" />
                    </StackPanel>
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
                    <WrapPanel Margin="0,12,0,0">
                        <ComboBox Width="160" Margin="0,0,8,0" ItemsSource="{Binding FormasPagamento}"
                                  SelectedItem="{Binding FormaPagamentoSelecionada}" />
                        <TextBlock Text="{Binding Total, StringFormat='Total: {0:C}'}" FontWeight="Bold" FontSize="16"
                                   VerticalAlignment="Center" Margin="12,0,0,0" />
                        <ui:Button Content="Finalizar venda" Appearance="Primary" Margin="12,0,0,0"
                                   Icon="{ui:SymbolIcon Symbol=ReceiptMoney24}"
                                   Command="{Binding FinalizarVendaCommand}" />
                    </WrapPanel>
                </StackPanel>
            </ui:Card>

            <ui:Card>
                <StackPanel>
                    <TextBlock Text="Histórico de vendas" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />
                    <StackPanel Visibility="{Binding Historico.Count, Converter={StaticResource CountToVisibilityConverter}}">
                        <TextBlock Text="Nenhuma venda registrada ainda." Opacity="0.7" />
                    </StackPanel>
                    <DataGrid ItemsSource="{Binding Historico}" AutoGenerateColumns="False" IsReadOnly="True" MaxHeight="240"
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
                            <DataGridTemplateColumn Header="" Width="140">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <ui:Button Content="Cancelar" Appearance="Danger" Icon="{ui:SymbolIcon Symbol=Dismiss24}"
                                                   Visibility="{Binding PodeCancelar, Converter={StaticResource BooleanToVisibilityConverter}}"
                                                   Command="{Binding DataContext.CancelarVendaCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
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

`BooleanToVisibilityConverter` is WPF's built-in converter (`System.Windows.Controls.BooleanToVisibilityConverter`), declared locally as a resource — no new converter class needed (`CountToVisibilityConverter` only handles `int` counts, not booleans, so it isn't reused for the `PodeCancelar` binding).

- [ ] **Step 3: Create `VendaView.xaml.cs`**

Create `Lojinha.App/Views/VendaView.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace Lojinha.App.Views;

public partial class VendaView : UserControl
{
    public VendaView()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 4: Add `Vendas` to `MainViewModel`**

Replace the full contents of `Lojinha.App/ViewModels/MainViewModel.cs` with:

```csharp
namespace Lojinha.App.ViewModels;

public class MainViewModel
{
    public CategoryViewModel Categorias { get; }
    public SupplierViewModel Fornecedores { get; }
    public ProductViewModel Produtos { get; }
    public StockViewModel Estoque { get; }
    public SalesViewModel Vendas { get; }

    public MainViewModel(CategoryViewModel categorias, SupplierViewModel fornecedores, ProductViewModel produtos, StockViewModel estoque, SalesViewModel vendas)
    {
        Categorias = categorias;
        Fornecedores = fornecedores;
        Produtos = produtos;
        Estoque = estoque;
        Vendas = vendas;
    }
}
```

- [ ] **Step 5: Register `SalesService`/`SalesViewModel` in `App.xaml.cs`**

In `Lojinha.App/App.xaml.cs`, in `ConfigureServices`, add `services.AddScoped<SalesService>();` right after `services.AddScoped<StockService>();`, and add `services.AddScoped<SalesViewModel>();` right after `services.AddScoped<StockViewModel>();`:

```csharp
        services.AddScoped<CategoryService>();
        services.AddScoped<SupplierService>();
        services.AddScoped<ProductService>();
        services.AddScoped<StockService>();
        services.AddScoped<SalesService>();

        services.AddScoped<CategoryViewModel>();
        services.AddScoped<SupplierViewModel>();
        services.AddScoped<ProductViewModel>();
        services.AddScoped<StockViewModel>();
        services.AddScoped<SalesViewModel>();
        services.AddScoped<MainViewModel>();
```

- [ ] **Step 6: Add the Vendas nav item to `MainWindow.xaml`**

In `Lojinha.App/MainWindow.xaml`, inside `<ui:NavigationView.MenuItems>`, add a 5th item after the `EstoqueItem` `NavigationViewItem`, before `</ui:NavigationView.MenuItems>`:

```xml
                <ui:NavigationViewItem x:Name="VendasItem" Content="Vendas" TargetPageTag="vendas"
                                        Click="NavigationViewItem_OnClick">
                    <ui:NavigationViewItem.Icon>
                        <ui:SymbolIcon Symbol="ShoppingBag24" />
                    </ui:NavigationViewItem.Icon>
                </ui:NavigationViewItem>
```

- [ ] **Step 7: Wire Vendas navigation in `MainWindow.xaml.cs`**

In `Lojinha.App/MainWindow.xaml.cs`, make these four changes:

1. Add a field next to the other four views:

```csharp
    private readonly VendaView _vendaView = new();
```

2. Add the `VendasItem.IsActive` line to `NavigationViewItem_OnClick`:

```csharp
    private void NavigationViewItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not NavigationViewItem { TargetPageTag: { } tag })
        {
            return;
        }

        CategoriasItem.IsActive = tag == "categorias";
        FornecedoresItem.IsActive = tag == "fornecedores";
        ProdutosItem.IsActive = tag == "produtos";
        EstoqueItem.IsActive = tag == "estoque";
        VendasItem.IsActive = tag == "vendas";

        NavigateTo(tag);
    }
```

3. Add the `"vendas"` case to the `switch` inside `NavigateTo`:

```csharp
        (FrameworkElement view, object dataContext) = tag switch
        {
            "categorias" => ((FrameworkElement)_categoriaView, (object)_viewModel.Categorias),
            "fornecedores" => ((FrameworkElement)_fornecedorView, (object)_viewModel.Fornecedores),
            "produtos" => ((FrameworkElement)_produtoView, (object)_viewModel.Produtos),
            "estoque" => ((FrameworkElement)_estoqueView, (object)_viewModel.Estoque),
            "vendas" => ((FrameworkElement)_vendaView, (object)_viewModel.Vendas),
            _ => ((FrameworkElement)_categoriaView, (object)_viewModel.Categorias)
        };
```

4. Add the `"vendas"` case to the `switch` inside `RefreshViewModel`:

```csharp
    private void RefreshViewModel(string tag)
    {
        switch (tag)
        {
            case "categorias":
                _viewModel.Categorias.Refresh();
                break;
            case "fornecedores":
                _viewModel.Fornecedores.Refresh();
                break;
            case "produtos":
                _viewModel.Produtos.Refresh();
                break;
            case "estoque":
                _viewModel.Estoque.Refresh();
                break;
            case "vendas":
                _viewModel.Vendas.Refresh();
                break;
            default:
                _viewModel.Categorias.Refresh();
                break;
        }
    }
```

- [ ] **Step 8: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 9: Manual smoke check**

Run: `dotnet run --project Lojinha.App`
Expected: a 5th "Vendas" sidebar item appears with a shopping-bag icon. **Click it** (this is the step a previous module's plan got wrong — clicking every sidebar item must actually switch the displayed screen; if it doesn't, that's a blocking bug, not a cosmetic one). On the Vendas screen: search/select a product, enter a quantity, "Adicionar ao carrinho" adds a row to the carrinho grid and updates the Total; "Finalizar venda" with an empty carrinho shows an error snackbar; with items, it registers the sale, clears the carrinho, shows a success snackbar, and the sale appears in "Histórico de vendas" below; clicking "Cancelar" on a history row asks for confirmation, and confirming marks it "Cancelada" (grayed out, no more Cancelar button) and shows a success snackbar. Also verify: add a product on the Produtos screen, then navigate to Vendas — the new product must appear in the search combo without restarting the app (this is the cross-screen-refresh convention from Global Constraints). Close the app when done.

- [ ] **Step 10: Commit**

```bash
git add Lojinha.App/ViewModels/SalesViewModel.cs Lojinha.App/Views/VendaView.xaml Lojinha.App/Views/VendaView.xaml.cs Lojinha.App/ViewModels/MainViewModel.cs Lojinha.App/App.xaml.cs Lojinha.App/MainWindow.xaml Lojinha.App/MainWindow.xaml.cs
git commit -m "feat: add Vendas screen (cart sale, automatic stock deduction, cancellation, history)"
```

---

### Task 5: Final integration check

**Files:** none (verification only).

- [ ] **Step 1: Full test suite**

Run: `dotnet test`
Expected: PASS, 32 tests total, 0 failures.

- [ ] **Step 2: Full build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 3: End-to-end manual walkthrough**

Run: `dotnet run --project Lojinha.App` and, in one session:
1. Add a category, a fornecedor, and two products (using that category) with different prices.
2. Go to Estoque, add stock lots for both products.
3. Go to Vendas: add both products to the carrinho with different quantities, pick a forma de pagamento, finalize the sale — confirm the total is correct and it appears in histórico.
4. Go back to Estoque — confirm the current-stock numbers reflect the sale (went down by the sold quantities).
5. Back on Vendas, try to sell more of a product than is currently in stock — confirm it's blocked with a clear error and nothing was registered.
6. Cancel the successful sale from histórico — confirm stock goes back up on the Estoque screen (navigate there to check) and the sale shows "Cancelada" with no cancel button anymore.
7. Toggle dark mode — confirm the Vendas screen (cart, total, histórico) stays legible in both themes.
8. Try to delete one of the two products from the Produtos screen — since it has sale history (even from the cancelled sale, since cancelling doesn't delete the `SaleItem` record), confirm it's blocked with the "possui vendas registradas" error instead of a raw exception.

- [ ] **Step 4: Push**

```bash
git push
```
