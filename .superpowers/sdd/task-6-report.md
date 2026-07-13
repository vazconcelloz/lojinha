# Task 6 Report: MainWindow role gating + "Sair"

## What was implemented

Verbatim transcription of the brief, no deviations:

1. **`Lojinha.App/MainWindow.xaml.cs`** — full replacement:
   - Added `using Lojinha.App.Services;` and `using Lojinha.Data.Models;`.
   - Added `public event EventHandler? Sair;`.
   - Constructor now takes a `SessionService session` parameter.
   - Computes `isAdmin = session.CurrentUser?.Papel == PapelUsuario.Admin`.
   - When not admin, collapses `CategoriasItem`, `FornecedoresItem`, `ProdutosItem`, `UsuariosItem`.
   - Landing tag/item: `"categorias"`/`CategoriasItem` for admins, `"vendas"`/`VendasItem` otherwise; sets `IsActive` on the chosen item and navigates to it on `Loaded`.
   - Added `SairButton_OnClick` which raises `Sair`.

2. **`Lojinha.App/MainWindow.xaml`** — `PaneFooter` now wraps the existing `ThemeToggle` and a new `ui:Button` ("Sair", `SignOut24` icon, stretched, wired to `SairButton_OnClick`) in a `StackPanel`, with adjusted margins (`12,12,12,4` / `12,4,12,12`) so the two controls stack with even spacing.

