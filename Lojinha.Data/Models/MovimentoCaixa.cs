namespace Lojinha.Data.Models;

public class MovimentoCaixa
{
    public int Id { get; set; }
    public int CaixaSessaoId { get; set; }
    public CaixaSessao? CaixaSessao { get; set; }
    public TipoMovimentoCaixa Tipo { get; set; }
    public decimal Valor { get; set; }
    public DateTime DataHora { get; set; }
    public required string AutorizadoPor { get; set; }
    public string? Observacao { get; set; }
}
