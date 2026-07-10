using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lojinha.Data.Models;
using Lojinha.Services;

namespace Lojinha.App.ViewModels;

public partial class SupplierViewModel : ObservableObject
{
    private readonly SupplierService _service;

    public ObservableCollection<Supplier> Fornecedores { get; } = new();

    [ObservableProperty]
    private string novoNome = string.Empty;

    [ObservableProperty]
    private string? novoContato;

    [ObservableProperty]
    private string? mensagemErro;

    public SupplierViewModel(SupplierService service)
    {
        _service = service;
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
        MensagemErro = null;
        try
        {
            _service.Add(NovoNome, NovoContato);
            NovoNome = string.Empty;
            NovoContato = null;
            Carregar();
        }
        catch (Exception ex)
        {
            MensagemErro = ex.Message;
        }
    }
}
