using Lojinha.Data;
using Lojinha.Data.Models;

namespace Lojinha.Services;

public class ProductService
{
    private readonly LojinhaDbContext _context;

    public ProductService(LojinhaDbContext context)
    {
        _context = context;
    }

    public Product Add(string nome, string codigoBarras, int categoryId, TipoVenda tipoVenda, decimal precoCusto, decimal precoVenda, decimal estoqueMinimo)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Nome é obrigatório.", nameof(nome));
        }

        if (precoVenda <= 0)
        {
            throw new ArgumentException("Preço de venda deve ser maior que zero.", nameof(precoVenda));
        }

        if (_context.Products.Any(p => p.CodigoBarras == codigoBarras))
        {
            throw new InvalidOperationException($"Já existe um produto com o código de barras '{codigoBarras}'.");
        }

        var product = new Product
        {
            Nome = nome,
            CodigoBarras = codigoBarras,
            CategoryId = categoryId,
            TipoVenda = tipoVenda,
            PrecoCusto = precoCusto,
            PrecoVenda = precoVenda,
            EstoqueMinimo = estoqueMinimo
        };

        _context.Products.Add(product);
        _context.SaveChanges();
        return product;
    }

    public void Update(int id, string nome, string codigoBarras, int categoryId, TipoVenda tipoVenda, decimal precoCusto, decimal precoVenda, decimal estoqueMinimo)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Nome é obrigatório.", nameof(nome));
        }

        if (precoVenda <= 0)
        {
            throw new ArgumentException("Preço de venda deve ser maior que zero.", nameof(precoVenda));
        }

        if (_context.Products.Any(p => p.CodigoBarras == codigoBarras && p.Id != id))
        {
            throw new InvalidOperationException($"Já existe um produto com o código de barras '{codigoBarras}'.");
        }

        var product = _context.Products.Find(id);
        if (product is null)
        {
            throw new InvalidOperationException("Produto não encontrado.");
        }

        product.Nome = nome;
        product.CodigoBarras = codigoBarras;
        product.CategoryId = categoryId;
        product.TipoVenda = tipoVenda;
        product.PrecoCusto = precoCusto;
        product.PrecoVenda = precoVenda;
        product.EstoqueMinimo = estoqueMinimo;
        _context.SaveChanges();
    }

    public void Delete(int id)
    {
        var product = _context.Products.Find(id);
        if (product is null)
        {
            throw new InvalidOperationException("Produto não encontrado.");
        }

        if (_context.SaleItems.Any(si => si.ProductId == id))
        {
            throw new InvalidOperationException("Produto possui vendas registradas e não pode ser excluído.");
        }

        _context.Products.Remove(product);
        _context.SaveChanges();
    }

    public IEnumerable<Product> GetAll()
    {
        return _context.Products.ToList();
    }

    public IEnumerable<Product> Search(string termo)
    {
        if (string.IsNullOrWhiteSpace(termo))
        {
            return GetAll();
        }

        return _context.Products
            .Where(p => p.Nome.Contains(termo) || p.CodigoBarras.Contains(termo))
            .ToList();
    }
}
