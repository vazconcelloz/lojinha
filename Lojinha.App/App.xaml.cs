using System.IO;
using System.Windows;
using System.Windows.Media;
using Lojinha.App.Services;
using Lojinha.App.ViewModels;
using Lojinha.Data;
using Lojinha.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
using Wpf.Ui.Appearance;

namespace Lojinha.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private IServiceScope? _scope;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        ApplicationAccentColorManager.Apply(Color.FromRgb(0xD7, 0x00, 0x00), ApplicationTheme.Light);

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
        var sairClicked = false;
        mainWindow.Sair += (_, _) =>
        {
            sairClicked = true;
            mainWindow.Close();
            _scope.ServiceProvider.GetRequiredService<SessionService>().CurrentUser = null;
            MostrarLoginEEntrar();
        };
        mainWindow.Closed += (_, _) =>
        {
            if (!sairClicked)
            {
                Shutdown();
            }
        };
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
        services.AddScoped<CaixaService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();

        services.AddScoped<CategoryViewModel>();
        services.AddScoped<SupplierViewModel>();
        services.AddScoped<ProductViewModel>();
        services.AddScoped<StockViewModel>();
        services.AddScoped<TurnoViewModel>();
        services.AddScoped<SalesViewModel>();
        services.AddScoped<UserViewModel>();
        services.AddScoped<MainViewModel>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<LoginWindow>();
        services.AddTransient<AutorizacaoViewModel>();
        services.AddTransient<AutorizacaoWindow>();
        services.AddTransient<MainWindow>();
    }
}
