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
}
