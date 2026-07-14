using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lojinha.App.Services;
using Lojinha.Data.Models;
using Lojinha.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace Lojinha.App.ViewModels;

public record ItemCarrinho(int ProductId, string Produto, decimal Quantidade, decimal PrecoUnitario)
{
    public decimal Subtotal => Quantidade * PrecoUnitario;
}

public record VendaHistoricoItem(int SaleId, DateTime DataHora, decimal Total, FormaPagamento FormaPagamento, bool Cancelada, string? UsuarioNome)
{
    public string Status => Cancelada ? "Cancelada" : "Concluída";
    public bool PodeCancelar => !Cancelada;
}

public partial class SalesViewModel : ObservableObject
{
    private readonly SalesService _salesService;
    private readonly ProductService _productService;
    private readonly ISnackbarService _snackbar;
    private readonly IContentDialogService _dialogService;
    private readonly SessionService _session;

    public ObservableCollection<Product> Produtos { get; } = new();
    public ObservableCollection<ItemCarrinho> Carrinho { get; } = new();
    public ObservableCollection<VendaHistoricoItem> Historico { get; } = new();
    public FormaPagamento[] FormasPagamento { get; } = Enum.GetValues<FormaPagamento>();

    [ObservableProperty]
    private string termoBusca = string.Empty;

    [ObservableProperty]
    private Product? produtoSelecionado;

    [ObservableProperty]
    private decimal quantidade;

    [ObservableProperty]
    private FormaPagamento formaPagamentoSelecionada;

    public decimal Total => Carrinho.Sum(i => i.Subtotal);

    public bool PodeCancelarVenda => _session.CurrentUser?.Papel == PapelUsuario.Admin;

    public SalesViewModel(SalesService salesService, ProductService productService, ISnackbarService snackbar, IContentDialogService dialogService, SessionService session)
    {
        _salesService = salesService;
        _productService = productService;
        _snackbar = snackbar;
        _dialogService = dialogService;
        _session = session;
        Carrinho.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Total));
        CarregarProdutos();
        CarregarHistorico();
    }

    public void Refresh()
    {
        CarregarProdutos();
        CarregarHistorico();
        OnPropertyChanged(nameof(PodeCancelarVenda));
    }

    private void CarregarProdutos()
    {
        var produtoSelecionadoId = ProdutoSelecionado?.Id;

        Produtos.Clear();
        foreach (var produto in _productService.Search(TermoBusca))
        {
            Produtos.Add(produto);
        }

        ProdutoSelecionado = produtoSelecionadoId is null
            ? null
            : Produtos.FirstOrDefault(p => p.Id == produtoSelecionadoId);
    }

    private void CarregarHistorico()
    {
        Historico.Clear();
        foreach (var venda in _salesService.GetSaleHistory())
        {
            Historico.Add(new VendaHistoricoItem(venda.Id, venda.DataHora, venda.Total, venda.FormaPagamento, venda.Cancelada, venda.UsuarioNome));
        }
    }

    partial void OnTermoBuscaChanged(string value)
    {
        CarregarProdutos();
    }

    [RelayCommand]
    private void AdicionarAoCarrinho()
    {
        if (ProdutoSelecionado is null)
        {
            _snackbar.Show("Erro", "Selecione um produto.", ControlAppearance.Danger);
            return;
        }

        if (Quantidade <= 0)
        {
            _snackbar.Show("Erro", "Quantidade deve ser maior que zero.", ControlAppearance.Danger);
            return;
        }

        Carrinho.Add(new ItemCarrinho(ProdutoSelecionado.Id, ProdutoSelecionado.Nome, Quantidade, ProdutoSelecionado.PrecoVenda));
        Quantidade = 0;
    }

    [RelayCommand]
    private void Escanear()
    {
        var codigo = TermoBusca.Trim();
        if (string.IsNullOrEmpty(codigo))
        {
            return;
        }

        var produto = _productService.GetAll().FirstOrDefault(p => p.CodigoBarras == codigo);
        if (produto is null)
        {
            _snackbar.Show("Erro", "Produto não encontrado.", ControlAppearance.Danger);
            TermoBusca = string.Empty;
            return;
        }

        var quantidadeAdicionar = Quantidade > 0 ? Quantidade : 1;

        var itemExistente = Carrinho.FirstOrDefault(i => i.ProductId == produto.Id);
        if (itemExistente is not null)
        {
            var index = Carrinho.IndexOf(itemExistente);
            Carrinho[index] = itemExistente with { Quantidade = itemExistente.Quantidade + quantidadeAdicionar };
        }
        else
        {
            Carrinho.Add(new ItemCarrinho(produto.Id, produto.Nome, quantidadeAdicionar, produto.PrecoVenda));
        }

        TermoBusca = string.Empty;
        Quantidade = 0;
    }

    [RelayCommand]
    private void RemoverDoCarrinho(ItemCarrinho item)
    {
        Carrinho.Remove(item);
    }

    [RelayCommand]
    private void FinalizarVenda()
    {
        if (Carrinho.Count == 0)
        {
            _snackbar.Show("Erro", "Adicione ao menos um item à venda.", ControlAppearance.Danger);
            return;
        }

        try
        {
            var itens = Carrinho.Select(i => (i.ProductId, i.Quantidade, DescontoItem: 0m));
            var valorRecebido = FormaPagamentoSelecionada == FormaPagamento.Dinheiro ? Total : (decimal?)null;
            _salesService.RegisterSale(itens, FormaPagamentoSelecionada, _session.CurrentUser?.NomeUsuario, valorRecebido: valorRecebido);
            Carrinho.Clear();
            CarregarHistorico();
            _snackbar.Show("Sucesso", "Venda registrada.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }

    [RelayCommand]
    private async Task CancelarVenda(VendaHistoricoItem item)
    {
        var options = new SimpleContentDialogCreateOptions
        {
            Title = "Cancelar venda",
            Content = $"Tem certeza que deseja cancelar a venda de {item.DataHora:dd/MM/yyyy HH:mm}? O estoque vendido será devolvido.",
            PrimaryButtonText = "Cancelar venda",
            CloseButtonText = "Voltar",
        };

        var result = await _dialogService.ShowSimpleDialogAsync(options, CancellationToken.None);
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            _salesService.CancelSale(item.SaleId);
            CarregarHistorico();
            _snackbar.Show("Sucesso", "Venda cancelada.", ControlAppearance.Success);
        }
        catch (Exception ex)
        {
            _snackbar.Show("Erro", ex.Message, ControlAppearance.Danger);
        }
    }
}
