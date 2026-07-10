using Lojinha.Data;
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
}
