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

    public SupplierViewModel(SupplierService service, ISnackbarService snackbar, IContentDialogService dialogService)
    {
        _service = service;
        _snackbar = snackbar;
        _dialogService = dialogService;
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
