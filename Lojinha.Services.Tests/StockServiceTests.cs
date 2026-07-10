using Lojinha.Data;
using Lojinha.Data.Models;
using Lojinha.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Lojinha.Services.Tests;

public class StockServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LojinhaDbContext _context;
    private readonly StockService _service;

    public StockServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LojinhaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LojinhaDbContext(options);
        _context.Database.EnsureCreated();

        _service = new StockService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private Product CreateProduct(decimal estoqueMinimo = 0, string codigoBarras = "789000000001")
    {
        var category = new Category { Nome = "Bebidas" };
        var product = new Product
        {
            Nome = "Coca-Cola 2L",
            CodigoBarras = codigoBarras,
            Category = category,
            TipoVenda = TipoVenda.Unidade,
            PrecoCusto = 5,
            PrecoVenda = 8,
            EstoqueMinimo = estoqueMinimo
        };
        _context.Products.Add(product);
        _context.SaveChanges();
        return product;
    }

    [Fact]
    public void GetCurrentStock_SumsRemainingQuantityAcrossLots()
    {
        var product = CreateProduct();
        _context.StockLots.Add(new StockLot { ProductId = product.Id, Quantidade = 10, QuantidadeRestante = 10, DataEntrada = DateTime.Today });
        _context.StockLots.Add(new StockLot { ProductId = product.Id, Quantidade = 5, QuantidadeRestante = 3, DataEntrada = DateTime.Today });
        _context.SaveChanges();

        var stock = _service.GetCurrentStock(product.Id);

        Assert.Equal(13, stock);
    }

    [Fact]
    public void AddLot_CreatesLotWithFullQuantidadeRestante()
    {
        var product = CreateProduct();

        var lot = _service.AddLot(product.Id, quantidade: 20, dataValidade: null, supplierId: null);

        Assert.Equal(20, lot.Quantidade);
        Assert.Equal(20, lot.QuantidadeRestante);
        Assert.Equal(20, _service.GetCurrentStock(product.Id));
    }

    [Fact]
    public void AddLot_ThrowsWhenQuantidadeIsNotPositive()
    {
        var product = CreateProduct();

        Assert.Throws<ArgumentException>(() => _service.AddLot(product.Id, quantidade: 0, dataValidade: null, supplierId: null));
    }

    [Fact]
    public void GetLowStockProducts_ReturnsProductsBelowMinimum()
    {
        var lowProduct = CreateProduct(estoqueMinimo: 10);
        _service.AddLot(lowProduct.Id, quantidade: 3, dataValidade: null, supplierId: null);

        var okProduct = CreateProduct(estoqueMinimo: 10, codigoBarras: "789000000002");
        _service.AddLot(okProduct.Id, quantidade: 15, dataValidade: null, supplierId: null);

        var lowStock = _service.GetLowStockProducts();

        Assert.Contains(lowStock, p => p.Id == lowProduct.Id);
        Assert.DoesNotContain(lowStock, p => p.Id == okProduct.Id);
    }

    [Fact]
    public void GetExpiringLots_ReturnsLotsWithinThresholdDays()
    {
        var product = CreateProduct();
        _service.AddLot(product.Id, quantidade: 5, dataValidade: DateTime.Today.AddDays(3), supplierId: null);
        _service.AddLot(product.Id, quantidade: 5, dataValidade: DateTime.Today.AddDays(30), supplierId: null);

        var expiring = _service.GetExpiringLots(diasLimite: 7);

        Assert.Single(expiring);
        Assert.Equal(DateTime.Today.AddDays(3), expiring.First().DataValidade);
    }

    [Fact]
    public void GetExpiredLots_ReturnsLotsWithPastDataValidade()
    {
        var product = CreateProduct();
        _service.AddLot(product.Id, quantidade: 5, dataValidade: DateTime.Today.AddDays(-1), supplierId: null);
        _service.AddLot(product.Id, quantidade: 5, dataValidade: DateTime.Today.AddDays(5), supplierId: null);

        var expired = _service.GetExpiredLots();

        Assert.Single(expired);
        Assert.Equal(DateTime.Today.AddDays(-1), expired.First().DataValidade);
    }
}
