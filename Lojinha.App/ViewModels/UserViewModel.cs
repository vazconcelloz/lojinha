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
