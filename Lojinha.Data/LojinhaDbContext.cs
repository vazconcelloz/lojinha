using Microsoft.EntityFrameworkCore;
using Lojinha.Data.Models;

namespace Lojinha.Data;

public class LojinhaDbContext : DbContext
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockLot> StockLots => Set<StockLot>();

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
    }
}
