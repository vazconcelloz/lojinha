using Lojinha.Data;
using Lojinha.Data.Models;
using Lojinha.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Lojinha.Services.Tests;

public class CategoryServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LojinhaDbContext _context;
    private readonly CategoryService _service;

    public CategoryServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LojinhaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LojinhaDbContext(options);
        _context.Database.EnsureCreated();

        _service = new CategoryService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void Add_CreatesCategoryWithGivenName()
    {
        var category = _service.Add("Bebidas");

        Assert.True(category.Id > 0);
        Assert.Equal("Bebidas", category.Nome);
        Assert.Single(_service.GetAll());
    }

    [Fact]
    public void Add_ThrowsWhenNameIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => _service.Add(""));
    }

    [Fact]
    public void Delete_RemovesCategoryWithoutProducts()
    {
        var category = _service.Add("Bebidas");

        _service.Delete(category.Id);

        Assert.Empty(_service.GetAll());
    }

    [Fact]
    public void Delete_ThrowsWhenCategoryHasProducts()
    {
        var category = _service.Add("Bebidas");
        _context.Products.Add(new Product
        {
            Nome = "Coca-Cola 2L",
            CodigoBarras = "789000000001",
            CategoryId = category.Id,
            TipoVenda = TipoVenda.Unidade,
            PrecoCusto = 5,
            PrecoVenda = 8,
            EstoqueMinimo = 10
        });
        _context.SaveChanges();

        Assert.Throws<InvalidOperationException>(() => _service.Delete(category.Id));
    }
}
