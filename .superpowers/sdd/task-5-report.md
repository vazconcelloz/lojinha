# Task 5 Report: Usuários screen

## Status: DONE

## What was implemented

Implemented the Usuários screen exactly per the brief, verbatim:

1. **Created** `Lojinha.App/ViewModels/UserViewModel.cs` — `ObservableObject` with `Usuarios` (ObservableCollection<User>), `Papeis` (PapelUsuario[]), `NomeUsuario`/`Senha`/`PapelSelecionado`/`EditandoId` observable properties, computed `EmEdicao`, and `Adicionar`/`Editar`/`Salvar`/`Cancelar`/`Excluir` (async, with confirmation dialog) relay commands, plus public `Refresh()`. Copied verbatim from the brief.
2. **Created** `Lojinha.App/Views/UsuarioView.xaml` — Card with Nome/Senha/Papel inputs and Adicionar/Salvar/Cancelar buttons gated by `EmEdicao` via `BooleanToVisibilityConverter` (with `ConverterParameter=Invert` for Adicionar), plus a DataGrid of users with Editar/Excluir row buttons, and an empty-state placeholder gated by `CountToVisibilityConverter`. Copied verbatim from the brief.
3. **Created** `Lojinha.App/Views/UsuarioView.xaml.cs` — trivial UserControl code-behind. Copied verbatim.
4. **Modified** `Lojinha.App/ViewModels/MainViewModel.cs` — added `UserViewModel Usuarios { get; }` property and constructor parameter, per brief (full-file replace as specified).
5. **Modified** `Lojinha.App/App.xaml.cs` — added `services.AddScoped<UserViewModel>();` right after `SalesViewModel` registration.
6. **Modified** `Lojinha.App/MainWindow.xaml` — added the 6th `NavigationViewItem` ("UsuariosItem", Content="Usuários", TargetPageTag="usuarios", `Click="NavigationViewItem_OnClick"`, Person24 icon) after VendasItem, before `</ui:NavigationView.MenuItems>`.
7. **Modified** `Lojinha.App/MainWindow.xaml.cs` — four changes, all present:
   - New field `private readonly UsuarioView _usuarioView = new();`
   - `UsuariosItem.IsActive = tag == "usuarios";` added to `NavigationViewItem_OnClick`
   - `"usuarios" => ((FrameworkElement)_usuarioView, (object)_viewModel.Usuarios),` added to the `NavigateTo` switch expression
   - `case "usuarios": _viewModel.Usuarios.Refresh(); break;` added to the `RefreshViewModel` switch

## Deviations from verbatim brief code

None. All code was copied exactly as specified in the brief. Verified against existing `UserService` (`Add`/`Update`/`Delete`/`GetAll` signatures), `User` model, and `PapelUsuario` enum (Task 2/earlier) before writing — signatures matched the brief's usage exactly, no adjustments needed.

## Build output

```
dotnet build
...
Compilação com êxito.
    2 Aviso(s)
    0 Erro(s)
```
The 2 warnings are pre-existing `CS0618` obsolete-API warnings on `IContentDialogService.SetContentPresenter` in `MainWindow.xaml.cs` line 26 (unrelated to this task's changes — present before this task too).

## Test output

```
dotnet test
...
Aprovado!  – Com falha:     0, Aprovado:    53, Ignorado:     0, Total:    53, Duração: 1 s
```
53/53 passing, matching expectation (this task is UI-only, no new service tests).

## Smoke-run observations

- Launched `dotnet run --project Lojinha.App` in background (task ID bs4etl649).
- After ~8s, confirmed via `tasklist //FI "IMAGENAME eq Lojinha.App.exe"` that `Lojinha.App.exe` (PID 31540) was running and stable — no crash, no exception dialog, memory usage ~209MB consistent with a normal WPF app sitting at the login window.
- Since I have no GUI-driving capability, I could not click through the login flow or the new Usuários nav item interactively. Instead I verified the navigation wiring by static code inspection (see below) and confirmed the process itself stays alive without throwing on startup (which would have surfaced as an immediate process exit or non-zero exit code before I could observe it in `tasklist`).
- Terminated cleanly: `taskkill //F //IM Lojinha.App.exe` → "processo... foi finalizado" (success). Confirmed via a follow-up `tasklist //FI "IMAGENAME eq Lojinha.App.exe"` returning "nenhuma tarefa em execução correspondente" (no matching process) — process fully gone.
- Note: the background task subsequently reported "failed / exit code 1" — this is expected and benign: it's the `dotnet run` wrapper process reporting a non-zero exit because I force-killed its child, not an application error.
- Re-ran `dotnet build Lojinha.App` afterward to confirm no stray file locks blocked a rebuild (a lesson-learned from prior tasks) — succeeded cleanly, 0 errors, confirming the process left no lock artifacts.

## Explicit confirmation of the three (four) navigation wiring points

Per the task's lesson #1, re-verified via grep on the final `MainWindow.xaml.cs`:

```
18:    private readonly UsuarioView _usuarioView = new();
44:        UsuariosItem.IsActive = tag == "usuarios";
58:            "usuarios" => ((FrameworkElement)_usuarioView, (object)_viewModel.Usuarios),
87:            case "usuarios":
                   _viewModel.Usuarios.Refresh();
                   break;
```

All four elements present and correctly wired:
1. `NavigationViewItem` in XAML has `Click="NavigationViewItem_OnClick"` wired (confirmed in `MainWindow.xaml`).
2. `TargetPageTag="usuarios"` has a matching case in the `NavigateTo` switch expression.
3. `TargetPageTag="usuarios"` has a matching case in the `RefreshViewModel` switch statement.
4. (Bonus wiring point specific to this pattern) `UsuariosItem.IsActive` toggle line present in `NavigationViewItem_OnClick`.

## Files changed

- `Lojinha.App/ViewModels/UserViewModel.cs` (new)
- `Lojinha.App/Views/UsuarioView.xaml` (new)
- `Lojinha.App/Views/UsuarioView.xaml.cs` (new)
- `Lojinha.App/ViewModels/MainViewModel.cs` (modified)
- `Lojinha.App/App.xaml.cs` (modified)
- `Lojinha.App/MainWindow.xaml` (modified)
- `Lojinha.App/MainWindow.xaml.cs` (modified)

Commit: `38f6786` — "feat: add Usuários screen (create/edit/delete users and roles)"

## Self-review findings

- All three (four) navigation wiring points present and correct — see above.
- Confirmed **no role-check was added** anywhere in this task's changes: grepped `MainWindow.xaml` for `Papel|Admin|Vendedor|IsAdmin|Role|Visibility` — no matches. The `UsuariosItem` nav entry is unconditionally visible to all users, exactly as intended (Task 6 will add the Admin-only gating; this task deliberately does not touch that).
- `UserViewModel.Editar` correctly clears `Senha` to empty (not populating with any plaintext, since none is available/stored anyway) — matches the documented "leave blank to keep current password" contract, and `Salvar` correctly passes `null` to `UserService.Update` when `Senha` is empty, preserving the existing password hash.
- `MainViewModel` constructor parameter order and property list match the brief exactly; DI registration order in `App.xaml.cs` places `UserViewModel` after `SalesViewModel` and before `MainViewModel`, consistent with existing pattern.

## Concerns

None. Build is clean (0 errors, only 2 pre-existing unrelated warnings), all 53 tests pass, the process starts and stays alive without crashing, and static analysis confirms all navigation wiring points and the deliberate absence of role-gating.
