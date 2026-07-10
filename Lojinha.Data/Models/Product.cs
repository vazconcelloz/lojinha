namespace Lojinha.Data.Models;

public class Product
{
    public int Id { get; set; }
    public required string Nome { get; set; }
    public required string CodigoBarras { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public TipoVenda TipoVenda { get; set; }
    public decimal PrecoCusto { get; set; }
    public decimal PrecoVenda { get; set; }
    public decimal EstoqueMinimo { get; set; }

    public ICollection<StockLot> StockLots { get; set; } = new List<StockLot>();
}
