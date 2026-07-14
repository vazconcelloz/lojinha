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

    [Fact]
    public void RegisterSale_StoresUsuarioNomeWhenProvided()
    {
        var product = CreateProduct();
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        var sale = _service.RegisterSale(new[] { (product.Id, 1m) }, FormaPagamento.Dinheiro, "vendedor1");

        Assert.Equal("vendedor1", sale.UsuarioNome);
    }
}
