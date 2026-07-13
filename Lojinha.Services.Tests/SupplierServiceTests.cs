using Lojinha.Data;
using Lojinha.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Lojinha.Services.Tests;

public class SupplierServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LojinhaDbContext _context;
    private readonly SupplierService _service;

    public SupplierServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LojinhaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LojinhaDbContext(options);
        _context.Database.EnsureCreated();

        _service = new SupplierService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void Add_CreatesSupplierWithNameAndContact()
    {
        var supplier = _service.Add("Padaria Insumos LTDA", "(11) 99999-0000");

        Assert.True(supplier.Id > 0);
        Assert.Equal("Padaria Insumos LTDA", supplier.Nome);
        Assert.Equal("(11) 99999-0000", supplier.Contato);
        Assert.Single(_service.GetAll());
    }

    [Fact]
    public void Add_ThrowsWhenNameIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => _service.Add("", null));
    }

    [Fact]
    public void Delete_RemovesSupplier()
    {
        var supplier = _service.Add("Padaria Insumos LTDA", null);

        _service.Delete(supplier.Id);

        Assert.Empty(_service.GetAll());
    }

    [Fact]
    public void Update_ChangesNameAndContact()
    {
        var supplier = _service.Add("Padaria Insumos LTDA", "(11) 99999-0000");

        _service.Update(supplier.Id, "Padaria Insumos ME", "(11) 98888-1111");

        var atualizado = _service.GetAll().First();
        Assert.Equal("Padaria Insumos ME", atualizado.Nome);
        Assert.Equal("(11) 98888-1111", atualizado.Contato);
    }

    [Fact]
    public void Update_ThrowsWhenSupplierNotFound()
    {
        Assert.Throws<InvalidOperationException>(() => _service.Update(999, "Nome", null));
    }

    [Fact]
    public void Update_ThrowsWhenNameIsEmpty()
    {
        var supplier = _service.Add("Padaria Insumos LTDA", null);

        Assert.Throws<ArgumentException>(() => _service.Update(supplier.Id, "", null));
    }
}
