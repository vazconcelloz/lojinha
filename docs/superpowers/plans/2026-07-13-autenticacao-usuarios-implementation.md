# Autenticação/Usuários Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add mandatory login to Lojinha with two roles (Admin/Vendedor) controlling access, plus an Admin-only Usuários management screen.

**Architecture:** A new `User`/`PapelUsuario` model and `UserService` (PBKDF2 password hashing, no new dependency) back a modal `LoginWindow` shown before `MainWindow`. A singleton `SessionService` holds the logged-in user for the app's lifetime; role-based UI gating happens at `MainWindow` construction (nav item visibility) and inside `SalesViewModel`/`StockViewModel` (button visibility). Logging out closes `MainWindow`, clears the session, and re-shows `LoginWindow` — no new DI scope, no app restart.

**Tech Stack:** .NET 8, WPF, WPF-UI 4.3.0, CommunityToolkit.Mvvm, EF Core 8 + SQLite, xUnit, `System.Security.Cryptography` (PBKDF2, built-in).

## Global Constraints

- Passwords are never stored or logged in plain text. Hashing: `Rfc2898DeriveBytes.Pbkdf2` (SHA256, 100,000 iterations, 16-byte random salt, 32-byte hash), both salt and hash stored as `byte[]` columns on `User`.
- `Authenticate` compares hashes with `CryptographicOperations.FixedTimeEquals` (not `==`), and its failure message is the same generic string ("Usuário ou senha inválidos.") whether the username or the password was wrong — never reveal which one.
- Every service method that fails throws `InvalidOperationException`/`ArgumentException` with a Portuguese, user-facing message, consistent with existing services.
- `UserService.Delete` never allows removing the last remaining Admin.
- WPF-UI `SymbolIcon` names used below (`Person24`, `SignOut24`, plus already-used `Edit24`/`Save24`/`Dismiss24`/`Delete24`/`Add24`) are verified against the actually-installed WPF-UI 4.3.0 package.
- `MainWindow.xaml.cs` has three tag-based switches (`NavigationViewItem_OnClick`'s `IsActive` assignments, `NavigateTo`'s view switch, `RefreshViewModel`'s refresh switch) — adding "usuarios" requires updating all three, same as every prior new screen.
- Every screen's ViewModel exposes public `Refresh()`, called on navigation-in — this is what fixed a real cross-screen staleness bug in a prior module. Any new role-dependent computed property (`PodeCancelarVenda`, `PodeGerenciarEstoque`) must have its `Refresh()` explicitly re-raise `PropertyChanged` for that property too — `SalesViewModel`/`StockViewModel` are DI-scoped and get **reused across a logout/login cycle** (the DI scope is never recreated, only `MainWindow` itself is a fresh instance each login), so a role change without this explicit re-raise would leave stale button visibility until some unrelated property happened to change.
- No automated UI tests in this plan (per spec, and consistent with the rest of the project) — frontend tasks are verified by `dotnet build` + a manual smoke run.
- All new/changed UI copy is in Portuguese.

---

### Task 1: `User`/`PapelUsuario` models, `DbContext` wiring, `Sale.UsuarioNome`, migration

**Files:**
- Create: `Lojinha.Data/Models/PapelUsuario.cs`
- Create: `Lojinha.Data/Models/User.cs`
- Modify: `Lojinha.Data/LojinhaDbContext.cs`
- Modify: `Lojinha.Data/Models/Sale.cs`
- Migration: generated under `Lojinha.Data/Migrations/`

**Interfaces:**
- Produces: `PapelUsuario` enum (`Admin`, `Vendedor`); `User` (`Id`, `NomeUsuario`, `SenhaHash`, `SenhaSalt`, `Papel`); `LojinhaDbContext.Users` `DbSet`; `Sale.UsuarioNome` (`string?`) — consumed by Task 2 (`UserService`) and Task 7 (`SalesService`).

- [ ] **Step 1: Create the `PapelUsuario` enum**

Create `Lojinha.Data/Models/PapelUsuario.cs`:

```csharp
namespace Lojinha.Data.Models;

public enum PapelUsuario
{
    Admin,
    Vendedor
}
```

- [ ] **Step 2: Create the `User` model**

Create `Lojinha.Data/Models/User.cs`:

```csharp
namespace Lojinha.Data.Models;

public class User
{
    public int Id { get; set; }
    public required string NomeUsuario { get; set; }
    public required byte[] SenhaHash { get; set; }
    public required byte[] SenhaSalt { get; set; }
    public PapelUsuario Papel { get; set; }
}
```

- [ ] **Step 3: Add `Sale.UsuarioNome`**

In `Lojinha.Data/Models/Sale.cs`, add this property after `DataCancelamento`:

```csharp
    public string? UsuarioNome { get; set; }
```

- [ ] **Step 4: Wire `User` into `LojinhaDbContext`**

In `Lojinha.Data/LojinhaDbContext.cs`, add a `DbSet` after `SaleItems`:

```csharp
    public DbSet<User> Users => Set<User>();
```

Then, inside `OnModelCreating`, after the existing `SaleItem`/`Product` relationship configuration (before the closing brace of the method), add:

```csharp
        modelBuilder.Entity<User>()
            .HasIndex(u => u.NomeUsuario)
            .IsUnique();
```

- [ ] **Step 5: Generate the EF Core migration**

Run: `dotnet ef migrations add AddUsers --project Lojinha.Data`
Expected: no errors; two new files appear under `Lojinha.Data/Migrations/` (a new `..._AddUsers.cs` migration + matching `.Designer.cs`), and `Lojinha.Data/Migrations/LojinhaDbContextModelSnapshot.cs` is updated to include the `Users` table and `Sale.UsuarioNome` column.

- [ ] **Step 6: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 7: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 42 tests total (unchanged — this task adds no tests, just schema).

- [ ] **Step 8: Commit**

```bash
git add Lojinha.Data/Models/PapelUsuario.cs Lojinha.Data/Models/User.cs Lojinha.Data/Models/Sale.cs Lojinha.Data/LojinhaDbContext.cs Lojinha.Data/Migrations
git commit -m "feat: add User/PapelUsuario models, Sale.UsuarioNome, and migration"
```

---

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
Expected: PASS, 11 tests total for this class.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 53 tests total.

- [ ] **Step 6: Commit**

```bash
git add Lojinha.Services/UserService.cs Lojinha.Services.Tests/UserServiceTests.cs
git commit -m "feat: add UserService (PBKDF2 password hashing, last-admin guard)"
```

---

### Task 3: `SessionService`

**Files:**
- Create: `Lojinha.App/Services/SessionService.cs`
- Modify: `Lojinha.App/App.xaml.cs`

**Interfaces:**
- Produces: `SessionService.CurrentUser` (`User?`, settable) — registered as a DI singleton, consumed by Task 4 (`LoginWindow`), Task 6 (`MainWindow` role gating), Task 7 (`SalesViewModel`), Task 8 (`StockViewModel`).

- [ ] **Step 1: Create `SessionService`**

Create `Lojinha.App/Services/SessionService.cs`:

```csharp
using Lojinha.Data.Models;

namespace Lojinha.App.Services;

public class SessionService
{
    public User? CurrentUser { get; set; }
}
```

- [ ] **Step 2: Register it in DI**

In `Lojinha.App/App.xaml.cs`, add `using Lojinha.App.Services;` to the usings, then in `ConfigureServices`, add this line right after `services.AddSingleton<IContentDialogService, ContentDialogService>();`:

```csharp
        services.AddSingleton<SessionService>();
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 4: Commit**

```bash
git add Lojinha.App/Services/SessionService.cs Lojinha.App/App.xaml.cs
git commit -m "feat: add SessionService"
```

---

### Task 4: `LoginWindow` + startup flow

**Files:**
- Create: `Lojinha.App/ViewModels/LoginViewModel.cs`
- Create: `Lojinha.App/LoginWindow.xaml`
- Create: `Lojinha.App/LoginWindow.xaml.cs`
- Modify: `Lojinha.App/App.xaml.cs`

**Interfaces:**
- Consumes: `UserService.AnyUsers()`/`Add()`/`Authenticate()` (Task 2), `SessionService` (Task 3), `BooleanToVisibilityConverter` resource (existing, already fixed to honor `Invert`).
- Produces: `LoginViewModel.PrimeiroAcesso` (`bool`), `LoginViewModel.EntrarCommand`, `LoginViewModel.LoginBemSucedido` (`event EventHandler?`) — consumed by `LoginWindow.xaml.cs`. `App.MostrarLoginEEntrar()` (private method) — the startup/logout re-entry point, consumed by Task 6's "Sair" wiring.

- [ ] **Step 1: Create `LoginViewModel`**

Create `Lojinha.App/ViewModels/LoginViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lojinha.App.Services;
using Lojinha.Data.Models;
using Lojinha.Services;

namespace Lojinha.App.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly UserService _userService;
    private readonly SessionService _session;

    public bool PrimeiroAcesso { get; }

    [ObservableProperty]
    private string nomeUsuario = string.Empty;

    [ObservableProperty]
    private string senha = string.Empty;

    [ObservableProperty]
    private string mensagemErro = string.Empty;

    public event EventHandler? LoginBemSucedido;

    public LoginViewModel(UserService userService, SessionService session)
    {
        _userService = userService;
        _session = session;
        PrimeiroAcesso = !_userService.AnyUsers();
    }

    [RelayCommand]
    private void Entrar()
    {
        MensagemErro = string.Empty;

        try
        {
            var usuario = PrimeiroAcesso
                ? _userService.Add(NomeUsuario, Senha, PapelUsuario.Admin)
                : _userService.Authenticate(NomeUsuario, Senha);

            _session.CurrentUser = usuario;
            LoginBemSucedido?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MensagemErro = ex.Message;
        }
    }
}
```

- [ ] **Step 2: Create `LoginWindow.xaml`**

Create `Lojinha.App/LoginWindow.xaml`:

```xml
<ui:FluentWindow x:Class="Lojinha.App.LoginWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
        mc:Ignorable="d"
        Title="Lojinha" Height="360" Width="380"
        WindowStartupLocation="CenterScreen"
        ResizeMode="NoResize"
        ExtendsContentIntoTitleBar="True"
        WindowBackdropType="None">
    <Grid Margin="24">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <ui:TitleBar Grid.Row="0" Title="Lojinha" />

        <StackPanel Grid.Row="1" VerticalAlignment="Center">
            <TextBlock Text="Criar primeiro administrador"
                       Visibility="{Binding PrimeiroAcesso, Converter={StaticResource BooleanToVisibilityConverter}}"
                       FontWeight="Bold" FontSize="18" Margin="0,0,0,16" HorizontalAlignment="Center" />
            <TextBlock Text="Entrar"
                       Visibility="{Binding PrimeiroAcesso, Converter={StaticResource BooleanToVisibilityConverter}, ConverterParameter=Invert}"
                       FontWeight="Bold" FontSize="18" Margin="0,0,0,16" HorizontalAlignment="Center" />

            <ui:TextBox PlaceholderText="Nome de usuário" Margin="0,0,0,8"
                        Text="{Binding NomeUsuario, UpdateSourceTrigger=PropertyChanged}" />
            <ui:PasswordBox PlaceholderText="Senha" Margin="0,0,0,8"
                            Password="{Binding Senha, UpdateSourceTrigger=PropertyChanged}" />

            <TextBlock Text="{Binding MensagemErro}" Foreground="Red" Margin="0,0,0,8" TextWrapping="Wrap" />

            <ui:Button Content="Criar administrador" Appearance="Primary" HorizontalAlignment="Stretch"
                       Visibility="{Binding PrimeiroAcesso, Converter={StaticResource BooleanToVisibilityConverter}}"
                       Command="{Binding EntrarCommand}" />
            <ui:Button Content="Entrar" Appearance="Primary" HorizontalAlignment="Stretch"
                       Visibility="{Binding PrimeiroAcesso, Converter={StaticResource BooleanToVisibilityConverter}, ConverterParameter=Invert}"
                       Command="{Binding EntrarCommand}" />
        </StackPanel>
    </Grid>
