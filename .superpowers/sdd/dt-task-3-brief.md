### Task 3: `AutorizacaoWindow` — supervisor-override authorization

**Files:**
- Create: `Lojinha.App/ViewModels/AutorizacaoViewModel.cs`
- Create: `Lojinha.App/AutorizacaoWindow.xaml`
- Create: `Lojinha.App/AutorizacaoWindow.xaml.cs`
- Create: `Lojinha.App/Services/AuthorizationService.cs`
- Modify: `Lojinha.App/App.xaml.cs`

**Interfaces:**
- Consumes: `UserService.Authenticate` (existing), `PapelUsuario.Admin` (existing).
- Produces: `IAuthorizationService.AutorizarDesconto() : string?` — consumed by Task 4 (`SalesViewModel.FinalizarVenda`).

- [ ] **Step 1: Create `AutorizacaoViewModel`**

Create `Lojinha.App/ViewModels/AutorizacaoViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lojinha.Data.Models;
using Lojinha.Services;

namespace Lojinha.App.ViewModels;

public partial class AutorizacaoViewModel : ObservableObject
{
    private readonly UserService _userService;

    [ObservableProperty]
    private string nomeUsuario = string.Empty;

    [ObservableProperty]
    private string senha = string.Empty;

    [ObservableProperty]
    private string mensagemErro = string.Empty;

    public string? NomeAutorizador { get; private set; }

    public event EventHandler? AutorizacaoConcedida;

    public AutorizacaoViewModel(UserService userService)
    {
        _userService = userService;
    }

    [RelayCommand]
    private void Autorizar()
    {
        MensagemErro = string.Empty;

        try
        {
            var usuario = _userService.Authenticate(NomeUsuario, Senha);

            if (usuario.Papel != PapelUsuario.Admin)
            {
                MensagemErro = "Apenas administradores podem autorizar desconto.";
                return;
            }

            NomeAutorizador = usuario.NomeUsuario;
            AutorizacaoConcedida?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MensagemErro = ex.Message;
        }
    }
}
```

- [ ] **Step 2: Create `AutorizacaoWindow.xaml`**

Create `Lojinha.App/AutorizacaoWindow.xaml`:

```xml
<ui:FluentWindow x:Class="Lojinha.App.AutorizacaoWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
        mc:Ignorable="d"
        Title="Lojinha" Height="320" Width="380"
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
            <TextBlock Text="Autorização do administrador"
                       FontWeight="Bold" FontSize="18" Margin="0,0,0,16" HorizontalAlignment="Center" />
            <TextBlock Text="Um desconto foi aplicado e precisa de autorização."
                       TextWrapping="Wrap" Opacity="0.7" Margin="0,0,0,16" HorizontalAlignment="Center" TextAlignment="Center" />

            <ui:TextBox PlaceholderText="Usuário do administrador" Margin="0,0,0,8"
                        Text="{Binding NomeUsuario, UpdateSourceTrigger=PropertyChanged}" />
            <ui:PasswordBox PlaceholderText="Senha" Margin="0,0,0,8"
                            Password="{Binding Senha, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />

            <TextBlock Text="{Binding MensagemErro}" Foreground="Red" Margin="0,0,0,8" TextWrapping="Wrap" />

            <ui:Button Content="Autorizar" Appearance="Primary" HorizontalAlignment="Stretch"
                       Command="{Binding AutorizarCommand}" />
        </StackPanel>
    </Grid>
</ui:FluentWindow>
```

- [ ] **Step 3: Create `AutorizacaoWindow.xaml.cs`**

Create `Lojinha.App/AutorizacaoWindow.xaml.cs`:

```csharp
using Lojinha.App.ViewModels;
using Wpf.Ui.Controls;

namespace Lojinha.App;

public partial class AutorizacaoWindow : FluentWindow
{
    public AutorizacaoWindow(AutorizacaoViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.AutorizacaoConcedida += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }

    public string? NomeAutorizador => (DataContext as AutorizacaoViewModel)?.NomeAutorizador;
}
```

- [ ] **Step 4: Create `AuthorizationService`**

Create `Lojinha.App/Services/AuthorizationService.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Lojinha.App.Services;

public interface IAuthorizationService
{
    string? AutorizarDesconto();
}

public class AuthorizationService : IAuthorizationService
{
    private readonly IServiceProvider _serviceProvider;

    public AuthorizationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public string? AutorizarDesconto()
    {
        var window = _serviceProvider.GetRequiredService<AutorizacaoWindow>();
        var autorizado = window.ShowDialog();
        return autorizado == true ? window.NomeAutorizador : null;
    }
}
```

- [ ] **Step 5: Register the new types in `App.xaml.cs`**

In `Lojinha.App/App.xaml.cs`, replace:

```csharp
        services.AddScoped<UserService>();
```

with:

```csharp
        services.AddScoped<UserService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
```

Then replace:

```csharp
        services.AddTransient<LoginViewModel>();
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
```

with:

```csharp
        services.AddTransient<LoginViewModel>();
        services.AddTransient<LoginWindow>();
        services.AddTransient<AutorizacaoViewModel>();
        services.AddTransient<AutorizacaoWindow>();
        services.AddTransient<MainWindow>();
```

- [ ] **Step 6: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 7: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 62 tests total (unchanged — no automated tests for this UI-only task, matching this project's convention for `LoginWindow`/`UsuarioView`).

- [ ] **Step 8: Manual smoke check**

Run: `dotnet run --project Lojinha.App` in the background, confirm the process starts and stays alive (`tasklist //FI "IMAGENAME eq Lojinha.App.exe"`), then terminate it (`taskkill //F //IM Lojinha.App.exe`). `AutorizacaoWindow` isn't reachable from the UI yet (Task 4 wires the trigger) — this step only confirms the DI registrations don't break app startup.

- [ ] **Step 9: Commit**

```bash
git add Lojinha.App/ViewModels/AutorizacaoViewModel.cs Lojinha.App/AutorizacaoWindow.xaml Lojinha.App/AutorizacaoWindow.xaml.cs Lojinha.App/Services/AuthorizationService.cs Lojinha.App/App.xaml.cs
git commit -m "feat: add AutorizacaoWindow for supervisor-override discount authorization"
```

---

