namespace Lojinha.Data.Models;

public class StockLot
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public decimal Quantidade { get; set; }
    public decimal QuantidadeRestante { get; set; }
    public DateTime DataEntrada { get; set; }
    public DateTime? DataValidade { get; set; }
}
