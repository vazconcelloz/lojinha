using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lojinha.Data.Models;
using Lojinha.Services;

namespace Lojinha.App.ViewModels;

public partial class ProductViewModel : ObservableObject
{
    private readonly ProductService _productService;
    private readonly CategoryService _categoryService;

    public ObservableCollection<Product> Produtos { get; } = new();
    public ObservableCollection<Category> Categorias { get; } = new();
    public TipoVenda[] TiposVenda { get; } = Enum.GetValues<TipoVenda>();

    [ObservableProperty]
    private string nome = string.Empty;

    [ObservableProperty]
    private string codigoBarras = string.Empty;

    [ObservableProperty]
    private Category? categoriaSelecionada;

    [ObservableProperty]
    private TipoVenda tipoVendaSelecionado;

    [ObservableProperty]
    private decimal precoCusto;

    [ObservableProperty]
    private decimal precoVenda;

    [ObservableProperty]
    private decimal estoqueMinimo;

    [ObservableProperty]
    private string? mensagemErro;

    [ObservableProperty]
    private string termoBusca = string.Empty;

    public ProductViewModel(ProductService productService, CategoryService categoryService)
    {
        _productService = productService;
        _categoryService = categoryService;
        CarregarCategorias();
        Buscar();
    }

    private void CarregarCategorias()
    {
        Categorias.Clear();
        foreach (var categoria in _categoryService.GetAll())
        {
            Categorias.Add(categoria);
        }
    }

    partial void OnTermoBuscaChanged(string value)
    {
        Buscar();
    }

    [RelayCommand]
    private void Buscar()
    {
        Produtos.Clear();
        foreach (var produto in _productService.Search(TermoBusca))
        {
            Produtos.Add(produto);
        }
    }

    [RelayCommand]
    private void Adicionar()
    {
        MensagemErro = null;

        if (CategoriaSelecionada is null)
        {
            MensagemErro = "Selecione uma categoria.";
            return;
        }

        try
        {
            _productService.Add(Nome, CodigoBarras, CategoriaSelecionada.Id, TipoVendaSelecionado, PrecoCusto, PrecoVenda, EstoqueMinimo);
            Nome = string.Empty;
            CodigoBarras = string.Empty;
            PrecoCusto = 0;
            PrecoVenda = 0;
            EstoqueMinimo = 0;
            Buscar();
        }
        catch (Exception ex)
        {
            MensagemErro = ex.Message;
        }
    }
}