</ui:FluentWindow>
```

- [ ] **Step 3: Create `LoginWindow.xaml.cs`**

Create `Lojinha.App/LoginWindow.xaml.cs`:

```csharp
using Lojinha.App.ViewModels;
using Wpf.Ui.Controls;

namespace Lojinha.App;

public partial class LoginWindow : FluentWindow
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.LoginBemSucedido += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }
}
```

Subscribing to `LoginBemSucedido` in code-behind (rather than pure XAML binding) is a deliberate, minimal exception — there is no XAML-only way to close a `Window` with a `DialogResult` in response to a ViewModel event, and `MainWindow.xaml.cs` already establishes this app's precedent of small, pragmatic code-behind in top-level windows (as opposed to `UserControl` views, which stay code-behind-free).

- [ ] **Step 4: Wire the login flow into `App.xaml.cs`**

Replace the full contents of `Lojinha.App/App.xaml.cs` with:

```csharp
using System.IO;
using System.Windows;
using Lojinha.App.Services;
using Lojinha.App.ViewModels;
using Lojinha.Data;
using Lojinha.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;

namespace Lojinha.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private IServiceScope? _scope;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        _scope = _serviceProvider.CreateScope();

        var context = _scope.ServiceProvider.GetRequiredService<LojinhaDbContext>();
        context.Database.Migrate();

        MostrarLoginEEntrar();
    }

    private void MostrarLoginEEntrar()
    {
        var loginWindow = _scope!.ServiceProvider.GetRequiredService<LoginWindow>();
        var loginOk = loginWindow.ShowDialog();

        if (loginOk != true)
        {
            Shutdown();
            return;
        }

        var mainWindow = _scope.ServiceProvider.GetRequiredService<MainWindow>();
        Current.MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _scope?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lojinha.db");

        services.AddDbContext<LojinhaDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddSingleton<IContentDialogService, ContentDialogService>();
        services.AddSingleton<SessionService>();

        services.AddScoped<CategoryService>();
        services.AddScoped<SupplierService>();
        services.AddScoped<ProductService>();
        services.AddScoped<StockService>();
        services.AddScoped<SalesService>();
        services.AddScoped<UserService>();

        services.AddScoped<CategoryViewModel>();
        services.AddScoped<SupplierViewModel>();
        services.AddScoped<ProductViewModel>();
        services.AddScoped<StockViewModel>();
        services.AddScoped<SalesViewModel>();
        services.AddScoped<MainViewModel>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
    }
}
```

Note: `UserViewModel` is intentionally NOT registered yet — that's Task 5. This step only wires the login flow; `MostrarLoginEEntrar` does not yet have "Sair" support (that's Task 6) — for now it's called exactly once, at startup.

- [ ] **Step 5: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 53 tests total (unchanged — this task is UI-only).

- [ ] **Step 7: Manual smoke check**

Run: `dotnet run --project Lojinha.App`
Expected: since there are no users yet in this fresh database, the window titled "Lojinha" opens showing "Criar primeiro administrador" with Nome de usuário/Senha fields and a "Criar administrador" button (no "Entrar" text/button visible). Fill in a username and password and click it — `MainWindow` opens normally with the existing sidebar. Close everything, run again — this time the window shows "Entrar" (login mode, not first-access mode) since a user now exists; log in with the same credentials and confirm `MainWindow` opens again. Try wrong credentials once and confirm the red error message appears without closing the window.

- [ ] **Step 8: Commit**

```bash
git add Lojinha.App/ViewModels/LoginViewModel.cs Lojinha.App/LoginWindow.xaml Lojinha.App/LoginWindow.xaml.cs Lojinha.App/App.xaml.cs
git commit -m "feat: add login window and first-access admin creation flow"
```

---

### Task 5: Usuários screen

**Files:**
- Create: `Lojinha.App/ViewModels/UserViewModel.cs`
- Create: `Lojinha.App/Views/UsuarioView.xaml`
- Create: `Lojinha.App/Views/UsuarioView.xaml.cs`
- Modify: `Lojinha.App/ViewModels/MainViewModel.cs`
- Modify: `Lojinha.App/App.xaml.cs`
- Modify: `Lojinha.App/MainWindow.xaml`
- Modify: `Lojinha.App/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `UserService.Add`/`Update`/`Delete`/`GetAll` (Task 2), `ISnackbarService`/`IContentDialogService` (existing), `CountToVisibilityConverter`/`BooleanToVisibilityConverter` (existing).
- Produces: `UserViewModel` with `Usuarios`, `Papeis`, `NomeUsuario`, `Senha`, `PapelSelecionado`, `EditandoId`/`EmEdicao`, `AdicionarCommand`/`EditarCommand`/`SalvarCommand`/`CancelarCommand`/`ExcluirCommand`, public `Refresh()`. `MainViewModel.Usuarios` property (type `UserViewModel`). This screen is **not yet role-restricted** — Task 6 adds the Admin-only nav gating.

