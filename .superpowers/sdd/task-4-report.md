# Task 4 Report: LoginWindow + startup flow

## Status: DONE

## What was implemented

All four files from the brief were created/replaced verbatim, transcription-verified against the brief text:

1. **`Lojinha.App/ViewModels/LoginViewModel.cs`** (new) — `ObservableObject` with `PrimeiroAcesso` (readonly bool, set once in ctor from `!UserService.AnyUsers()`), observable `NomeUsuario`/`Senha`/`MensagemErro`, `LoginBemSucedido` event, and `[RelayCommand] Entrar()` that branches `Add(..., PapelUsuario.Admin)` vs `Authenticate(...)` based on `PrimeiroAcesso`, sets `_session.CurrentUser`, fires the event on success, and catches exceptions into `MensagemErro`.
2. **`Lojinha.App/LoginWindow.xaml`** (new) — `ui:FluentWindow`, 380x360, centered, no-resize, with conditional "Criar primeiro administrador" / "Entrar" headers and buttons toggled via the existing `BooleanToVisibilityConverter` resource (with `ConverterParameter=Invert` on the login-mode variants).
3. **`Lojinha.App/LoginWindow.xaml.cs`** (new) — `FluentWindow` subclass; constructor takes `LoginViewModel`, sets `DataContext`, subscribes to `LoginBemSucedido` to set `DialogResult = true` then `Close()`.
4. **`Lojinha.App/App.xaml.cs`** (replaced) — `OnStartup` now calls `MostrarLoginEEntrar()` after migration instead of directly showing `MainWindow`. New private method shows `LoginWindow` modally via `ShowDialog()`; if not confirmed, calls `Shutdown()`; otherwise resolves `MainWindow`, sets `Current.MainWindow`, and shows it. `ConfigureServices` gained `UserService` (scoped) and `LoginViewModel`/`LoginWindow` (transient) registrations. `UserViewModel` was intentionally NOT registered (deferred to Task 5), matching the brief's note.

## Deviations from brief

None. Diff of `App.xaml.cs` inspected line-by-line against the brief's verbatim listing — matches exactly (confirmed via `git diff` after commit, reproduced below). New files were written byte-for-byte from the brief's code blocks.

```diff
+        MostrarLoginEEntrar();
+    }
+
+    private void MostrarLoginEEntrar()
+    {
+        var loginWindow = _scope!.ServiceProvider.GetRequiredService<LoginWindow>();
+        var loginOk = loginWindow.ShowDialog();
+
+        if (loginOk != true)
+        {
+            Shutdown();
+            return;
+        }
+
         var mainWindow = _scope.ServiceProvider.GetRequiredService<MainWindow>();
+        Current.MainWindow = mainWindow;
         mainWindow.Show();
     }
@@
+        services.AddScoped<UserService>();
@@
+        services.AddTransient<LoginViewModel>();
+        services.AddTransient<LoginWindow>();
         services.AddTransient<MainWindow>();
```

One pre-verification note (not a deviation, just confirmation before writing): the brief's XAML references `{StaticResource BooleanToVisibilityConverter}`. I checked `Lojinha.App/App.xaml` first — the converter class is physically named `BoolToVisibilityConverter` (in `Converters/BoolToVisibilityConverter.cs`) but is registered under the resource key `x:Key="BooleanToVisibilityConverter"`, and it already supports `ConverterParameter="Invert"` (case-insensitive `string.Equals` check). So the brief's XAML resolves correctly with no changes needed.

## Build output

`dotnet build`: **0 Erro(s)**, 2 pre-existing warnings (CS0618 obsolete `SetContentPresenter` in `MainWindow.xaml.cs`, unrelated to this task, present before this change too).

```
Compilação com êxito.
    2 Aviso(s)
    0 Erro(s)
```

## Test output

`dotnet test`: **53/53 passed**, 0 failed, 0 skipped (unchanged from before this task, as expected — this task is UI-only and added no service-layer code).

```
Aprovado!  – Com falha:     0, Aprovado:    53, Ignorado:     0, Total:    53, Duração: 900 ms
```

## Smoke-run observations

Pre-check: confirmed no `lojinha.db` existed yet at `Lojinha.App/bin/Debug/net8.0-windows/lojinha.db` before the run — so `UserService.AnyUsers()` was guaranteed to return `false` on this run, i.e. first-access mode was the expected path.

