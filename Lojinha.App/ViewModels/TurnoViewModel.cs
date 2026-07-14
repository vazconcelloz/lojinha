using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lojinha.App.Services;
using Lojinha.Data.Models;
using Lojinha.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace Lojinha.App.ViewModels;

public partial class TurnoViewModel : ObservableObject
{
    private readonly CaixaService _caixaService;
    private readonly SessionService _session;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISnackbarService _snackbar;

    public ObservableCollection<MovimentoCaixa> Movimentos { get; } = new();
    public TipoMovimentoCaixa[] TiposMovimento { get; } = Enum.GetValues<TipoMovimentoCaixa>();

    [ObservableProperty]
    private CaixaSessao? sessaoAtual;

    [ObservableProperty]
    private decimal valorAberturaEntrada;

    [ObservableProperty]
    private decimal valorContadoEntrada;

    [ObservableProperty]
    private decimal valorMovimentoEntrada;

    [ObservableProperty]
    private TipoMovimentoCaixa tipoMovimentoSelecionado;

    public bool SessaoAberta => SessaoAtual is not null;

    public TurnoViewModel(CaixaService caixaService, SessionService session, IAuthorizationService authorizationService, ISnackbarService snackbar)
    {
        _caixaService = caixaService;
        _session = session;
        _authorizationService = authorizationService;
        _snackbar = snackbar;
        Carregar();
    }

    public void Refresh()
    {
        Carregar();
    }

    private void Carregar()
    {
        SessaoAtual = _caixaService.GetSessaoAberta();
        Movimentos.Clear();
        if (SessaoAtual is not null)
        {
            foreach (var movimento in _caixaService.GetMovimentos(SessaoAtual.Id))
            {
                Movimentos.Add(movimento);
            }
        }
    }

    partial void OnSessaoAtualChanged(CaixaSessao? value)
    {
        OnPropertyChanged(nameof(SessaoAberta));
    }

    [RelayCommand]
    private void AbrirCaixa()
    {
        try
        {
            _caixaService.AbrirCaixa(ValorAberturaEntrada, _session.CurrentUser?.NomeUsuario ?? string.Empty);
            ValorAberturaEntrada = 0;
            Carregar();
            _snackbar.Show("Sucesso", "Caixa aberto.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private void RegistrarMovimento()
    {
        string? autorizadoPor;

        if (_session.CurrentUser?.Papel == PapelUsuario.Admin)
        {
            autorizadoPor = _session.CurrentUser.NomeUsuario;
        }
        else
        {
            autorizadoPor = _authorizationService.AutorizarDesconto();
            if (autorizadoPor is null)
            {
                _snackbar.Show("Erro", "Movimento não autorizado.", ControlAppearance.Danger);
                return;
            }
        }

        try
        {
            _caixaService.RegistrarMovimento(TipoMovimentoSelecionado, ValorMovimentoEntrada, autorizadoPor, null);
            ValorMovimentoEntrada = 0;
            Carregar();
            _snackbar.Show("Sucesso", "Movimento registrado.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private void FecharCaixa()
    {
        try
        {
            var sessao = _caixaService.FecharCaixa(ValorContadoEntrada, _session.CurrentUser?.NomeUsuario ?? string.Empty);
            ValorContadoEntrada = 0;
            Carregar();
            _snackbar.Show("Sucesso", $"Caixa fechado. Diferença: {sessao.Diferenca:C}", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }
}
