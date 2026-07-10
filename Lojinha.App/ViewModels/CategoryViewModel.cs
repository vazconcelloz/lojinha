using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lojinha.Data.Models;
using Lojinha.Services;

namespace Lojinha.App.ViewModels;

public partial class CategoryViewModel : ObservableObject
{
    private readonly CategoryService _service;

    public ObservableCollection<Category> Categorias { get; } = new();

    [ObservableProperty]
    private string novoNome = string.Empty;

    [ObservableProperty]
    private string? mensagemErro;

    public CategoryViewModel(CategoryService service)
    {
        _service = service;
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

    [RelayCommand]
    private void Adicionar()
    {
        MensagemErro = null;
        try
        {
            _service.Add(NovoNome);
            NovoNome = string.Empty;
            Carregar();
        }
        catch (Exception ex)
        {
            MensagemErro = ex.Message;
        }
    }
}
