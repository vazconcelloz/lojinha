using Lojinha.Data;
using Lojinha.Data.Models;

namespace Lojinha.Services;

public class SupplierService
{
    private readonly LojinhaDbContext _context;

    public SupplierService(LojinhaDbContext context)
    {
        _context = context;
    }

    public Supplier Add(string nome, string? contato)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Nome é obrigatório.", nameof(nome));
        }

        var supplier = new Supplier { Nome = nome, Contato = contato };
        _context.Suppliers.Add(supplier);
        _context.SaveChanges();
        return supplier;
    }

    public void Delete(int id)
    {
        var supplier = _context.Suppliers.Find(id);
        if (supplier is null)
        {
            throw new InvalidOperationException("Fornecedor não encontrado.");
        }

        _context.Suppliers.Remove(supplier);
        _context.SaveChanges();
    }

    public IEnumerable<Supplier> GetAll()
    {
        return _context.Suppliers.ToList();
    }
}
