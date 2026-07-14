namespace Lojinha.Data.Models;

public class CaixaSessao
{
    public int Id { get; set; }
    public DateTime DataAbertura { get; set; }
    public decimal ValorAbertura { get; set; }
    public required string UsuarioAbertura { get; set; }
    public DateTime? DataFechamento { get; set; }
    public decimal? ValorContado { get; set; }
    public decimal? ValorEsperado { get; set; }
    public decimal? Diferenca { get; set; }
    public string? UsuarioFechamento { get; set; }

    public ICollection<MovimentoCaixa> Movimentos { get; set; } = new List<MovimentoCaixa>();
}
