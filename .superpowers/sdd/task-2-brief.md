### Task 2: `UserService`

**Files:**
- Create: `Lojinha.Services/UserService.cs`
- Test: `Lojinha.Services.Tests/UserServiceTests.cs`

**Interfaces:**
- Produces: `UserService.Add(string nomeUsuario, string senha, PapelUsuario papel) : User`, `UserService.Update(int id, string nomeUsuario, string? novaSenha, PapelUsuario papel)`, `UserService.Delete(int id)`, `UserService.GetAll() : IEnumerable<User>`, `UserService.AnyUsers() : bool`, `UserService.Authenticate(string nomeUsuario, string senha) : User` — consumed by Task 3 onward.

- [ ] **Step 1: Write the failing tests**

Create `Lojinha.Services.Tests/UserServiceTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~UserServiceTests"`
Expected: build error (`UserService` doesn't exist yet).

- [ ] **Step 3: Implement `UserService`**

Create `Lojinha.Services/UserService.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Lojinha.Data;
using Lojinha.Data.Models;

namespace Lojinha.Services;

public class UserService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    private readonly LojinhaDbContext _context;

    public UserService(LojinhaDbContext context)
    {
        _context = context;
    }

    public bool AnyUsers()
    {
        return _context.Users.Any();
    }

    public User Add(string nomeUsuario, string senha, PapelUsuario papel)
    {
        if (string.IsNullOrWhiteSpace(nomeUsuario))
        {
            throw new ArgumentException("Nome de usuário é obrigatório.", nameof(nomeUsuario));
        }

        if (string.IsNullOrWhiteSpace(senha))
        {
            throw new ArgumentException("Senha é obrigatória.", nameof(senha));
        }

        if (_context.Users.Any(u => u.NomeUsuario == nomeUsuario))
        {
            throw new InvalidOperationException($"Já existe um usuário com o nome '{nomeUsuario}'.");
        }

        var (hash, salt) = HashSenha(senha);

        var user = new User
        {
            NomeUsuario = nomeUsuario,
            SenhaHash = hash,
            SenhaSalt = salt,
            Papel = papel
        };

        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    public void Update(int id, string nomeUsuario, string? novaSenha, PapelUsuario papel)
    {
        if (string.IsNullOrWhiteSpace(nomeUsuario))
        {
            throw new ArgumentException("Nome de usuário é obrigatório.", nameof(nomeUsuario));
        }

        if (_context.Users.Any(u => u.NomeUsuario == nomeUsuario && u.Id != id))
        {
            throw new InvalidOperationException($"Já existe um usuário com o nome '{nomeUsuario}'.");
        }

        var user = _context.Users.Find(id);
        if (user is null)
        {
            throw new InvalidOperationException("Usuário não encontrado.");
        }

        user.NomeUsuario = nomeUsuario;
        user.Papel = papel;

        if (!string.IsNullOrWhiteSpace(novaSenha))
        {
            var (hash, salt) = HashSenha(novaSenha);
            user.SenhaHash = hash;
            user.SenhaSalt = salt;
        }

        _context.SaveChanges();
    }

    public void Delete(int id)
    {
        var user = _context.Users.Find(id);
        if (user is null)
        {
            throw new InvalidOperationException("Usuário não encontrado.");
        }

        if (user.Papel == PapelUsuario.Admin && _context.Users.Count(u => u.Papel == PapelUsuario.Admin) <= 1)
        {
            throw new InvalidOperationException("Não é possível excluir o último administrador.");
        }

        _context.Users.Remove(user);
        _context.SaveChanges();
    }

    public IEnumerable<User> GetAll()
    {
        return _context.Users.ToList();
    }

    public User Authenticate(string nomeUsuario, string senha)
    {
        var user = _context.Users.FirstOrDefault(u => u.NomeUsuario == nomeUsuario);
        if (user is null)
        {
            throw new InvalidOperationException("Usuário ou senha inválidos.");
        }

        var hashCalculado = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(senha), user.SenhaSalt, Iterations, HashAlgorithmName.SHA256, HashSize);

        if (!CryptographicOperations.FixedTimeEquals(hashCalculado, user.SenhaHash))
        {
            throw new InvalidOperationException("Usuário ou senha inválidos.");
        }

        return user;
    }

    private static (byte[] Hash, byte[] Salt) HashSenha(string senha)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(senha), salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return (hash, salt);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~UserServiceTests"`
Expected: PASS, 10 tests total for this class.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 52 tests total.

- [ ] **Step 6: Commit**

```bash
git add Lojinha.Services/UserService.cs Lojinha.Services.Tests/UserServiceTests.cs
git commit -m "feat: add UserService (PBKDF2 password hashing, last-admin guard)"
```

---

