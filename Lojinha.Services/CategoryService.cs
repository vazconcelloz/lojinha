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

    public IEnumerable<Category> GetAll()
    {
        return _context.Categories.ToList();
    }
}