- [ ] **Step 1: Create `UserViewModel`**

Create `Lojinha.App/ViewModels/UserViewModel.cs`:

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

public partial class UserViewModel : ObservableObject
{
    private readonly UserService _service;
    private readonly ISnackbarService _snackbar;
    private readonly IContentDialogService _dialogService;

    public ObservableCollection<User> Usuarios { get; } = new();
    public PapelUsuario[] Papeis { get; } = Enum.GetValues<PapelUsuario>();

    [ObservableProperty]
    private string nomeUsuario = string.Empty;

    [ObservableProperty]
    private string senha = string.Empty;

    [ObservableProperty]
    private PapelUsuario papelSelecionado;

    [ObservableProperty]
    private int? editandoId;

    public bool EmEdicao => EditandoId is not null;

    public UserViewModel(UserService service, ISnackbarService snackbar, IContentDialogService dialogService)
    {
        _service = service;
        _snackbar = snackbar;
        _dialogService = dialogService;
        Carregar();
    }

    public void Refresh()
    {
        Carregar();
    }

    private void Carregar()
    {
        Usuarios.Clear();
        foreach (var usuario in _service.GetAll())
        {
            Usuarios.Add(usuario);
        }
    }

    partial void OnEditandoIdChanged(int? value)
    {
        OnPropertyChanged(nameof(EmEdicao));
    }

