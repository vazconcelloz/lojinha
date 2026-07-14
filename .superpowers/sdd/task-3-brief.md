### Task 3: `SessionService`

**Files:**
- Create: `Lojinha.App/Services/SessionService.cs`
- Modify: `Lojinha.App/App.xaml.cs`

**Interfaces:**
- Produces: `SessionService.CurrentUser` (`User?`, settable) — registered as a DI singleton, consumed by Task 4 (`LoginWindow`), Task 6 (`MainWindow` role gating), Task 7 (`SalesViewModel`), Task 8 (`StockViewModel`).

- [ ] **Step 1: Create `SessionService`**

Create `Lojinha.App/Services/SessionService.cs`:

```csharp
using Lojinha.Data.Models;

namespace Lojinha.App.Services;

public class SessionService
{
    public User? CurrentUser { get; set; }
}
```

- [ ] **Step 2: Register it in DI**

In `Lojinha.App/App.xaml.cs`, add `using Lojinha.App.Services;` to the usings, then in `ConfigureServices`, add this line right after `services.AddSingleton<IContentDialogService, ContentDialogService>();`:

```csharp
        services.AddSingleton<SessionService>();
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 4: Commit**

```bash
git add Lojinha.App/Services/SessionService.cs Lojinha.App/App.xaml.cs
git commit -m "feat: add SessionService"
```

---

