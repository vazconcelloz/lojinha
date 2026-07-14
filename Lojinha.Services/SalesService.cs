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

    public Sale RegisterSale(IEnumerable<(int ProductId, decimal Quantidade)> itens, FormaPagamento formaPagamento, string? usuarioNome = null)
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

        decimal total = 0;
        foreach (var item in itensList)
        {
            var produto = produtos[item.ProductId];
            var saleItem = new SaleItem
            {
                ProductId = item.ProductId,
                Quantidade = item.Quantidade,
                PrecoUnitario = produto.PrecoVenda
            };
            sale.Items.Add(saleItem);
            total += saleItem.Subtotal;
        }
        sale.Total = total;

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
