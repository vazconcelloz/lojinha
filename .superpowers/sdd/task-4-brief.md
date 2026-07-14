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

