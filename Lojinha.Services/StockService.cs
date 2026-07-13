using Lojinha.Data;
using Lojinha.Data.Models;
using Microsoft.EntityFrameworkCore;

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
            .Select(l => l.QuantidadeRestante)
            .AsEnumerable()
            .Sum();
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

    public void DeleteLot(int id)
    {
        var lot = _context.StockLots.Find(id);
        if (lot is null)
        {
            throw new InvalidOperationException("Lote não encontrado.");
        }

        _context.StockLots.Remove(lot);
        _context.SaveChanges();
    }

    public void DeductStock(int productId, decimal quantidade)
    {
        if (quantidade <= 0)
        {
            throw new ArgumentException("Quantidade deve ser maior que zero.", nameof(quantidade));
        }

        var lotes = _context.StockLots
            .Where(l => l.ProductId == productId && l.QuantidadeRestante > 0)
            .OrderBy(l => l.DataEntrada)
            .ToList();

        var disponivel = lotes.Sum(l => l.QuantidadeRestante);
        if (disponivel < quantidade)
        {
            throw new InvalidOperationException("Estoque insuficiente para dar baixa.");
        }

        var restante = quantidade;
        foreach (var lote in lotes)
        {
            if (restante <= 0)
            {
                break;
            }

            var consumido = Math.Min(lote.QuantidadeRestante, restante);
            lote.QuantidadeRestante -= consumido;
            restante -= consumido;
        }

        _context.SaveChanges();
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
            .Include(l => l.Product)
            .Where(l => l.QuantidadeRestante > 0
                && l.DataValidade != null
                && l.DataValidade >= DateTime.Today
                && l.DataValidade <= limite)
            .ToList();
    }

    public IEnumerable<StockLot> GetExpiredLots()
    {
        return _context.StockLots
            .Include(l => l.Product)
            .Where(l => l.QuantidadeRestante > 0
                && l.DataValidade != null
                && l.DataValidade < DateTime.Today)
            .ToList();
    }
}
