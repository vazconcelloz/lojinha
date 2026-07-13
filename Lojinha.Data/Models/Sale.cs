namespace Lojinha.Data.Models;

public class Sale
{
    public int Id { get; set; }
    public DateTime DataHora { get; set; }
    public FormaPagamento FormaPagamento { get; set; }
    public decimal Total { get; set; }
    public bool Cancelada { get; set; }
    public DateTime? DataCancelamento { get; set; }

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}
