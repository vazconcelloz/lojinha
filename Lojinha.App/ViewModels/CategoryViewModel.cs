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

public partial class CategoryViewModel : ObservableObject
{
    private readonly CategoryService _service;
    private readonly ISnackbarService _snackbar;
    private readonly IContentDialogService _dialogService;

    public ObservableCollection<Category> Categorias { get; } = new();

    [ObservableProperty]
    private string novoNome = string.Empty;

    [ObservableProperty]
    private int? editandoId;

    public bool EmEdicao => EditandoId is not null;

    public CategoryViewModel(CategoryService service, ISnackbarService snackbar, IContentDialogService dialogService)
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
        Categorias.Clear();
        foreach (var categoria in _service.GetAll())
        {
            Categorias.Add(categoria);
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
            _service.Add(NovoNome);
            NovoNome = string.Empty;
            Carregar();
            _snackbar.Show("Sucesso", "Categoria adicionada.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private void Editar(Category categoria)
    {
        EditandoId = categoria.Id;
        NovoNome = categoria.Nome;
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
            _service.Update(EditandoId.Value, NovoNome);
            NovoNome = string.Empty;
            EditandoId = null;
            Carregar();
            _snackbar.Show("Sucesso", "Categoria atualizada.", ControlAppearance.Success);
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
        EditandoId = null;
    }

    [RelayCommand]
    private async Task Excluir(Category categoria)
    {
        var options = new SimpleContentDialogCreateOptions
        {
            Title = "Excluir categoria",
            Content = $"Tem certeza que deseja excluir '{categoria.Nome}'?",
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
            _service.Delete(categoria.Id);
            Carregar();
            _snackbar.Show("Sucesso", $"Categoria '{categoria.Nome}' excluída.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }
}
