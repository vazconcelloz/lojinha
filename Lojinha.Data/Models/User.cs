namespace Lojinha.Data.Models;

public class User
{
    public int Id { get; set; }
    public required string NomeUsuario { get; set; }
    public required byte[] SenhaHash { get; set; }
    public required byte[] SenhaSalt { get; set; }
    public PapelUsuario Papel { get; set; }
}