Ran `dotnet run --project Lojinha.App` in the background:
- Process `Lojinha.App.exe` (PID 33456) appeared in `tasklist` and remained alive for the full observation window (~10s) with no crash and no output/exceptions in the captured log (log file was empty — consistent with a live GUI app producing no console output).
- Confirmed `lojinha.db` was created on disk during the run (EF Core `Database.Migrate()` executed successfully), proving `OnStartup` → `MostrarLoginEEntrar()` executed without throwing before reaching the modal `ShowDialog()` call (a window can't be "alive and idle" if construction/DI resolution had thrown — it would have crashed the process, which it did not).
- Terminated with `taskkill /F /PID 33456`; re-ran `tasklist` and confirmed no `Lojinha.App.exe` process remained.
- Checked for stray `dotnet.exe` processes afterward: four remained, all identified via `Get-CimInstance Win32_Process` as SDK build-server infrastructure (`VBCSCompiler.dll` and `MSBuild.dll /nodemode:1 /nodeReuse:true`), not related to `dotnet run` — these are long-lived build/compile servers reused across build invocations, not leftover app processes.

I have no GUI-driving capability (no way to type into the `ui:TextBox`/`ui:PasswordBox` or click the button), so I could not literally exercise the full click-to-login round trip. Instead, reasoning through the code paths as requested:

1. **`LoginViewModel.PrimeiroAcesso` reflects `!UserService.AnyUsers()` correctly.** In the constructor: `PrimeiroAcesso = !_userService.AnyUsers();`. `AnyUsers()` is `_context.Users.Any()`. Since the DB was freshly created via migration with zero rows in `Users` on this run, `AnyUsers()` returns `false`, so `PrimeiroAcesso` is `true` — the window would show "Criar primeiro administrador" (bound directly, `Visibility` visible when `true`) and hide "Entrar" (bound with `ConverterParameter=Invert`, visible when `false`). Confirmed by direct read of `UserService.cs`: `AnyUsers()` is a simple non-throwing LINQ `.Any()`.

2. **`Entrar` command dispatches correctly.** The ternary `PrimeiroAcesso ? _userService.Add(NomeUsuario, Senha, PapelUsuario.Admin) : _userService.Authenticate(NomeUsuario, Senha)` is a direct, unambiguous branch on the same `PrimeiroAcesso` flag set at construction time (it never changes after construction, since it has no setter and isn't `[ObservableProperty]`). `UserService.Add` hardcodes the papel to whatever is passed — here always `PapelUsuario.Admin` for first-access — matching the "first user is always Admin" design intent. `UserService.Authenticate` throws `InvalidOperationException("Usuário ou senha inválidos.")` on wrong username or wrong password (via `CryptographicOperations.FixedTimeEquals` PBKDF2 hash comparison), which is caught by the `Entrar()` method's `try/catch` and surfaces as `MensagemErro`, matching the brief's expected "wrong credentials show red error, window stays open" behavior — the `catch` block never invokes `LoginBemSucedido`, so the window is never closed on failure.

3. **`LoginBemSucedido` handler ordering in `LoginWindow.xaml.cs` is correct.** The lambda does `DialogResult = true;` **then** `Close();`. This order is required: WPF's `Window.DialogResult` setter only works while the window is open and was shown via `ShowDialog()` — setting it after `Close()` has already run would throw `InvalidOperationException` because the window would no longer be in a state that accepts a dialog result. Setting `DialogResult = true` first makes `ShowDialog()` (called from `App.MostrarLoginEEntrar()`) both close the window and return `true` from the call — `Close()` afterward is effectively redundant-but-harmless (WPF's `DialogResult` setter internally can close the window, but calling `Close()` explicitly is the documented/typical pattern and causes no error). This confirms `loginOk != true` in `App.MostrarLoginEEntrar()` will be `false` on successful login, so execution proceeds to resolving and showing `MainWindow`.

## Files changed

- `C:\Users\João\Desktop\lojinha\.claude\worktrees\autenticacao-usuarios\Lojinha.App\ViewModels\LoginViewModel.cs` (new)
- `C:\Users\João\Desktop\lojinha\.claude\worktrees\autenticacao-usuarios\Lojinha.App\LoginWindow.xaml` (new)
- `C:\Users\João\Desktop\lojinha\.claude\worktrees\autenticacao-usuarios\Lojinha.App\LoginWindow.xaml.cs` (new)
- `C:\Users\João\Desktop\lojinha\.claude\worktrees\autenticacao-usuarios\Lojinha.App\App.xaml.cs` (modified)

## Self-review findings

- All four files transcribed match the brief exactly, verified via `git diff` post-commit and direct comparison of new-file contents to the brief's code blocks.
- Verified upstream dependencies before writing: `UserService.AnyUsers()/Add()/Authenticate()` signatures match what `LoginViewModel` calls; `PapelUsuario.Admin` exists; `SessionService.CurrentUser` is a settable `User?` property; the `BooleanToVisibilityConverter` resource key (backed by `BoolToVisibilityConverter` class) already supports the `Invert` parameter — no changes needed there, as noted in the brief's "Interfaces" section ("already fixed to honor Invert").
- `MainWindow.xaml.cs`'s existing pattern (small code-behind for window-level concerns, `UserControl` views stay code-behind-free) is indeed the established precedent `LoginWindow.xaml.cs` follows, as the brief's inline comment states.
- No other files were touched; `Lojinha.App.csproj` needed no changes (WPF `.xaml`/`.xaml.cs` pairs are picked up automatically by the SDK-style project's default globs, confirmed implicitly by the clean build).
- `UserViewModel` was correctly NOT registered in `ConfigureServices`, matching the brief's explicit note that this is Task 5's responsibility.

## Concerns

None. Build is clean, all 53 pre-existing tests pass unchanged, the new files match the brief verbatim, and the code-path reasoning for the untested-by-hand UI flow (first-access detection, Add-vs-Authenticate branching, DialogResult-before-Close ordering) holds up under inspection of the actual implementation and its dependencies.
