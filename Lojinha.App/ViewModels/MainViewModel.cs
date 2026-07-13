namespace Lojinha.App.ViewModels;

public class MainViewModel
{
    public CategoryViewModel Categorias { get; }
    public SupplierViewModel Fornecedores { get; }
    public ProductViewModel Produtos { get; }
    public StockViewModel Estoque { get; }
    public SalesViewModel Vendas { get; }

    public MainViewModel(CategoryViewModel categorias, SupplierViewModel fornecedores, ProductViewModel produtos, StockViewModel estoque, SalesViewModel vendas)
    {
        Categorias = categorias;
        Fornecedores = fornecedores;
        Produtos = produtos;
        Estoque = estoque;
        Vendas = vendas;
    }
}
