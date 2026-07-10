using Lojinha.Data;
using Lojinha.Data.Models;

namespace Lojinha.Services;

public class StockService
{
    private readonly LojinhaDbContext _context;

    public StockService(LojinhaDbContext context)
    {
        _context = context;
    }

    public decimal GetCurrentStock(int productId)
    {
        return _context.StockLots
            .Where(l => l.ProductId == productId)
            .Sum(l => l.QuantidadeRestante);
    }

    public StockLot AddLot(int productId, decimal quantidade, DateTime? dataValidade, int? supplierId)
    {
        if (quantidade <= 0)
        {
            throw new ArgumentException("Quantidade deve ser maior que zero.", nameof(quantidade));
        }

        var lot = new StockLot
        {
            ProductId = productId,
            SupplierId = supplierId,
            Quantidade = quantidade,
            QuantidadeRestante = quantidade,
            DataEntrada = DateTime.Today,
            DataValidade = dataValidade
        };

        _context.StockLots.Add(lot);
        _context.SaveChanges();
        return lot;
    }

    public IEnumerable<Product> GetLowStockProducts()
    {
        return _context.Products
            .ToList()
            .Where(p => GetCurrentStock(p.Id) < p.EstoqueMinimo);
    }

    public IEnumerable<StockLot> GetExpiringLots(int diasLimite = 7)
    {
        var limite = DateTime.Today.AddDays(diasLimite);
        return _context.StockLots
            .Where(l => l.QuantidadeRestante > 0
                && l.DataValidade != null
                && l.DataValidade >= DateTime.Today
                && l.DataValidade <= limite)
            .ToList();
    }

    public IEnumerable<StockLot> GetExpiredLots()
    {
        return _context.StockLots
            .Where(l => l.QuantidadeRestante > 0
                && l.DataValidade != null
                && l.DataValidade < DateTime.Today)
            .ToList();
    }
}
