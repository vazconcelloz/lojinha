### Task 1: `CaixaSessao`/`MovimentoCaixa` models, `DbContext` wiring, migration

**Files:**
- Create: `Lojinha.Data/Models/TipoMovimentoCaixa.cs`
- Create: `Lojinha.Data/Models/CaixaSessao.cs`
- Create: `Lojinha.Data/Models/MovimentoCaixa.cs`
- Modify: `Lojinha.Data/LojinhaDbContext.cs`
- Migration: generated under `Lojinha.Data/Migrations/`

**Interfaces:**
- Produces: `TipoMovimentoCaixa` enum (`Sangria`, `Suprimento`); `CaixaSessao` (`Id`, `DataAbertura`, `ValorAbertura`, `UsuarioAbertura`, `DataFechamento`, `ValorContado`, `ValorEsperado`, `Diferenca`, `UsuarioFechamento`, `Movimentos`); `MovimentoCaixa` (`Id`, `CaixaSessaoId`, `CaixaSessao`, `Tipo`, `Valor`, `DataHora`, `AutorizadoPor`, `Observacao`); `LojinhaDbContext.CaixaSessoes`/`MovimentosCaixa` `DbSet`s — consumed by Task 2 (`CaixaService`).

- [ ] **Step 1: Create the `TipoMovimentoCaixa` enum**

Create `Lojinha.Data/Models/TipoMovimentoCaixa.cs`:

```csharp
namespace Lojinha.Data.Models;

public enum TipoMovimentoCaixa
{
    Sangria,
    Suprimento
}
```

- [ ] **Step 2: Create the `CaixaSessao` model**

Create `Lojinha.Data/Models/CaixaSessao.cs`:

```csharp
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
```

- [ ] **Step 3: Create the `MovimentoCaixa` model**

Create `Lojinha.Data/Models/MovimentoCaixa.cs`:

```csharp
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
```

- [ ] **Step 4: Wire the new models into `LojinhaDbContext`**

In `Lojinha.Data/LojinhaDbContext.cs`, replace:

```csharp
    public DbSet<User> Users => Set<User>();
```

with:

```csharp
    public DbSet<User> Users => Set<User>();
    public DbSet<CaixaSessao> CaixaSessoes => Set<CaixaSessao>();
    public DbSet<MovimentoCaixa> MovimentosCaixa => Set<MovimentoCaixa>();
```

Then, inside `OnModelCreating`, replace:

```csharp
        modelBuilder.Entity<User>()
            .HasIndex(u => u.NomeUsuario)
            .IsUnique();
    }
```

with:

```csharp
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
```

- [ ] **Step 5: Generate the EF Core migration**

Run: `dotnet ef migrations add AddCaixaSessao --project Lojinha.Data`
Expected: no errors; two new files appear under `Lojinha.Data/Migrations/` (a new `..._AddCaixaSessao.cs` migration + matching `.Designer.cs`), and `Lojinha.Data/Migrations/LojinhaDbContextModelSnapshot.cs` is updated with the new tables.

- [ ] **Step 6: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 7: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 62 tests total (unchanged — this task adds no tests, just schema).

- [ ] **Step 8: Commit**

```bash
git add Lojinha.Data/Models/TipoMovimentoCaixa.cs Lojinha.Data/Models/CaixaSessao.cs Lojinha.Data/Models/MovimentoCaixa.cs Lojinha.Data/LojinhaDbContext.cs Lojinha.Data/Migrations
git commit -m "feat: add CaixaSessao/MovimentoCaixa models and migration"
```

---