    [RelayCommand]
    private void Adicionar()
    {
        try
        {
            _service.Add(NomeUsuario, Senha, PapelSelecionado);
            LimparFormulario();
            Carregar();
            _snackbar.Show("Sucesso", "Usuário adicionado.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private void Editar(User usuario)
    {
        EditandoId = usuario.Id;
        NomeUsuario = usuario.NomeUsuario;
        Senha = string.Empty;
        PapelSelecionado = usuario.Papel;
    }

    [RelayCommand]
    private void Salvar()
    {
        if (EditandoId is null)
        {
            return;
        }

        try
        {
            _service.Update(EditandoId.Value, NomeUsuario, string.IsNullOrEmpty(Senha) ? null : Senha, PapelSelecionado);
            EditandoId = null;
            LimparFormulario();
            Carregar();
            _snackbar.Show("Sucesso", "Usuário atualizado.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private void Cancelar()
    {
        EditandoId = null;
        LimparFormulario();
    }

    private void LimparFormulario()
    {
        NomeUsuario = string.Empty;
        Senha = string.Empty;
    }

    [RelayCommand]
    private async Task Excluir(User usuario)
    {
        var options = new SimpleContentDialogCreateOptions
        {
            Title = "Excluir usuário",
            Content = $"Tem certeza que deseja excluir '{usuario.NomeUsuario}'?",
            PrimaryButtonText = "Excluir",
            CloseButtonText = "Cancelar",
        };

        var result = await _dialogService.ShowSimpleDialogAsync(options, CancellationToken.None);
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            _service.Delete(usuario.Id);
            Carregar();
            _snackbar.Show("Sucesso", $"Usuário '{usuario.NomeUsuario}' excluído.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }
}
```

`Senha` is deliberately cleared (not populated) by `Editar` — leaving it empty and saving means "keep the current password" (see `Salvar`'s `string.IsNullOrEmpty(Senha) ? null : Senha`), matching `UserService.Update`'s optional-password contract from Task 2.

- [ ] **Step 2: Create `UsuarioView.xaml`**

Create `Lojinha.App/Views/UsuarioView.xaml`:

```xml
<UserControl x:Class="Lojinha.App.Views.UsuarioView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
             mc:Ignorable="d">
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <ui:Card Grid.Row="0" Margin="0,0,0,16">
            <StackPanel Orientation="Horizontal">
                <ui:TextBox Width="200" PlaceholderText="Nome de usuário"
                            Text="{Binding NomeUsuario, UpdateSourceTrigger=PropertyChanged}" />
                <ui:PasswordBox Width="200" Margin="12,0,0,0" PlaceholderText="Senha"
                                Password="{Binding Senha, UpdateSourceTrigger=PropertyChanged}" />
                <ComboBox Width="130" Margin="12,0,0,0" ItemsSource="{Binding Papeis}"
                          SelectedItem="{Binding PapelSelecionado}" />
                <ui:Button Content="Adicionar" Margin="12,0,0,0" Appearance="Primary"
                           Icon="{ui:SymbolIcon Symbol=Add24}"
                           Visibility="{Binding EmEdicao, Converter={StaticResource BooleanToVisibilityConverter}, ConverterParameter=Invert}"
                           Command="{Binding AdicionarCommand}" />
                <ui:Button Content="Salvar" Margin="12,0,0,0" Appearance="Primary"
                           Icon="{ui:SymbolIcon Symbol=Save24}"
                           Visibility="{Binding EmEdicao, Converter={StaticResource BooleanToVisibilityConverter}}"
                           Command="{Binding SalvarCommand}" />
                <ui:Button Content="Cancelar" Margin="8,0,0,0"
                           Icon="{ui:SymbolIcon Symbol=Dismiss24}"
                           Visibility="{Binding EmEdicao, Converter={StaticResource BooleanToVisibilityConverter}}"
                           Command="{Binding CancelarCommand}" />
            </StackPanel>
        </ui:Card>

        <Grid Grid.Row="1">
            <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center"
                        Visibility="{Binding Usuarios.Count, Converter={StaticResource CountToVisibilityConverter}}">
                <ui:SymbolIcon Symbol="Person24" FontSize="48" HorizontalAlignment="Center" Opacity="0.5" />
                <TextBlock Text="Nenhum usuário cadastrado ainda" Margin="0,8,0,0" Opacity="0.7"
                           HorizontalAlignment="Center" />
            </StackPanel>

            <DataGrid ItemsSource="{Binding Usuarios}" AutoGenerateColumns="False" IsReadOnly="True"
                      Visibility="{Binding Usuarios.Count, Converter={StaticResource CountToVisibilityConverter}, ConverterParameter=Invert}">
                <DataGrid.Columns>
                    <DataGridTextColumn Header="Id" Binding="{Binding Id}" Width="60" />
                    <DataGridTextColumn Header="Usuário" Binding="{Binding NomeUsuario}" Width="*" />
                    <DataGridTextColumn Header="Papel" Binding="{Binding Papel}" Width="120" />
                    <DataGridTemplateColumn Header="" Width="110">
                        <DataGridTemplateColumn.CellTemplate>
                            <DataTemplate>
                                <StackPanel Orientation="Horizontal">
                                    <ui:Button Icon="{ui:SymbolIcon Symbol=Edit24}"
                                               Command="{Binding DataContext.EditarCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                               CommandParameter="{Binding}" />
                                    <ui:Button Appearance="Danger" Margin="4,0,0,0" Icon="{ui:SymbolIcon Symbol=Delete24}"
                                               Command="{Binding DataContext.ExcluirCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                               CommandParameter="{Binding}" />
                                </StackPanel>
                            </DataTemplate>
                        </DataGridTemplateColumn.CellTemplate>
                    </DataGridTemplateColumn>
                </DataGrid.Columns>
            </DataGrid>
        </Grid>
    </Grid>
</UserControl>
```

- [ ] **Step 3: Create `UsuarioView.xaml.cs`**

Create `Lojinha.App/Views/UsuarioView.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace Lojinha.App.Views;

public partial class UsuarioView : UserControl
{
    public UsuarioView()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 4: Add `Usuarios` to `MainViewModel`**

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
    public UserViewModel Usuarios { get; }

    public MainViewModel(CategoryViewModel categorias, SupplierViewModel fornecedores, ProductViewModel produtos, StockViewModel estoque, SalesViewModel vendas, UserViewModel usuarios)
    {
        Categorias = categorias;
        Fornecedores = fornecedores;
        Produtos = produtos;
        Estoque = estoque;
        Vendas = vendas;
        Usuarios = usuarios;
    }
}
```

- [ ] **Step 5: Register `UserViewModel` in `App.xaml.cs`**

In `Lojinha.App/App.xaml.cs`, in `ConfigureServices`, add `services.AddScoped<UserViewModel>();` right after `services.AddScoped<SalesViewModel>();`.

- [ ] **Step 6: Add the Usuários nav item to `MainWindow.xaml`**

In `Lojinha.App/MainWindow.xaml`, inside `<ui:NavigationView.MenuItems>`, add a 6th item after the `VendasItem` `NavigationViewItem`, before `</ui:NavigationView.MenuItems>`:

```xml
                <ui:NavigationViewItem x:Name="UsuariosItem" Content="Usuários" TargetPageTag="usuarios"
                                        Click="NavigationViewItem_OnClick">
                    <ui:NavigationViewItem.Icon>
                        <ui:SymbolIcon Symbol="Person24" />
                    </ui:NavigationViewItem.Icon>
                </ui:NavigationViewItem>
```

- [ ] **Step 7: Wire Usuários navigation in `MainWindow.xaml.cs`**

In `Lojinha.App/MainWindow.xaml.cs`, make these four changes:

1. Add `using Lojinha.App.Views;` is already present; add a field next to the other five views:

```csharp
    private readonly UsuarioView _usuarioView = new();
```

2. Add the `UsuariosItem.IsActive` line to `NavigationViewItem_OnClick`:

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
        UsuariosItem.IsActive = tag == "usuarios";

        NavigateTo(tag);
    }
```

3. Add the `"usuarios"` case to the `switch` inside `NavigateTo`:

```csharp
        (FrameworkElement view, object dataContext) = tag switch
        {
            "categorias" => ((FrameworkElement)_categoriaView, (object)_viewModel.Categorias),
            "fornecedores" => ((FrameworkElement)_fornecedorView, (object)_viewModel.Fornecedores),
            "produtos" => ((FrameworkElement)_produtoView, (object)_viewModel.Produtos),
            "estoque" => ((FrameworkElement)_estoqueView, (object)_viewModel.Estoque),
            "vendas" => ((FrameworkElement)_vendaView, (object)_viewModel.Vendas),
            "usuarios" => ((FrameworkElement)_usuarioView, (object)_viewModel.Usuarios),
            _ => ((FrameworkElement)_categoriaView, (object)_viewModel.Categorias)
        };
```

4. Add the `"usuarios"` case to the `switch` inside `RefreshViewModel`:

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
            case "usuarios":
                _viewModel.Usuarios.Refresh();
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

- [ ] **Step 9: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 53 tests total.

- [ ] **Step 10: Manual smoke check**

Run: `dotnet run --project Lojinha.App` (log in with the Admin created in Task 4). Expected: a 6th "Usuários" sidebar item appears (visible to everyone for now, that's fixed in Task 6). Clicking it shows the Admin user created during first-access in the grid. Add a second user with Papel "Vendedor"; edit it (leave Senha blank, change only the Papel or name) and confirm the change saved without needing to re-enter the password; try excluding the only Admin (the very first user, if it's the only Admin) and confirm the "não é possível excluir o último administrador" error shows via snackbar.

- [ ] **Step 11: Commit**

```bash
git add Lojinha.App/ViewModels/UserViewModel.cs Lojinha.App/Views/UsuarioView.xaml Lojinha.App/Views/UsuarioView.xaml.cs Lojinha.App/ViewModels/MainViewModel.cs Lojinha.App/App.xaml.cs Lojinha.App/MainWindow.xaml Lojinha.App/MainWindow.xaml.cs
git commit -m "feat: add Usuários screen (create/edit/delete users and roles)"
```

---

### Task 6: `MainWindow` role gating + "Sair"

**Files:**
- Modify: `Lojinha.App/MainWindow.xaml`
- Modify: `Lojinha.App/MainWindow.xaml.cs`
- Modify: `Lojinha.App/App.xaml.cs`

**Interfaces:**
- Consumes: `SessionService.CurrentUser` (Task 3), `App.MostrarLoginEEntrar()` (Task 4, made re-entrant here).
- Produces: `MainWindow.Sair` (`event EventHandler?`) — the app subscribes to this to re-show `LoginWindow` without recreating the DI scope.

- [ ] **Step 1: Add role gating and the "Sair" event to `MainWindow.xaml.cs`**

Replace the full contents of `Lojinha.App/MainWindow.xaml.cs` with:

```csharp
using System.Windows;
using Lojinha.App.Services;
using Lojinha.App.ViewModels;
using Lojinha.App.Views;
using Lojinha.Data.Models;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Lojinha.App;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private readonly CategoriaView _categoriaView = new();
    private readonly FornecedorView _fornecedorView = new();
    private readonly ProdutoView _produtoView = new();
    private readonly EstoqueView _estoqueView = new();
    private readonly VendaView _vendaView = new();
    private readonly UsuarioView _usuarioView = new();

    public event EventHandler? Sair;

    public MainWindow(MainViewModel viewModel, ISnackbarService snackbarService, IContentDialogService contentDialogService, SessionService session)
    {
        InitializeComponent();

        _viewModel = viewModel;
        snackbarService.SetSnackbarPresenter(RootSnackbarPresenter);
        contentDialogService.SetContentPresenter(RootContentDialogPresenter);

        var isAdmin = session.CurrentUser?.Papel == PapelUsuario.Admin;
        if (!isAdmin)
        {
            CategoriasItem.Visibility = Visibility.Collapsed;
            FornecedoresItem.Visibility = Visibility.Collapsed;
            ProdutosItem.Visibility = Visibility.Collapsed;
            UsuariosItem.Visibility = Visibility.Collapsed;
        }

        var tagInicial = isAdmin ? "categorias" : "vendas";
        var itemInicial = isAdmin ? CategoriasItem : VendasItem;
        itemInicial.IsActive = true;
        Loaded += (_, _) => NavigateTo(tagInicial);
    }

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
        UsuariosItem.IsActive = tag == "usuarios";

        NavigateTo(tag);
    }

    private void NavigateTo(string tag)
    {
        (FrameworkElement view, object dataContext) = tag switch
        {
            "categorias" => ((FrameworkElement)_categoriaView, (object)_viewModel.Categorias),
            "fornecedores" => ((FrameworkElement)_fornecedorView, (object)_viewModel.Fornecedores),
            "produtos" => ((FrameworkElement)_produtoView, (object)_viewModel.Produtos),
            "estoque" => ((FrameworkElement)_estoqueView, (object)_viewModel.Estoque),
            "vendas" => ((FrameworkElement)_vendaView, (object)_viewModel.Vendas),
            "usuarios" => ((FrameworkElement)_usuarioView, (object)_viewModel.Usuarios),
            _ => ((FrameworkElement)_categoriaView, (object)_viewModel.Categorias)
        };

        RefreshViewModel(tag);

        view.DataContext = dataContext;
        RootNavigation.ReplaceContent(view, dataContext);
    }

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
            case "usuarios":
                _viewModel.Usuarios.Refresh();
                break;
            default:
                _viewModel.Categorias.Refresh();
                break;
        }
    }

    private void ThemeToggle_OnToggle(object sender, RoutedEventArgs e)
    {
        var theme = ThemeToggle.IsChecked == true ? ApplicationTheme.Dark : ApplicationTheme.Light;
        ApplicationThemeManager.Apply(theme, WindowBackdropType.None, false);
    }

    private void SairButton_OnClick(object sender, RoutedEventArgs e)
    {
        Sair?.Invoke(this, EventArgs.Empty);
    }
}
```

- [ ] **Step 2: Add the "Sair" button to `MainWindow.xaml`**

In `Lojinha.App/MainWindow.xaml`, replace the `<ui:NavigationView.PaneFooter>` block:

```xml
            <ui:NavigationView.PaneFooter>
                <ui:ToggleSwitch x:Name="ThemeToggle" OnContent="Escuro" OffContent="Claro" Margin="12"
                                  Checked="ThemeToggle_OnToggle" Unchecked="ThemeToggle_OnToggle" />
            </ui:NavigationView.PaneFooter>
```

with:

```xml
            <ui:NavigationView.PaneFooter>
                <StackPanel>
                    <ui:ToggleSwitch x:Name="ThemeToggle" OnContent="Escuro" OffContent="Claro" Margin="12,12,12,4"
                                      Checked="ThemeToggle_OnToggle" Unchecked="ThemeToggle_OnToggle" />
                    <ui:Button Content="Sair" Icon="{ui:SymbolIcon Symbol=SignOut24}" Margin="12,4,12,12"
                               HorizontalAlignment="Stretch" Click="SairButton_OnClick" />
                </StackPanel>
            </ui:NavigationView.PaneFooter>
```

- [ ] **Step 3: Make `MostrarLoginEEntrar` re-entrant and wire "Sair" to it**

In `Lojinha.App/App.xaml.cs`, replace the `MostrarLoginEEntrar` method:

```csharp
    private void MostrarLoginEEntrar()
    {
        var loginWindow = _scope!.ServiceProvider.GetRequiredService<LoginWindow>();
        var loginOk = loginWindow.ShowDialog();

        if (loginOk != true)
        {
            Shutdown();
            return;
        }

        var mainWindow = _scope.ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Sair += (_, _) =>
        {
            mainWindow.Close();
            _scope.ServiceProvider.GetRequiredService<SessionService>().CurrentUser = null;
            MostrarLoginEEntrar();
        };
        Current.MainWindow = mainWindow;
        mainWindow.Show();
    }
```

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 53 tests total.

- [ ] **Step 6: Manual smoke check**

Run: `dotnet run --project Lojinha.App`, log in as the Admin created earlier. Expected: sidebar shows all 6 items (Admin sees everything). Click "Sair" — the window closes and the login screen reappears (app doesn't exit). Log back in as the same Admin — confirm the app returns to normal. Then, from the Usuários screen, create a Vendedor account; click "Sair"; log in as that Vendedor. Expected: sidebar shows only "Vendas" and "Estoque" (Categorias/Fornecedores/Produtos/Usuários are gone), and the app lands on the Vendas screen by default (not Categorias, which this user can't see).

- [ ] **Step 7: Commit**

```bash
git add Lojinha.App/MainWindow.xaml Lojinha.App/MainWindow.xaml.cs Lojinha.App/App.xaml.cs
git commit -m "feat: add role-based sidebar gating and Sair (logout without restart)"
```

---

### Task 7: Vendas — track `UsuarioNome`, hide "Cancelar" for Vendedor

**Files:**
- Modify: `Lojinha.Services/SalesService.cs`
- Test: `Lojinha.Services.Tests/SalesServiceTests.cs`
- Modify: `Lojinha.App/ViewModels/SalesViewModel.cs`
- Modify: `Lojinha.App/Views/VendaView.xaml`
- Create: `Lojinha.App/Converters/BooleanAndToVisibilityConverter.cs`
- Modify: `Lojinha.App/App.xaml`

**Interfaces:**
- Consumes: `SessionService.CurrentUser` (Task 3).
- Produces: `SalesService.RegisterSale(..., string? usuarioNome = null)` (new optional parameter, existing call sites unaffected), `SalesViewModel.PodeCancelarVenda` (`bool`, computed), `BooleanAndToVisibilityConverter` (resource key `"BooleanAndToVisibilityConverter"`).

- [ ] **Step 1: Write the failing test for `UsuarioNome` tracking**

Add this `[Fact]` inside `Lojinha.Services.Tests/SalesServiceTests.cs`'s `SalesServiceTests` class, after `GetSaleHistory_OrdersByDataHoraDescending`:

```csharp
    [Fact]
    public void RegisterSale_StoresUsuarioNomeWhenProvided()
    {
        var product = CreateProduct();
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        var sale = _service.RegisterSale(new[] { (product.Id, 1m) }, FormaPagamento.Dinheiro, "vendedor1");

        Assert.Equal("vendedor1", sale.UsuarioNome);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~SalesServiceTests"`
Expected: build error (`RegisterSale` doesn't accept a third argument yet).

- [ ] **Step 3: Add the optional parameter to `SalesService.RegisterSale`**

In `Lojinha.Services/SalesService.cs`, change the `RegisterSale` method signature and the `Sale` object construction:

```csharp
    public Sale RegisterSale(IEnumerable<(int ProductId, decimal Quantidade)> itens, FormaPagamento formaPagamento, string? usuarioNome = null)
```

and inside the method, where `sale` is constructed:

```csharp
        var sale = new Sale
        {
            DataHora = DateTime.Now,
            FormaPagamento = formaPagamento,
            Cancelada = false,
            UsuarioNome = usuarioNome
        };
```

The default value `= null` means the 10 existing calls to `RegisterSale` (two-argument form, in both `SalesServiceTests.cs` and `SalesViewModel.cs`) keep compiling unchanged.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~SalesServiceTests"`
Expected: PASS, 11 tests total for this class.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 53 tests total.

- [ ] **Step 6: Create `BooleanAndToVisibilityConverter`**

Create `Lojinha.App/Converters/BooleanAndToVisibilityConverter.cs`:

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Lojinha.App.Converters;

public class BooleanAndToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = values.All(v => v is bool b && b);
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
```

This combines two independent booleans into one `Visibility` — needed because the "Cancelar" button in Vendas' histórico must be hidden when EITHER the sale is already cancelled (`VendaHistoricoItem.PodeCancelar`, existing) OR the current user isn't an Admin (`SalesViewModel.PodeCancelarVenda`, new below) — two booleans from two different `DataContext`s that a single `Converter`/`ConverterParameter` binding can't combine.

- [ ] **Step 7: Register the converter in `App.xaml`**

In `Lojinha.App/App.xaml`, add this line right after `<BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter" />`:

```xml
            <converters:BooleanAndToVisibilityConverter x:Key="BooleanAndToVisibilityConverter" />
```

- [ ] **Step 8: Update `SalesViewModel`**

In `Lojinha.App/ViewModels/SalesViewModel.cs`:

1. Add `using Lojinha.App.Services;` to the usings.
2. Add a `SessionService _session` field, injected via the constructor:

```csharp
    private readonly SalesService _salesService;
    private readonly ProductService _productService;
    private readonly ISnackbarService _snackbar;
    private readonly IContentDialogService _dialogService;
    private readonly SessionService _session;
```

```csharp
    public SalesViewModel(SalesService salesService, ProductService productService, ISnackbarService snackbar, IContentDialogService dialogService, SessionService session)
    {
        _salesService = salesService;
        _productService = productService;
        _snackbar = snackbar;
        _dialogService = dialogService;
        _session = session;
        Carrinho.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Total));
        CarregarProdutos();
        CarregarHistorico();
    }
```

3. Add the computed property, right after `Total`:

```csharp
    public bool PodeCancelarVenda => _session.CurrentUser?.Papel == PapelUsuario.Admin;
```

4. Update `Refresh()` to re-raise it (needed because this ViewModel is reused across a logout/login cycle — see this plan's Global Constraints):

```csharp
    public void Refresh()
    {
        CarregarProdutos();
        CarregarHistorico();
        OnPropertyChanged(nameof(PodeCancelarVenda));
    }
```

5. Update `FinalizarVenda` to pass the current user's name:

```csharp
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
            _salesService.RegisterSale(itens, FormaPagamentoSelecionada, _session.CurrentUser?.NomeUsuario);
            Carrinho.Clear();
            CarregarHistorico();
            _snackbar.Show("Sucesso", "Venda registrada.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }
```

6. Add `UsuarioNome` to the `VendaHistoricoItem` record and to `CarregarHistorico`'s mapping:

```csharp
public record VendaHistoricoItem(int SaleId, DateTime DataHora, decimal Total, FormaPagamento FormaPagamento, bool Cancelada, string? UsuarioNome)
{
    public string Status => Cancelada ? "Cancelada" : "Concluída";
    public bool PodeCancelar => !Cancelada;
}
```

```csharp
    private void CarregarHistorico()
    {
        Historico.Clear();
        foreach (var venda in _salesService.GetSaleHistory())
        {
            Historico.Add(new VendaHistoricoItem(venda.Id, venda.DataHora, venda.Total, venda.FormaPagamento, venda.Cancelada, venda.UsuarioNome));
        }
    }
```

- [ ] **Step 9: Update `VendaView.xaml`**

In `Lojinha.App/Views/VendaView.xaml`, add a new column to the histórico `DataGrid`, right after the `"Status"` column and before the action `DataGridTemplateColumn`:

```xml
                            <DataGridTextColumn Header="Vendedor" Binding="{Binding UsuarioNome}" Width="120" />
```

Then replace the "Cancelar" button's `Visibility` binding:

```xml
                                        <ui:Button Content="Cancelar" Appearance="Danger" Icon="{ui:SymbolIcon Symbol=Dismiss24}"
                                                   Visibility="{Binding PodeCancelar, Converter={StaticResource BooleanToVisibilityConverter}}"
                                                   Command="{Binding DataContext.CancelarVendaCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                                   CommandParameter="{Binding}" />
```

with:

```xml
                                        <ui:Button Content="Cancelar" Appearance="Danger" Icon="{ui:SymbolIcon Symbol=Dismiss24}"
                                                   Command="{Binding DataContext.CancelarVendaCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                                   CommandParameter="{Binding}">
                                            <ui:Button.Visibility>
                                                <MultiBinding Converter="{StaticResource BooleanAndToVisibilityConverter}">
                                                    <Binding Path="PodeCancelar" />
                                                    <Binding Path="DataContext.PodeCancelarVenda" RelativeSource="{RelativeSource AncestorType=DataGrid}" />
                                                </MultiBinding>
                                            </ui:Button.Visibility>
                                        </ui:Button>
```

- [ ] **Step 10: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 11: Manual smoke check**

Run: `dotnet run --project Lojinha.App`, log in as Admin, register a sale — confirm the histórico's "Vendedor" column shows the Admin's username and the "Cancelar" button is visible (and still respects the already-cancelled-hides-the-button behavior from before). Log out, log in as a Vendedor, register a sale — confirm "Vendedor" shows that account's name, and confirm the "Cancelar" button does NOT appear on any row in the histórico (including the Vendedor's own new sale), since only Admin can cancel.

- [ ] **Step 12: Commit**

```bash
git add Lojinha.Services/SalesService.cs Lojinha.Services.Tests/SalesServiceTests.cs Lojinha.App/ViewModels/SalesViewModel.cs Lojinha.App/Views/VendaView.xaml Lojinha.App/Converters/BooleanAndToVisibilityConverter.cs Lojinha.App/App.xaml
git commit -m "feat: track which user registered each sale, restrict cancel to Admin"
```

---

### Task 8: Estoque — restrict lot entry/delete to Admin

**Files:**
- Modify: `Lojinha.App/ViewModels/StockViewModel.cs`
- Modify: `Lojinha.App/Views/EstoqueView.xaml`

**Interfaces:**
- Consumes: `SessionService.CurrentUser` (Task 3).
- Produces: `StockViewModel.PodeGerenciarEstoque` (`bool`, computed).

- [ ] **Step 1: Update `StockViewModel`**

In `Lojinha.App/ViewModels/StockViewModel.cs`:

1. Add `using Lojinha.App.Services;` to the usings.
2. Add a `SessionService _session` field, injected via the constructor:

```csharp
    private readonly StockService _stockService;
    private readonly ProductService _productService;
    private readonly SupplierService _supplierService;
    private readonly ISnackbarService _snackbar;
    private readonly IContentDialogService _dialogService;
    private readonly SessionService _session;
```

```csharp
    public StockViewModel(StockService stockService, ProductService productService, SupplierService supplierService, ISnackbarService snackbar, IContentDialogService dialogService, SessionService session)
    {
        _stockService = stockService;
        _productService = productService;
        _supplierService = supplierService;
        _snackbar = snackbar;
        _dialogService = dialogService;
        _session = session;
        CarregarCombos();
        AtualizarPaineis();
    }
```

3. Add the computed property, right after the `[ObservableProperty] private DateTime? dataValidade;` block:

```csharp
    public bool PodeGerenciarEstoque => _session.CurrentUser?.Papel == PapelUsuario.Admin;
```

(This requires `using Lojinha.Data.Models;`, already present in this file for `Product`/`Supplier`.)

4. Update `Refresh()` to re-raise it:

```csharp
    public void Refresh()
    {
        CarregarCombos();
        AtualizarPaineis();
        OnPropertyChanged(nameof(PodeGerenciarEstoque));
    }
```

- [ ] **Step 2: Update `EstoqueView.xaml`**

In `Lojinha.App/Views/EstoqueView.xaml`, wrap the "Entrada de lote" `ui:Card` (the first one, containing the lot-entry form) with a visibility binding — replace:

```xml
            <ui:Card Margin="0,0,0,16">
                <StackPanel>
                    <TextBlock Text="Entrada de lote" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />
```

with:

```xml
            <ui:Card Margin="0,0,0,16" Visibility="{Binding PodeGerenciarEstoque, Converter={StaticResource BooleanToVisibilityConverter}}">
                <StackPanel>
                    <TextBlock Text="Entrada de lote" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />
```

Then, on the "Vencimentos" card's delete button (the last `ui:Button` in the file, inside the `Vencimentos` `DataGrid`'s template column), add the same visibility binding — replace:

```xml
                                        <ui:Button Appearance="Danger" Icon="{ui:SymbolIcon Symbol=Delete24}"
                                                   Command="{Binding DataContext.ExcluirLoteCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                                   CommandParameter="{Binding}" />
```

with:

```xml
                                        <ui:Button Appearance="Danger" Icon="{ui:SymbolIcon Symbol=Delete24}"
                                                   Visibility="{Binding DataContext.PodeGerenciarEstoque, RelativeSource={RelativeSource AncestorType=DataGrid}, Converter={StaticResource BooleanToVisibilityConverter}}"
                                                   Command="{Binding DataContext.ExcluirLoteCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                                   CommandParameter="{Binding}" />
```

(Unlike Vendas' "Cancelar" button, this delete button only has one condition to check — no `MultiBinding` needed here.)

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 54 tests total.

- [ ] **Step 5: Manual smoke check**

Run: `dotnet run --project Lojinha.App`, log in as Admin, go to Estoque — confirm "Entrada de lote" card and the Vencimentos delete buttons are visible; add a lot to confirm the form still works. Log out, log in as a Vendedor, go to Estoque — confirm "Entrada de lote" is gone and the delete buttons on Vencimentos rows (if any lots are near/past expiry) don't appear, while "Estoque atual"/"Estoque baixo"/"Vencimentos" tables themselves remain visible (view-only).

- [ ] **Step 6: Commit**

```bash
git add Lojinha.App/ViewModels/StockViewModel.cs Lojinha.App/Views/EstoqueView.xaml
git commit -m "feat: restrict Estoque lot entry/delete to Admin"
```

---

### Task 9: Final integration check

**Files:** none (verification only).

- [ ] **Step 1: Full test suite**

Run: `dotnet test`
Expected: PASS, 54 tests total, 0 failures.

- [ ] **Step 2: Full build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 3: End-to-end manual walkthrough**

Run: `dotnet run --project Lojinha.App` on a **fresh database** (delete `lojinha.db` from the build output directory first, or run from a clean checkout) and, in one session:
1. Confirm the first-access "Criar primeiro administrador" screen appears; create an Admin.
2. As Admin: create a Vendedor user, add a category/fornecedor/product, add stock, register a sale, cancel that sale (confirming stock returns), verify the sale's "Vendedor" column shows the Admin's username.
3. Click "Sair"; log in as the Vendedor. Confirm the sidebar only shows Vendas/Estoque, the app lands on Vendas, Estoque hides "Entrada de lote" and lot-delete buttons, and Vendas' histórico hides every "Cancelar" button (including on the Vendedor's own new sale).
4. Register a sale as the Vendedor; confirm its "Vendedor" column shows the Vendedor's username, not the Admin's.
5. Click "Sair"; log back in as Admin; confirm the Vendedor's new sale now shows a visible "Cancelar" button (role-based visibility correctly refreshed on the reused `SalesViewModel` instance — this is the specific staleness risk called out in this plan's Global Constraints).
6. Try to delete the Admin account from Usuários while it's the only Admin — confirm the "não é possível excluir o último administrador" error.
7. Toggle dark mode and confirm the login screen, Usuários screen, and the "Sair" button all stay legible in both themes.

- [ ] **Step 4: Push**

```bash
git push
```
