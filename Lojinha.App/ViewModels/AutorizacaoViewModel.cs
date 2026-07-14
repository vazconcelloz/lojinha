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
