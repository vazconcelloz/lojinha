using Lojinha.Data;
using Lojinha.Data.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Lojinha.Services.Tests;

public class UserServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LojinhaDbContext _context;
    private readonly UserService _service;

    public UserServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LojinhaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LojinhaDbContext(options);
        _context.Database.EnsureCreated();

        _service = new UserService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void AnyUsers_ReturnsFalseWhenNoUsersExist()
    {
        Assert.False(_service.AnyUsers());
    }

    [Fact]
    public void Add_CreatesUserWithHashedPassword()
    {
        var user = _service.Add("admin", "senha123", PapelUsuario.Admin);

        Assert.True(user.Id > 0);
        Assert.Equal("admin", user.NomeUsuario);
        Assert.Equal(PapelUsuario.Admin, user.Papel);
        Assert.Equal(32, user.SenhaHash.Length);
        Assert.Equal(16, user.SenhaSalt.Length);
        Assert.True(_service.AnyUsers());
    }

    [Fact]
    public void Add_ThrowsWhenUsernameAlreadyExists()
    {
        _service.Add("admin", "senha123", PapelUsuario.Admin);

        Assert.Throws<InvalidOperationException>(() => _service.Add("admin", "outrasenha", PapelUsuario.Vendedor));
    }

    [Fact]
    public void Authenticate_ReturnsUserWithCorrectCredentials()
    {
        _service.Add("admin", "senha123", PapelUsuario.Admin);

        var user = _service.Authenticate("admin", "senha123");

        Assert.Equal("admin", user.NomeUsuario);
    }

    [Fact]
    public void Authenticate_ThrowsWithWrongPassword()
    {
        _service.Add("admin", "senha123", PapelUsuario.Admin);

        Assert.Throws<InvalidOperationException>(() => _service.Authenticate("admin", "senhaerrada"));
    }

    [Fact]
    public void Authenticate_ThrowsWithUnknownUsername()
    {
        Assert.Throws<InvalidOperationException>(() => _service.Authenticate("naoexiste", "senha123"));
    }

    [Fact]
    public void Update_WithoutNewPassword_KeepsOldPasswordWorking()
    {
        var user = _service.Add("admin", "senha123", PapelUsuario.Admin);

        _service.Update(user.Id, "admin2", null, PapelUsuario.Admin);

        var autenticado = _service.Authenticate("admin2", "senha123");
        Assert.Equal("admin2", autenticado.NomeUsuario);
    }

    [Fact]
    public void Update_WithNewPassword_InvalidatesOldPassword()
    {
        var user = _service.Add("admin", "senha123", PapelUsuario.Admin);

        _service.Update(user.Id, "admin", "novasenha", PapelUsuario.Admin);

        Assert.Throws<InvalidOperationException>(() => _service.Authenticate("admin", "senha123"));
        var autenticado = _service.Authenticate("admin", "novasenha");
        Assert.Equal("admin", autenticado.NomeUsuario);
    }

    [Fact]
    public void Delete_RemovesUserWhenNotLastAdmin()
    {
        _service.Add("admin1", "senha123", PapelUsuario.Admin);
        var admin2 = _service.Add("admin2", "senha456", PapelUsuario.Admin);

        _service.Delete(admin2.Id);

        Assert.Single(_service.GetAll());
    }

    [Fact]
    public void Delete_ThrowsWhenDeletingLastAdmin()
    {
        var admin = _service.Add("admin", "senha123", PapelUsuario.Admin);

        Assert.Throws<InvalidOperationException>(() => _service.Delete(admin.Id));
    }

    [Fact]
    public void Delete_RemovesVendedorFreely()
    {
        _service.Add("admin", "senha123", PapelUsuario.Admin);
        var vendedor = _service.Add("vendedor1", "senha456", PapelUsuario.Vendedor);

        _service.Delete(vendedor.Id);

        Assert.Single(_service.GetAll());
    }
}
