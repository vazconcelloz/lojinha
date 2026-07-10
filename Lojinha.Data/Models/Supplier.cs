namespace Lojinha.Data.Models;

public class Supplier
{
    public int Id { get; set; }
    public required string Nome { get; set; }
    public string? Contato { get; set; }

    public ICollection<StockLot> StockLots { get; set; } = new List<StockLot>();
}
