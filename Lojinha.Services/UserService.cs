using System.Security.Cryptography;
using System.Text;
using Lojinha.Data;
using Lojinha.Data.Models;

namespace Lojinha.Services;

public class UserService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    private readonly LojinhaDbContext _context;

    public UserService(LojinhaDbContext context)
    {
        _context = context;
    }

    public bool AnyUsers()
    {
        return _context.Users.Any();
    }

    public User Add(string nomeUsuario, string senha, PapelUsuario papel)
    {
        if (string.IsNullOrWhiteSpace(nomeUsuario))
        {
            throw new ArgumentException("Nome de usuário é obrigatório.", nameof(nomeUsuario));
        }

        if (string.IsNullOrWhiteSpace(senha))
        {
            throw new ArgumentException("Senha é obrigatória.", nameof(senha));
        }

        if (_context.Users.Any(u => u.NomeUsuario == nomeUsuario))
        {
            throw new InvalidOperationException($"Já existe um usuário com o nome '{nomeUsuario}'.");
        }

        var (hash, salt) = HashSenha(senha);

        var user = new User
        {
            NomeUsuario = nomeUsuario,
            SenhaHash = hash,
            SenhaSalt = salt,
            Papel = papel
        };

        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    public void Update(int id, string nomeUsuario, string? novaSenha, PapelUsuario papel)
    {
        if (string.IsNullOrWhiteSpace(nomeUsuario))
        {
            throw new ArgumentException("Nome de usuário é obrigatório.", nameof(nomeUsuario));
        }

        if (_context.Users.Any(u => u.NomeUsuario == nomeUsuario && u.Id != id))
        {
            throw new InvalidOperationException($"Já existe um usuário com o nome '{nomeUsuario}'.");
        }

        var user = _context.Users.Find(id);
        if (user is null)
        {
            throw new InvalidOperationException("Usuário não encontrado.");
        }

        user.NomeUsuario = nomeUsuario;
        user.Papel = papel;

        if (!string.IsNullOrWhiteSpace(novaSenha))
        {
            var (hash, salt) = HashSenha(novaSenha);
            user.SenhaHash = hash;
            user.SenhaSalt = salt;
        }

        _context.SaveChanges();
    }

    public void Delete(int id)
    {
        var user = _context.Users.Find(id);
        if (user is null)
        {
            throw new InvalidOperationException("Usuário não encontrado.");
        }

        if (user.Papel == PapelUsuario.Admin && _context.Users.Count(u => u.Papel == PapelUsuario.Admin) <= 1)
        {
            throw new InvalidOperationException("Não é possível excluir o último administrador.");
        }

        _context.Users.Remove(user);
        _context.SaveChanges();
    }

    public IEnumerable<User> GetAll()
    {
        return _context.Users.ToList();
    }

    public User Authenticate(string nomeUsuario, string senha)
    {
        var user = _context.Users.FirstOrDefault(u => u.NomeUsuario == nomeUsuario);
        if (user is null)
        {
            throw new InvalidOperationException("Usuário ou senha inválidos.");
        }

        var hashCalculado = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(senha), user.SenhaSalt, Iterations, HashAlgorithmName.SHA256, HashSize);

        if (!CryptographicOperations.FixedTimeEquals(hashCalculado, user.SenhaHash))
        {
            throw new InvalidOperationException("Usuário ou senha inválidos.");
        }

        return user;
    }

    private static (byte[] Hash, byte[] Salt) HashSenha(string senha)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(senha), salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return (hash, salt);
    }
}
