### Task 1: `User`/`PapelUsuario` models, `DbContext` wiring, `Sale.UsuarioNome`, migration

**Files:**
- Create: `Lojinha.Data/Models/PapelUsuario.cs`
- Create: `Lojinha.Data/Models/User.cs`
- Modify: `Lojinha.Data/LojinhaDbContext.cs`
- Modify: `Lojinha.Data/Models/Sale.cs`
- Migration: generated under `Lojinha.Data/Migrations/`

**Interfaces:**
- Produces: `PapelUsuario` enum (`Admin`, `Vendedor`); `User` (`Id`, `NomeUsuario`, `SenhaHash`, `SenhaSalt`, `Papel`); `LojinhaDbContext.Users` `DbSet`; `Sale.UsuarioNome` (`string?`) — consumed by Task 2 (`UserService`) and Task 7 (`SalesService`).

- [ ] **Step 1: Create the `PapelUsuario` enum**

Create `Lojinha.Data/Models/PapelUsuario.cs`:

```csharp
namespace Lojinha.Data.Models;

public enum PapelUsuario
{
    Admin,
    Vendedor
}
```

- [ ] **Step 2: Create the `User` model**

Create `Lojinha.Data/Models/User.cs`:

```csharp
namespace Lojinha.Data.Models;

public class User
{
    public int Id { get; set; }
    public required string NomeUsuario { get; set; }
    public required byte[] SenhaHash { get; set; }
    public required byte[] SenhaSalt { get; set; }
    public PapelUsuario Papel { get; set; }
}
```

- [ ] **Step 3: Add `Sale.UsuarioNome`**

In `Lojinha.Data/Models/Sale.cs`, add this property after `DataCancelamento`:

```csharp
    public string? UsuarioNome { get; set; }
```

- [ ] **Step 4: Wire `User` into `LojinhaDbContext`**

In `Lojinha.Data/LojinhaDbContext.cs`, add a `DbSet` after `SaleItems`:

```csharp
    public DbSet<User> Users => Set<User>();
```

Then, inside `OnModelCreating`, after the existing `SaleItem`/`Product` relationship configuration (before the closing brace of the method), add:

```csharp
        modelBuilder.Entity<User>()
            .HasIndex(u => u.NomeUsuario)
            .IsUnique();
```

- [ ] **Step 5: Generate the EF Core migration**

Run: `dotnet ef migrations add AddUsers --project Lojinha.Data`
Expected: no errors; two new files appear under `Lojinha.Data/Migrations/` (a new `..._AddUsers.cs` migration + matching `.Designer.cs`), and `Lojinha.Data/Migrations/LojinhaDbContextModelSnapshot.cs` is updated to include the `Users` table and `Sale.UsuarioNome` column.

- [ ] **Step 6: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 7: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 42 tests total (unchanged — this task adds no tests, just schema).

- [ ] **Step 8: Commit**

```bash
git add Lojinha.Data/Models/PapelUsuario.cs Lojinha.Data/Models/User.cs Lojinha.Data/Models/Sale.cs Lojinha.Data/LojinhaDbContext.cs Lojinha.Data/Migrations
git commit -m "feat: add User/PapelUsuario models, Sale.UsuarioNome, and migration"
```

---

