using Microsoft.EntityFrameworkCore;
using Lojinha.Data.Models;

namespace Lojinha.Data;

public class LojinhaDbContext : DbContext
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockLot> StockLots => Set<StockLot>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<User> Users => Set<User>();
    public DbSet<CaixaSessao> CaixaSessoes => Set<CaixaSessao>();
    public DbSet<MovimentoCaixa> MovimentosCaixa => Set<MovimentoCaixa>();

    public LojinhaDbContext(DbContextOptions<LojinhaDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>()
            .HasIndex(p => p.CodigoBarras)
            .IsUnique();

        modelBuilder.Entity<Product>()
            .Property(p => p.PrecoCusto)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Product>()
            .Property(p => p.PrecoVenda)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Product>()
            .Property(p => p.EstoqueMinimo)
            .HasPrecision(10, 3);

        modelBuilder.Entity<StockLot>()
            .Property(s => s.Quantidade)
            .HasPrecision(10, 3);

        modelBuilder.Entity<StockLot>()
            .Property(s => s.QuantidadeRestante)
            .HasPrecision(10, 3);

        modelBuilder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockLot>()
            .HasOne(s => s.Product)
            .WithMany(p => p.StockLots)
            .HasForeignKey(s => s.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<StockLot>()
            .HasOne(s => s.Supplier)
            .WithMany(sup => sup.StockLots)
            .HasForeignKey(s => s.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Sale>()
            .Property(s => s.Total)
            .HasPrecision(10, 2);

        modelBuilder.Entity<SaleItem>()
            .Property(i => i.Quantidade)
            .HasPrecision(10, 3);

        modelBuilder.Entity<SaleItem>()
            .Property(i => i.PrecoUnitario)
            .HasPrecision(10, 2);

        modelBuilder.Entity<SaleItem>()
            .Property(i => i.DescontoValor)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Sale>()
            .Property(s => s.DescontoValor)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Sale>()
            .Property(s => s.ValorRecebido)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Sale>()
            .Property(s => s.Troco)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Sale>()
            .HasMany(s => s.Items)
            .WithOne(i => i.Sale)
            .HasForeignKey(i => i.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SaleItem>()
            .HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.NomeUsuario)
            .IsUnique();

        modelBuilder.Entity<CaixaSessao>()
            .Property(c => c.ValorAbertura)
            .HasPrecision(10, 2);

        modelBuilder.Entity<CaixaSessao>()
            .Property(c => c.ValorContado)
            .HasPrecision(10, 2);

        modelBuilder.Entity<CaixaSessao>()
            .Property(c => c.ValorEsperado)
            .HasPrecision(10, 2);

        modelBuilder.Entity<CaixaSessao>()
            .Property(c => c.Diferenca)
            .HasPrecision(10, 2);

        modelBuilder.Entity<MovimentoCaixa>()
            .Property(m => m.Valor)
            .HasPrecision(10, 2);

        modelBuilder.Entity<MovimentoCaixa>()
            .HasOne(m => m.CaixaSessao)
            .WithMany(c => c.Movimentos)
            .HasForeignKey(m => m.CaixaSessaoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
