using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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

public partial class ItemCarrinho : ObservableObject
{
    public int ProductId { get; }
    public string Produto { get; }

    [ObservableProperty]
    private decimal quantidade;

    [ObservableProperty]
    private decimal precoUnitario;

    [ObservableProperty]
    private TipoDesconto descontoTipo = TipoDesconto.Valor;

    [ObservableProperty]
    private decimal descontoEntrada;

    public decimal Subtotal => Quantidade * PrecoUnitario;

    public decimal DescontoAplicado => DescontoTipo == TipoDesconto.Percentual
        ? Subtotal * DescontoEntrada / 100
        : DescontoEntrada;

    public decimal SubtotalComDesconto => Subtotal - DescontoAplicado;

    public ItemCarrinho(int productId, string produto, decimal quantidade, decimal precoUnitario)
    {
        ProductId = productId;
        Produto = produto;
        this.quantidade = quantidade;
        this.precoUnitario = precoUnitario;
    }

    partial void OnQuantidadeChanged(decimal value)
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(DescontoAplicado));
        OnPropertyChanged(nameof(SubtotalComDesconto));
    }

    partial void OnPrecoUnitarioChanged(decimal value)
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(DescontoAplicado));
        OnPropertyChanged(nameof(SubtotalComDesconto));
    }

    partial void OnDescontoTipoChanged(TipoDesconto value)
    {
        OnPropertyChanged(nameof(DescontoAplicado));
        OnPropertyChanged(nameof(SubtotalComDesconto));
    }

    partial void OnDescontoEntradaChanged(decimal value)
    {
        OnPropertyChanged(nameof(DescontoAplicado));
        OnPropertyChanged(nameof(SubtotalComDesconto));
    }
}

public record VendaHistoricoItem(int SaleId, DateTime DataHora, decimal Total, FormaPagamento FormaPagamento, bool Cancelada, string? UsuarioNome, decimal DescontoValor, decimal? ValorRecebido, decimal? Troco, string? AutorizadoPor)
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
    private readonly IAuthorizationService _authorizationService;

    public ObservableCollection<Product> Produtos { get; } = new();
    public ObservableCollection<ItemCarrinho> Carrinho { get; } = new();
    public ObservableCollection<VendaHistoricoItem> Historico { get; } = new();
    public FormaPagamento[] FormasPagamento { get; } = Enum.GetValues<FormaPagamento>();
    public TipoDesconto[] TiposDesconto { get; } = Enum.GetValues<TipoDesconto>();

    [ObservableProperty]
    private string termoBusca = string.Empty;

    [ObservableProperty]
    private Product? produtoSelecionado;

    [ObservableProperty]
    private decimal quantidade;

    [ObservableProperty]
    private FormaPagamento formaPagamentoSelecionada;

    [ObservableProperty]
    private TipoDesconto tipoDescontoVenda = TipoDesconto.Valor;

    [ObservableProperty]
    private decimal descontoVendaEntrada;

    [ObservableProperty]
    private decimal valorRecebido;

    public decimal CarrinhoSubtotal => Carrinho.Sum(i => i.SubtotalComDesconto);

    public decimal DescontoVendaAplicado => TipoDescontoVenda == TipoDesconto.Percentual
        ? CarrinhoSubtotal * DescontoVendaEntrada / 100
        : DescontoVendaEntrada;

    public decimal Total => CarrinhoSubtotal - DescontoVendaAplicado;

    public bool EhDinheiro => FormaPagamentoSelecionada == FormaPagamento.Dinheiro;

    public decimal Troco => EhDinheiro ? Math.Max(0, ValorRecebido - Total) : 0;

    public bool PodeCancelarVenda => _session.CurrentUser?.Papel == PapelUsuario.Admin;

    public SalesViewModel(SalesService salesService, ProductService productService, ISnackbarService snackbar, IContentDialogService dialogService, SessionService session, IAuthorizationService authorizationService)
    {
        _salesService = salesService;
        _productService = productService;
        _snackbar = snackbar;
        _dialogService = dialogService;
        _session = session;
        _authorizationService = authorizationService;
        Carrinho.CollectionChanged += OnCarrinhoChanged;
        CarregarProdutos();
        CarregarHistorico();
    }

    private void OnCarrinhoChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ItemCarrinho item in e.OldItems)
            {
                item.PropertyChanged -= OnItemCarrinhoPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (ItemCarrinho item in e.NewItems)
            {
                item.PropertyChanged += OnItemCarrinhoPropertyChanged;
            }
        }

        RaiseTotaisChanged();
    }

    private void OnItemCarrinhoPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ItemCarrinho.SubtotalComDesconto))
        {
            RaiseTotaisChanged();
        }
    }

    private void RaiseTotaisChanged()
    {
        OnPropertyChanged(nameof(CarrinhoSubtotal));
        OnPropertyChanged(nameof(DescontoVendaAplicado));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(Troco));
    }

    partial void OnTipoDescontoVendaChanged(TipoDesconto value)
    {
        OnPropertyChanged(nameof(DescontoVendaAplicado));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(Troco));
    }

    partial void OnDescontoVendaEntradaChanged(decimal value)
    {
        OnPropertyChanged(nameof(DescontoVendaAplicado));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(Troco));
    }

    partial void OnValorRecebidoChanged(decimal value)
    {
        OnPropertyChanged(nameof(Troco));
    }

    partial void OnFormaPagamentoSelecionadaChanged(FormaPagamento value)
    {
        OnPropertyChanged(nameof(EhDinheiro));
        OnPropertyChanged(nameof(Troco));
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
            var descontoTotal = venda.DescontoValor + venda.Items.Sum(i => i.DescontoValor);
            Historico.Add(new VendaHistoricoItem(venda.Id, venda.DataHora, venda.Total, venda.FormaPagamento, venda.Cancelada, venda.UsuarioNome, descontoTotal, venda.ValorRecebido, venda.Troco, venda.AutorizadoPor));
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
            itemExistente.Quantidade += quantidadeAdicionar;
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

        var temDesconto = Carrinho.Any(i => i.DescontoAplicado > 0) || DescontoVendaAplicado > 0;
        string? autorizadoPor = null;

        if (temDesconto)
        {
            if (_session.CurrentUser?.Papel == PapelUsuario.Admin)
            {
                autorizadoPor = _session.CurrentUser.NomeUsuario;
            }
            else
            {
                autorizadoPor = _authorizationService.AutorizarDesconto();
                if (autorizadoPor is null)
                {
                    _snackbar.Show("Erro", "Desconto não autorizado.", ControlAppearance.Danger);
                    return;
                }
            }
        }

        try
        {
            var itens = Carrinho.Select(i => (i.ProductId, i.Quantidade, i.DescontoAplicado));
            var valorRecebidoVenda = EhDinheiro ? ValorRecebido : (decimal?)null;
            _salesService.RegisterSale(itens, FormaPagamentoSelecionada, _session.CurrentUser?.NomeUsuario, DescontoVendaAplicado, valorRecebidoVenda, autorizadoPor);
            Carrinho.Clear();
            TipoDescontoVenda = TipoDesconto.Valor;
            DescontoVendaEntrada = 0;
            ValorRecebido = 0;
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
