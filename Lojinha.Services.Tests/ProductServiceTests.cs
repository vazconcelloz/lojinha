using Lojinha.Data;
using Lojinha.Data.Models;
using Lojinha.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Lojinha.Services.Tests;

public class ProductServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LojinhaDbContext _context;
    private readonly ProductService _service;
    private readonly Category _category;

    public ProductServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LojinhaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LojinhaDbContext(options);
        _context.Database.EnsureCreated();

        _service = new ProductService(_context);

        _category = new Category { Nome = "Bebidas" };
        _context.Categories.Add(_category);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void Add_CreatesProductWithGivenData()
    {
        var product = _service.Add("Coca-Cola 2L", "789000000001", _category.Id, TipoVenda.Unidade, 5m, 8m, 10m);

        Assert.True(product.Id > 0);
        Assert.Equal("Coca-Cola 2L", product.Nome);
        Assert.Single(_service.GetAll());
    }

    [Fact]
    public void Add_ThrowsWhenNameIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            _service.Add("", "789000000001", _category.Id, TipoVenda.Unidade, 5m, 8m, 10m));
    }

    [Fact]
    public void Add_ThrowsWhenBarcodeAlreadyExists()
    {
        _service.Add("Coca-Cola 2L", "789000000001", _category.Id, TipoVenda.Unidade, 5m, 8m, 10m);

        Assert.Throws<InvalidOperationException>(() =>
            _service.Add("Guaraná 2L", "789000000001", _category.Id, TipoVenda.Unidade, 4m, 7m, 10m));
    }

    [Fact]
    public void Delete_RemovesProduct()
    {
        var product = _service.Add("Coca-Cola 2L", "789000000001", _category.Id, TipoVenda.Unidade, 5m, 8m, 10m);

        _service.Delete(product.Id);

        Assert.Empty(_service.GetAll());
    }

    [Fact]
    public void Delete_ThrowsWhenProductHasSales()
    {
        var product = _service.Add("Coca-Cola 2L", "789000000001", _category.Id, TipoVenda.Unidade, 5m, 8m, 10m);
        var sale = new Sale { DataHora = DateTime.Now, FormaPagamento = FormaPagamento.Dinheiro, Total = 8m };
        sale.Items.Add(new SaleItem { ProductId = product.Id, Quantidade = 1, PrecoUnitario = 8m });
        _context.Sales.Add(sale);
        _context.SaveChanges();

        var ex = Assert.Throws<InvalidOperationException>(() => _service.Delete(product.Id));
        Assert.Equal("Produto possui vendas registradas e não pode ser excluído.", ex.Message);
    }

    [Fact]
    public void Update_ChangesAllFields()
    {
        var product = _service.Add("Coca-Cola 2L", "789000000001", _category.Id, TipoVenda.Unidade, 5m, 8m, 10m);
        var novaCategoria = new Category { Nome = "Refrigerantes" };
        _context.Categories.Add(novaCategoria);
        _context.SaveChanges();

        _service.Update(product.Id, "Coca-Cola 2L Zero", "789000000002", novaCategoria.Id, TipoVenda.Peso, 6m, 9m, 12m);

        var atualizado = _service.GetAll().First();
        Assert.Equal("Coca-Cola 2L Zero", atualizado.Nome);
        Assert.Equal("789000000002", atualizado.CodigoBarras);
        Assert.Equal(novaCategoria.Id, atualizado.CategoryId);
        Assert.Equal(TipoVenda.Peso, atualizado.TipoVenda);
        Assert.Equal(6m, atualizado.PrecoCusto);
        Assert.Equal(9m, atualizado.PrecoVenda);
        Assert.Equal(12m, atualizado.EstoqueMinimo);
    }

    [Fact]
    public void Update_ThrowsWhenProductNotFound()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _service.Update(999, "Nome", "789000000001", _category.Id, TipoVenda.Unidade, 5m, 8m, 10m));
    }

    [Fact]
    public void Update_ThrowsWhenBarcodeBelongsToAnotherProduct()
    {
        var product1 = _service.Add("Coca-Cola 2L", "789000000001", _category.Id, TipoVenda.Unidade, 5m, 8m, 10m);
        _service.Add("Guaraná 2L", "789000000002", _category.Id, TipoVenda.Unidade, 4m, 7m, 10m);

        Assert.Throws<InvalidOperationException>(() =>
            _service.Update(product1.Id, "Coca-Cola 2L", "789000000002", _category.Id, TipoVenda.Unidade, 5m, 8m, 10m));
    }

    [Fact]
    public void Update_AllowsSavingWithSameBarcode()
    {
        var product = _service.Add("Coca-Cola 2L", "789000000001", _category.Id, TipoVenda.Unidade, 5m, 8m, 10m);

        _service.Update(product.Id, "Coca-Cola 2L Editado", "789000000001", _category.Id, TipoVenda.Unidade, 5m, 8m, 10m);

        Assert.Equal("Coca-Cola 2L Editado", _service.GetAll().First().Nome);
    }
}
