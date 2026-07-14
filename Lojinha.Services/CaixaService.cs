using Lojinha.Data;
using Lojinha.Data.Models;

namespace Lojinha.Services;

public class CaixaService
{
    private readonly LojinhaDbContext _context;

    public CaixaService(LojinhaDbContext context)
    {
        _context = context;
    }

    public CaixaSessao? GetSessaoAberta()
    {
        return _context.CaixaSessoes.FirstOrDefault(c => c.DataFechamento == null);
    }

    public CaixaSessao AbrirCaixa(decimal valorAbertura, string usuarioNome)
    {
        if (GetSessaoAberta() is not null)
        {
            throw new InvalidOperationException("Já existe um caixa aberto.");
        }

        if (valorAbertura < 0)
        {
            throw new ArgumentException("Valor de abertura não pode ser negativo.", nameof(valorAbertura));
        }

        var sessao = new CaixaSessao
        {
            DataAbertura = DateTime.Now,
            ValorAbertura = valorAbertura,
            UsuarioAbertura = usuarioNome
        };

        _context.CaixaSessoes.Add(sessao);
        _context.SaveChanges();

        return sessao;
    }

    public MovimentoCaixa RegistrarMovimento(TipoMovimentoCaixa tipo, decimal valor, string autorizadoPor, string? observacao)
    {
        var sessao = GetSessaoAberta()
            ?? throw new InvalidOperationException("Nenhum caixa aberto.");

        if (valor <= 0)
        {
            throw new ArgumentException("Valor do movimento deve ser maior que zero.", nameof(valor));
        }

        var movimento = new MovimentoCaixa
        {
            CaixaSessaoId = sessao.Id,
            Tipo = tipo,
            Valor = valor,
            DataHora = DateTime.Now,
            AutorizadoPor = autorizadoPor,
            Observacao = observacao
        };

        _context.MovimentosCaixa.Add(movimento);
        _context.SaveChanges();

        return movimento;
    }

    public CaixaSessao FecharCaixa(decimal valorContado, string usuarioNome)
    {
        var sessao = GetSessaoAberta()
            ?? throw new InvalidOperationException("Nenhum caixa aberto para fechar.");

        if (valorContado < 0)
        {
            throw new ArgumentException("Valor contado não pode ser negativo.", nameof(valorContado));
        }

        var dataFechamento = DateTime.Now;

        var vendasDinheiro = _context.Sales
            .Where(s => s.FormaPagamento == FormaPagamento.Dinheiro
                && !s.Cancelada
                && s.DataHora >= sessao.DataAbertura
                && s.DataHora <= dataFechamento)
            .Select(s => s.Total)
            .AsEnumerable()
            .Sum();

        var movimentos = _context.MovimentosCaixa
            .Where(m => m.CaixaSessaoId == sessao.Id)
            .ToList();
        var suprimentos = movimentos.Where(m => m.Tipo == TipoMovimentoCaixa.Suprimento).Sum(m => m.Valor);
        var sangrias = movimentos.Where(m => m.Tipo == TipoMovimentoCaixa.Sangria).Sum(m => m.Valor);

        var valorEsperado = sessao.ValorAbertura + vendasDinheiro + suprimentos - sangrias;

        sessao.DataFechamento = dataFechamento;
        sessao.ValorContado = valorContado;
        sessao.ValorEsperado = valorEsperado;
        sessao.Diferenca = valorContado - valorEsperado;
        sessao.UsuarioFechamento = usuarioNome;

        _context.SaveChanges();

        return sessao;
    }

    public IEnumerable<MovimentoCaixa> GetMovimentos(int sessaoId)
    {
        return _context.MovimentosCaixa
            .Where(m => m.CaixaSessaoId == sessaoId)
            .OrderByDescending(m => m.DataHora)
            .ToList();
    }
}
