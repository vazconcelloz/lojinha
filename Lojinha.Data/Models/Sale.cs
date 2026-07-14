namespace Lojinha.Data.Models;

public class Sale
{
    public int Id { get; set; }
    public DateTime DataHora { get; set; }
    public FormaPagamento FormaPagamento { get; set; }
    public decimal Total { get; set; }
    public bool Cancelada { get; set; }
    public DateTime? DataCancelamento { get; set; }
    public string? UsuarioNome { get; set; }
    public decimal DescontoValor { get; set; }
    public decimal? ValorRecebido { get; set; }
    public decimal? Troco { get; set; }
    public string? AutorizadoPor { get; set; }

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}
