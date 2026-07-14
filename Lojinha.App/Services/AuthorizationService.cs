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
