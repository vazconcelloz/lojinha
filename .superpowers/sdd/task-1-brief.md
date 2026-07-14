### Task 1: `TipoDesconto` enum, `Sale`/`SaleItem` model changes, migration

**Files:**
- Create: `Lojinha.Data/Models/TipoDesconto.cs`
- Modify: `Lojinha.Data/Models/Sale.cs`
- Modify: `Lojinha.Data/Models/SaleItem.cs`
- Modify: `Lojinha.Data/LojinhaDbContext.cs`
- Migration: generated under `Lojinha.Data/Migrations/`

**Interfaces:**
- Produces: `TipoDesconto` enum (`Valor`, `Percentual`); `Sale.DescontoValor` (`decimal`), `Sale.ValorRecebido` (`decimal?`), `Sale.Troco` (`decimal?`), `Sale.AutorizadoPor` (`string?`); `SaleItem.DescontoValor` (`decimal`) — consumed by Task 2 (`SalesService`) and Task 4 (`SalesViewModel`).

- [ ] **Step 1: Create the `TipoDesconto` enum**

Create `Lojinha.Data/Models/TipoDesconto.cs`:

```csharp
namespace Lojinha.Data.Models;

public enum TipoDesconto
{
    Valor,
    Percentual
}
```

- [ ] **Step 2: Add discount/payment fields to `Sale`**

In `Lojinha.Data/Models/Sale.cs`, add these properties after `UsuarioNome`:

```csharp
    public decimal DescontoValor { get; set; }
    public decimal? ValorRecebido { get; set; }
    public decimal? Troco { get; set; }
    public string? AutorizadoPor { get; set; }
```

- [ ] **Step 3: Add discount field to `SaleItem`**

In `Lojinha.Data/Models/SaleItem.cs`, add this property after `PrecoUnitario` (before the existing `Subtotal` computed property):

```csharp
    public decimal DescontoValor { get; set; }
```

Then add a computed property after the existing `Subtotal` property:

```csharp
    public decimal SubtotalComDesconto => Subtotal - DescontoValor;
```

- [ ] **Step 4: Wire precision configuration into `LojinhaDbContext`**

In `Lojinha.Data/LojinhaDbContext.cs`, inside `OnModelCreating`, immediately after the existing block:

```csharp
        modelBuilder.Entity<SaleItem>()
            .Property(i => i.PrecoUnitario)
            .HasPrecision(10, 2);
```

add:

```csharp
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
```

- [ ] **Step 5: Generate the EF Core migration**

Run: `dotnet ef migrations add AddDescontoTroco --project Lojinha.Data`
Expected: no errors; two new files appear under `Lojinha.Data/Migrations/` (a new `..._AddDescontoTroco.cs` migration + matching `.Designer.cs`), and `Lojinha.Data/Migrations/LojinhaDbContextModelSnapshot.cs` is updated with the new columns.

- [ ] **Step 6: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 7: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 54 tests total (unchanged — this task adds no tests, just schema).

- [ ] **Step 8: Commit**

```bash
git add Lojinha.Data/Models/TipoDesconto.cs Lojinha.Data/Models/Sale.cs Lojinha.Data/Models/SaleItem.cs Lojinha.Data/LojinhaDbContext.cs Lojinha.Data/Migrations
git commit -m "feat: add TipoDesconto enum and Sale/SaleItem discount and troco fields"
```

---