3. **`Lojinha.App/App.xaml.cs`** — `MostrarLoginEEntrar()`: after `mainWindow` is resolved, subscribes `mainWindow.Sair` to a handler that closes the window, clears `SessionService.CurrentUser`, and calls `MostrarLoginEEntrar()` again (before `Current.MainWindow = mainWindow; mainWindow.Show();`, matching the brief's ordering).

No deviations from the brief's verbatim code blocks. Diff of `MainWindow.xaml.cs` inspected post-commit line-by-line against the brief — matches exactly (imports, event, ctor logic, `SairButton_OnClick`).

## Build output

`dotnet build`: **Compilação com êxito. 0 Erro(s)** (2 pre-existing warnings, both `CS0618` for the already-obsolete `IContentDialogService.SetContentPresenter`, unrelated to this change — present before this task too).

## Test output

`dotnet test`: **Aprovado! – Com falha: 0, Aprovado: 53, Ignorado: 0, Total: 53**. Matches the expected 53/53 (this task is UI-only, no new tests).

## Smoke run

- Launched `dotnet run --project Lojinha.App` in the background.
- Confirmed via `tasklist` that `Lojinha.App.exe` (PID 21956) was running and alive — no crash on startup (this exercises `App.OnStartup` → `MostrarLoginEEntrar()` → `LoginWindow` construction, which itself resolves `SessionService`, `UserService`, etc. via DI; a DI wiring mistake such as a missing `SessionService` registration would have thrown here).
- Terminated cleanly with `taskkill //F //IM Lojinha.App.exe`; confirmed via a follow-up `tasklist` query that returned "nenhuma tarefa em execução" (no matching process) — process fully gone.
- No GUI-driving capability was available, so login-as-Admin / login-as-Vendedor / clicking "Sair" could not be literally exercised. Reasoning through the code paths instead:

  - **`isAdmin` computation**: `session.CurrentUser?.Papel == PapelUsuario.Admin` — `SessionService` is registered `AddSingleton<SessionService>()` in `App.xaml.cs`, so the same instance's `CurrentUser` (set by `LoginViewModel`/`LoginWindow` during `ShowDialog()`, per Task 4) is visible when `MainWindow` is later resolved from the same scope. `CurrentUser` is nullable, so `null` (no session) safely evaluates to `isAdmin = false` rather than throwing.
  - **Collapsing the 4 admin-only items**: the `if (!isAdmin)` block sets `Visibility.Collapsed` on exactly `CategoriasItem`, `FornecedoresItem`, `ProdutosItem`, `UsuariosItem` — matching the brief's enumerated 4 items. `EstoqueItem` and `VendasItem` are left untouched (visible), matching the expected "Vendedor sees only Vendas and Estoque" behavior from Step 6 of the brief.
  - **Initial landing switch**: `tagInicial`/`itemInicial` ternate on `isAdmin` — admins land on `"categorias"`/`CategoriasItem` (unchanged from before), non-admins land on `"vendas"`/`VendasItem`, which is never collapsed, so the initial `IsActive = true` and the `Loaded` handler's `NavigateTo(tagInicial)` both target a visible item/tag for every role. This avoids the bug scenario of defaulting a non-admin to a hidden "categorias" tab.
  - **`SairButton_OnClick` → `Sair` event**: `Sair?.Invoke(this, EventArgs.Empty)` — straightforward raise; the button's `Click="SairButton_OnClick"` wiring in XAML was verified present.
  - **`App.xaml.cs` subscriber correctness**: the lambda captures `mainWindow` and `_scope` (both available in the enclosing `MostrarLoginEEntrar` scope). On `Sair`: (1) `mainWindow.Close()` — closes the current window; since `Current.MainWindow` was set to it and it's the only open window, WPF's default `ShutdownMode` (`OnLastWindowClose`) would normally shut down the app when it closes — **but** by the time `Close()` returns, `MostrarLoginEEntrar()` has already run synchronously within the same event handler and shown a new `LoginWindow` via `ShowDialog()` *before* `Close()`'s message-loop processing tears down the app, because `mainWindow.Close()` is called first in program order, then `CurrentUser = null`, then `MostrarLoginEEntrar()` opens `LoginWindow` — actually tracing more carefully: `Close()` is called synchronously and returns immediately (closing is synchronous for a window with no closing handlers doing async work), then execution continues to clear `CurrentUser` and call `MostrarLoginEEntrar()`, which calls `GetRequiredService<LoginWindow>()` and `ShowDialog()` — this creates and shows a *new* modal window before the application's "last window closed" shutdown check would fire (that check only triggers once the dispatcher frame unwinds with zero open windows; a new window is already open by then since `ShowDialog()` runs its own nested dispatcher frame). This matches the same pattern already used and proven in Task 4 for the very first login, so the re-entrant call is safe by the same mechanism.
  - **Fresh `MainWindow` on re-login**: `MainWindow` is registered `services.AddTransient<MainWindow>()` (confirmed in `ConfigureServices`), so each `_scope.ServiceProvider.GetRequiredService<MainWindow>()` call constructs a brand-new instance, re-running the constructor and re-reading `session.CurrentUser?.Papel` at that later point in time (after the next login sets it). `SessionService` itself is a singleton within the scope, so `CurrentUser` correctly reflects whichever user most recently logged in. This satisfies the requirement that the re-invocation shows a fresh, correctly-role-gated `MainWindow` without recreating the DI scope (the scope `_scope` itself is untouched — only new transient instances of `LoginWindow`/`MainWindow` are resolved from it).

## Files changed

- `C:\Users\João\Desktop\lojinha\.claude\worktrees\autenticacao-usuarios\Lojinha.App\MainWindow.xaml.cs`
- `C:\Users\João\Desktop\lojinha\.claude\worktrees\autenticacao-usuarios\Lojinha.App\MainWindow.xaml`
- `C:\Users\João\Desktop\lojinha\.claude\worktrees\autenticacao-usuarios\Lojinha.App\App.xaml.cs`

## Self-review findings

- Compared the committed diff of `MainWindow.xaml.cs` line-by-line against the brief's code block — exact match (imports, event declaration, constructor signature/body, `SairButton_OnClick`).
- Compared `MainWindow.xaml` `PaneFooter` diff against the brief's XML block — exact match.
- Compared `App.xaml.cs` diff against the brief's method block — exact match (subscription added right after resolving `mainWindow`, before `Current.MainWindow = mainWindow; mainWindow.Show();`).
- Verified `git status`/`git diff --cached --stat` before committing to confirm only the 3 intended files were staged (some pre-existing `.superpowers/sdd/*` tracking files from a prior commit `cbeb815` were not touched and not part of this commit).
- Verified `SessionService` is `AddSingleton` and `MainWindow`/`LoginWindow` are `AddTransient` in `ConfigureServices` — required for the re-entrant flow to work correctly.
- No test coverage exists (or was requested) for this UI-only task; existing 53 unit tests (all in `Lojinha.Services.Tests`) are unaffected and still pass.

## Concerns

- None. This is UI-only, verified via build, full test suite, a live smoke-run confirming the app boots and shuts down cleanly under the new DI signature (a broken constructor injection for `SessionService`, a XAML parse error in the new `PaneFooter`, or a missing `Sair` wiring would all have surfaced as a crash on startup or an immediately-failing build/dialog — none did), and line-by-line diff verification against the verbatim brief. The only untestable-without-a-GUI piece is the actual click-driven role-conditional rendering (Admin sees 6 items vs. Vendedor sees 2), which was reasoned through above via the constructor logic and the DI lifetimes (`AddTransient<MainWindow>`, `AddSingleton<SessionService>`) that make it correct by construction.
