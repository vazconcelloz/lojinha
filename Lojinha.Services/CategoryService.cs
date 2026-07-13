using Lojinha.Data;
using Lojinha.Data.Models;

namespace Lojinha.Services;

public class CategoryService
{
    private readonly LojinhaDbContext _context;

    public CategoryService(LojinhaDbContext context)
    {
        _context = context;
    }

    public Category Add(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Nome é obrigatório.", nameof(nome));
        }

        var category = new Category { Nome = nome };
        _context.Categories.Add(category);
        _context.SaveChanges();
        return category;
    }

    public void Delete(int id)
    {
        var category = _context.Categories.Find(id);
        if (category is null)
        {
            throw new InvalidOperationException("Categoria não encontrada.");
        }

        if (_context.Products.Any(p => p.CategoryId == id))
        {
            throw new InvalidOperationException("Categoria possui produtos vinculados e não pode ser excluída.");
        }

        _context.Categories.Remove(category);
        _context.SaveChanges();
    }

    public IEnumerable<Category> GetAll()
    {
        return _context.Categories.ToList();
    }
}
