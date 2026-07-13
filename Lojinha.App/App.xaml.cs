using System.IO;
using System.Windows;
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

        var mainWindow = _scope.ServiceProvider.GetRequiredService<MainWindow>();
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

        services.AddScoped<CategoryService>();
        services.AddScoped<SupplierService>();
        services.AddScoped<ProductService>();
        services.AddScoped<StockService>();
        services.AddScoped<SalesService>();

        services.AddScoped<CategoryViewModel>();
        services.AddScoped<SupplierViewModel>();
        services.AddScoped<ProductViewModel>();
        services.AddScoped<StockViewModel>();
        services.AddScoped<SalesViewModel>();
        services.AddScoped<MainViewModel>();

        services.AddTransient<MainWindow>();
    }
}
