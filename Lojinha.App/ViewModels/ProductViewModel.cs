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

public partial class ProductViewModel : ObservableObject
{
    private readonly ProductService _productService;
    private readonly CategoryService _categoryService;
    private readonly ISnackbarService _snackbar;
    private readonly IContentDialogService _dialogService;

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
    private string termoBusca = string.Empty;

    public ProductViewModel(ProductService productService, CategoryService categoryService, ISnackbarService snackbar, IContentDialogService dialogService)
    {
        _productService = productService;
        _categoryService = categoryService;
        _snackbar = snackbar;
        _dialogService = dialogService;
        CarregarCategorias();
        Buscar();
    }

    public void Refresh()
    {
        CarregarCategorias();
        Buscar();
    }

    private void CarregarCategorias()
    {
        var categoriaSelecionadaId = CategoriaSelecionada?.Id;

        Categorias.Clear();
        foreach (var categoria in _categoryService.GetAll())
        {
            Categorias.Add(categoria);
        }

        CategoriaSelecionada = categoriaSelecionadaId is null
            ? null
            : Categorias.FirstOrDefault(c => c.Id == categoriaSelecionadaId);
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
        if (CategoriaSelecionada is null)
        {
            _snackbar.Show("Erro", "Selecione uma categoria.", ControlAppearance.Danger);
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
            _snackbar.Show("Sucesso", "Produto adicionado.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private async Task Excluir(Product produto)
    {
        var options = new SimpleContentDialogCreateOptions
        {
            Title = "Excluir produto",
            Content = $"Tem certeza que deseja excluir '{produto.Nome}'? Os lotes de estoque vinculados também serão removidos.",
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
            _productService.Delete(produto.Id);
            Buscar();
            _snackbar.Show("Sucesso", $"Produto '{produto.Nome}' excluído.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }
}
