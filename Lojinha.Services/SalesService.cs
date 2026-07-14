using Lojinha.Data;
using Lojinha.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Lojinha.Services;

public class SalesService
{
    private readonly LojinhaDbContext _context;
    private readonly StockService _stockService;

    public SalesService(LojinhaDbContext context, StockService stockService)
    {
        _context = context;
        _stockService = stockService;
    }

    public Sale RegisterSale(
        IEnumerable<(int ProductId, decimal Quantidade, decimal DescontoItem)> itens,
        FormaPagamento formaPagamento,
        string? usuarioNome = null,
        decimal descontoVenda = 0,
        decimal? valorRecebido = null,
        string? autorizadoPor = null)
    {
        var itensList = itens.ToList();
        if (itensList.Count == 0)
        {
            throw new ArgumentException("Adicione ao menos um item à venda.", nameof(itens));
        }

        foreach (var item in itensList)
        {
            if (item.Quantidade <= 0)
            {
                throw new ArgumentException("Quantidade deve ser maior que zero.", nameof(itens));
            }
        }

        var quantidadePorProduto = itensList
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantidade));

        var produtos = new Dictionary<int, Product>();
        foreach (var produtoId in quantidadePorProduto.Keys)
        {
            var produto = _context.Products.Find(produtoId)
                ?? throw new InvalidOperationException("Produto não encontrado.");
            produtos[produtoId] = produto;

            if (_stockService.GetCurrentStock(produtoId) < quantidadePorProduto[produtoId])
            {
                throw new InvalidOperationException($"Estoque insuficiente para '{produto.Nome}'. Disponível: {_stockService.GetCurrentStock(produtoId)}.");
            }
        }

        var sale = new Sale
        {
            DataHora = DateTime.Now,
            FormaPagamento = formaPagamento,
            Cancelada = false,
            UsuarioNome = usuarioNome
        };

        decimal subtotalCarrinho = 0;
        foreach (var item in itensList)
        {
            var produto = produtos[item.ProductId];
            var itemSubtotal = item.Quantidade * produto.PrecoVenda;

            if (item.DescontoItem < 0 || item.DescontoItem > itemSubtotal)
            {
                throw new ArgumentException("Desconto do item não pode ser maior que o subtotal.", nameof(itens));
            }

            var saleItem = new SaleItem
            {
                ProductId = item.ProductId,
                Quantidade = item.Quantidade,
                PrecoUnitario = produto.PrecoVenda,
                DescontoValor = item.DescontoItem
            };
            sale.Items.Add(saleItem);
            subtotalCarrinho += itemSubtotal - item.DescontoItem;
        }

        if (descontoVenda < 0 || descontoVenda > subtotalCarrinho)
        {
            throw new ArgumentException("Desconto da venda não pode ser maior que o subtotal.", nameof(descontoVenda));
        }

        sale.DescontoValor = descontoVenda;
        sale.Total = subtotalCarrinho - descontoVenda;
        sale.AutorizadoPor = autorizadoPor;

        if (formaPagamento == FormaPagamento.Dinheiro)
        {
            if (valorRecebido is null || valorRecebido < sale.Total)
            {
                throw new ArgumentException("Valor recebido é obrigatório e deve ser maior ou igual ao total.", nameof(valorRecebido));
            }
            sale.ValorRecebido = valorRecebido;
            sale.Troco = valorRecebido.Value - sale.Total;
        }

        _context.Sales.Add(sale);

        foreach (var item in itensList)
        {
            _stockService.DeductStock(item.ProductId, item.Quantidade);
        }

        _context.SaveChanges();

        return sale;
    }

    public void CancelSale(int id)
    {
        var sale = _context.Sales
            .Include(s => s.Items)
            .FirstOrDefault(s => s.Id == id);

        if (sale is null)
        {
            throw new InvalidOperationException("Venda não encontrada.");
        }

        if (sale.Cancelada)
        {
            throw new InvalidOperationException("Venda já foi cancelada.");
        }

        sale.Cancelada = true;
        sale.DataCancelamento = DateTime.Now;

        foreach (var item in sale.Items)
        {
            _stockService.AddLot(item.ProductId, item.Quantidade, dataValidade: null, supplierId: null);
        }

        _context.SaveChanges();
    }

    public IEnumerable<Sale> GetSaleHistory()
    {
        return _context.Sales
            .Include(s => s.Items)
            .ThenInclude(i => i.Product)
            .OrderByDescending(s => s.DataHora)
            .ToList();
    }
}
