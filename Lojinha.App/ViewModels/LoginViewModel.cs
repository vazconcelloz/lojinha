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
