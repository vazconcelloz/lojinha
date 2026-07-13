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

public partial class SupplierViewModel : ObservableObject
{
    private readonly SupplierService _service;
    private readonly ISnackbarService _snackbar;
    private readonly IContentDialogService _dialogService;

    public ObservableCollection<Supplier> Fornecedores { get; } = new();

    [ObservableProperty]
    private string novoNome = string.Empty;

    [ObservableProperty]
    private string? novoContato;

    [ObservableProperty]
    private int? editandoId;

    public bool EmEdicao => EditandoId is not null;

    public SupplierViewModel(SupplierService service, ISnackbarService snackbar, IContentDialogService dialogService)
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
        Fornecedores.Clear();
        foreach (var fornecedor in _service.GetAll())
        {
            Fornecedores.Add(fornecedor);
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
            _service.Add(NovoNome, NovoContato);
            NovoNome = string.Empty;
            NovoContato = null;
            Carregar();
            _snackbar.Show("Sucesso", "Fornecedor adicionado.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private void Editar(Supplier fornecedor)
    {
        EditandoId = fornecedor.Id;
        NovoNome = fornecedor.Nome;
        NovoContato = fornecedor.Contato;
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
            _service.Update(EditandoId.Value, NovoNome, NovoContato);
            NovoNome = string.Empty;
            NovoContato = null;
            EditandoId = null;
            Carregar();
            _snackbar.Show("Sucesso", "Fornecedor atualizado.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private void Cancelar()
    {
        NovoNome = string.Empty;
        NovoContato = null;
        EditandoId = null;
    }

    [RelayCommand]
    private async Task Excluir(Supplier fornecedor)
    {
        var options = new SimpleContentDialogCreateOptions
        {
            Title = "Excluir fornecedor",
            Content = $"Tem certeza que deseja excluir '{fornecedor.Nome}'?",
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
            _service.Delete(fornecedor.Id);
            Carregar();
            _snackbar.Show("Sucesso", $"Fornecedor '{fornecedor.Nome}' excluído.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }
}
