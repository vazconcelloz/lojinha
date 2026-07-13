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

public record EstoqueAtualItem(string Produto, decimal Quantidade);

public record EstoqueBaixoItem(string Produto, decimal QuantidadeAtual, decimal EstoqueMinimo);

public record VencimentoItem(int LoteId, string Produto, decimal QuantidadeRestante, DateTime? DataValidade, bool Vencido);

public partial class StockViewModel : ObservableObject
{
    private readonly StockService _stockService;
    private readonly ProductService _productService;
    private readonly SupplierService _supplierService;
    private readonly ISnackbarService _snackbar;
    private readonly IContentDialogService _dialogService;

    public ObservableCollection<Product> Produtos { get; } = new();
    public ObservableCollection<Supplier> Fornecedores { get; } = new();
    public ObservableCollection<EstoqueAtualItem> EstoqueAtual { get; } = new();
    public ObservableCollection<EstoqueBaixoItem> EstoqueBaixo { get; } = new();
    public ObservableCollection<VencimentoItem> Vencimentos { get; } = new();

    [ObservableProperty]
    private Product? produtoSelecionado;

    [ObservableProperty]
    private decimal quantidade;

    [ObservableProperty]
    private Supplier? fornecedorSelecionado;

    [ObservableProperty]
    private DateTime? dataValidade;

    public StockViewModel(StockService stockService, ProductService productService, SupplierService supplierService, ISnackbarService snackbar, IContentDialogService dialogService)
    {
        _stockService = stockService;
        _productService = productService;
        _supplierService = supplierService;
        _snackbar = snackbar;
        _dialogService = dialogService;
        CarregarCombos();
        AtualizarPaineis();
    }

    public void Refresh()
    {
        CarregarCombos();
        AtualizarPaineis();
    }

    private void CarregarCombos()
    {
        var produtoSelecionadoId = ProdutoSelecionado?.Id;
        var fornecedorSelecionadoId = FornecedorSelecionado?.Id;

        Produtos.Clear();
        foreach (var produto in _productService.GetAll())
        {
            Produtos.Add(produto);
        }

        Fornecedores.Clear();
        foreach (var fornecedor in _supplierService.GetAll())
        {
            Fornecedores.Add(fornecedor);
        }

        ProdutoSelecionado = produtoSelecionadoId is null
            ? null
            : Produtos.FirstOrDefault(p => p.Id == produtoSelecionadoId);
        FornecedorSelecionado = fornecedorSelecionadoId is null
            ? null
            : Fornecedores.FirstOrDefault(f => f.Id == fornecedorSelecionadoId);
    }

    private void AtualizarPaineis()
    {
        EstoqueAtual.Clear();
        foreach (var produto in _productService.GetAll())
        {
            EstoqueAtual.Add(new EstoqueAtualItem(produto.Nome, _stockService.GetCurrentStock(produto.Id)));
        }

        EstoqueBaixo.Clear();
        foreach (var produto in _stockService.GetLowStockProducts())
        {
            EstoqueBaixo.Add(new EstoqueBaixoItem(produto.Nome, _stockService.GetCurrentStock(produto.Id), produto.EstoqueMinimo));
        }

        Vencimentos.Clear();
        foreach (var lote in _stockService.GetExpiredLots())
        {
            Vencimentos.Add(new VencimentoItem(lote.Id, lote.Product?.Nome ?? "-", lote.QuantidadeRestante, lote.DataValidade, true));
        }
        foreach (var lote in _stockService.GetExpiringLots())
        {
            Vencimentos.Add(new VencimentoItem(lote.Id, lote.Product?.Nome ?? "-", lote.QuantidadeRestante, lote.DataValidade, false));
        }
    }

    [RelayCommand]
    private void AdicionarLote()
    {
        if (ProdutoSelecionado is null)
        {
            _snackbar.Show("Erro", "Selecione um produto.", ControlAppearance.Danger);
            return;
        }

        try
        {
            _stockService.AddLot(ProdutoSelecionado.Id, Quantidade, DataValidade, FornecedorSelecionado?.Id);
            Quantidade = 0;
            DataValidade = null;
            AtualizarPaineis();
            _snackbar.Show("Sucesso", "Lote adicionado.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private async Task ExcluirLote(VencimentoItem item)
    {
        var options = new SimpleContentDialogCreateOptions
        {
            Title = "Excluir lote",
            Content = $"Tem certeza que deseja excluir o lote de '{item.Produto}'?",
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
            _stockService.DeleteLot(item.LoteId);
            AtualizarPaineis();
            _snackbar.Show("Sucesso", "Lote excluído.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }
}
