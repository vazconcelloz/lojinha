# Controle de Caixa — Design Spec

**Goal:** Add cash-register session control (abertura/fechamento de turno, sangria, suprimento, conferência) to the Vendas/Caixa screen. Registering a sale becomes gated on having an open session.

**Context:** Builds on the Tela de Caixa redesign (two-column layout, Caixa/Histórico tab toggle) and the Desconto e Troco feature (which introduced the `AutorizacaoWindow`/`IAuthorizationService` supervisor-override flow this design reuses). No changes to `Sale`/`SaleItem` — the session is joined to sales by time range, not a foreign key.

## Scope Decisions (from user)

- Opening a session becomes **mandatory** to register a sale — `FinalizarVenda` is blocked with an error message when no session is open.
- Any logged-in user opens/closes their own session (like picking up the drawer). **Sangria/suprimento require Admin authorization** — a Vendedor triggers the same `AutorizacaoWindow` used for discounts; an Admin self-authorizes.
- Closing requires **conferência**: the operator enters the counted cash amount, the system computes the expected amount and shows the difference (sobra/falta).
- **One open session at a time**, store-wide — this is a single-machine desktop app, so there's no need for per-user concurrent sessions. Shift handoff means closing one session and opening the next.
- UI: a **third tab** ("Turno") inside the existing Caixa screen, alongside Caixa and Histórico.

## Data Model

New EF Core migration, purely additive:

- `CaixaSessao`: `Id`, `DataAbertura` (`DateTime`), `ValorAbertura` (`decimal`), `UsuarioAbertura` (`string`), `DataFechamento` (`DateTime?`), `ValorContado` (`decimal?`), `ValorEsperado` (`decimal?`), `Diferenca` (`decimal?`), `UsuarioFechamento` (`string?`). A session is "open" when `DataFechamento is null`.
- `TipoMovimentoCaixa` enum: `Sangria`, `Suprimento`.
- `MovimentoCaixa`: `Id`, `CaixaSessaoId` (FK to `CaixaSessao`), `Tipo` (`TipoMovimentoCaixa`), `Valor` (`decimal`), `DataHora` (`DateTime`), `AutorizadoPor` (`string`), `Observacao` (`string?`).

`Sale` and `SaleItem` are **not** modified. A session's sales are found by time range (`Sale.DataHora` between `CaixaSessao.DataAbertura` and `DataFechamento` — or `DateTime.Now` for the currently-open session), which is unambiguous because only one session can be open at a time. This avoids touching `SalesService.RegisterSale`'s already-reviewed signature and avoids a new FK/migration risk on the `Sales` table.

## Expected-Cash Calculation

```
ValorEsperado = ValorAbertura
              + Σ(Sale.Total where FormaPagamento == Dinheiro, !Cancelada, DataHora in [abertura, agora/fechamento])
              + Σ(MovimentoCaixa.Valor where Tipo == Suprimento)
              - Σ(MovimentoCaixa.Valor where Tipo == Sangria)

Diferenca = ValorContado - ValorEsperado
```

Cancelled sales are excluded — a cancelled Dinheiro sale is assumed to have had its cash refunded, so it shouldn't count toward the drawer's expected contents. Cartão/Pix sales never touch this calculation (they don't move physical cash).

## CaixaService (new)

Mirrors `UserService`/`SalesService`'s existing conventions (Portuguese exception messages, `ArgumentException` for bad input, `InvalidOperationException` for invalid state transitions):

- `AbrirCaixa(decimal valorAbertura, string usuarioNome)` → `CaixaSessao`. Throws if a session is already open (`"Já existe um caixa aberto."`) or if `valorAbertura < 0` (`"Valor de abertura não pode ser negativo."`).
- `FecharCaixa(decimal valorContado, string usuarioNome)` → `CaixaSessao`. Like `RegistrarMovimento`, takes no session ID — there's at most one open session, so the service finds it itself via the same lookup `GetSessaoAberta()` uses. Throws if no session is open (`"Nenhum caixa aberto para fechar."`) or `valorContado < 0` (`"Valor contado não pode ser negativo."`). Computes `ValorEsperado`/`Diferenca` per the formula above and persists them alongside `DataFechamento`/`UsuarioFechamento`.
- `RegistrarMovimento(TipoMovimentoCaixa tipo, decimal valor, string autorizadoPor, string? observacao)` → `MovimentoCaixa`. Throws if no session is open (`"Nenhum caixa aberto."`) or `valor <= 0` (`"Valor do movimento deve ser maior que zero."`). Like `SalesService.RegisterSale`'s `autorizadoPor`, this service does **not** re-validate that `autorizadoPor` is an Admin — that's the ViewModel's job, consistent with the existing trust-boundary pattern.
- `GetSessaoAberta()` → `CaixaSessao?`. Returns the currently open session, or `null`.
- `GetMovimentos(int sessaoId)` → `IEnumerable<MovimentoCaixa>`. Ordered by `DataHora` descending, for display in the Turno tab.

## TurnoViewModel (new)

A dedicated ViewModel — cash-session management is a distinct responsibility from selling, and `SalesViewModel` has already grown across two prior features. `SalesViewModel` exposes it as a child: `public TurnoViewModel Turno { get; }`, constructor-injected via DI like every other service (`AddScoped<TurnoViewModel>()`).

State: `SessaoAberta` (`bool`, computed from whether `SessaoAtual` is non-null), `SessaoAtual` (`CaixaSessao?`), `ValorAberturaEntrada`/`ValorContadoEntrada`/`ValorMovimentoEntrada` (`decimal`, form inputs), `TipoMovimentoSelecionado` (`TipoMovimentoCaixa`), `Movimentos` (`ObservableCollection<MovimentoCaixa>`).

Commands:
- `AbrirCaixaCommand` — calls `CaixaService.AbrirCaixa`, reloads `SessaoAtual`/`Movimentos`.
- `RegistrarMovimentoCommand` — same Admin-authorization gate as `SalesViewModel.FinalizarVenda`: if the current user is Admin, self-authorize; otherwise call `IAuthorizationService.AutorizarDesconto()` (the existing method — this is a generic "get Admin sign-off" primitive, not discount-specific despite its name) and abort if it returns `null`. On success, calls `CaixaService.RegistrarMovimento`, reloads `Movimentos`.
- `FecharCaixaCommand` — calls `CaixaService.FecharCaixa`, shows the resulting `Diferenca` (sobra/falta) in a snackbar, reloads `SessaoAtual` (now `null`) and clears the movement list.

`TurnoViewModel` needs the same `IAuthorizationService`/`SessionService` dependencies `SalesViewModel` already has.

## SalesViewModel changes

- `MostrandoHistorico` (`bool`) is replaced by a new `AbaCaixa` enum (`Caixa`, `Historico`, `Turno`) and an `AbaAtiva` (`AbaCaixa`) property, since a third mutually-exclusive tab no longer fits a boolean. `MostrarCaixaCommand`/`MostrarHistoricoCommand` are updated to set `AbaAtiva` instead of a bool, and a new `MostrarTurnoCommand` is added.
- `FinalizarVenda`'s first check becomes: if `!Turno.SessaoAberta`, show `"Abra o caixa antes de registrar uma venda."` and return — before the existing empty-cart check, since without a session there's no point validating the cart.

## New converter: `EnumToVisibilityConverter`

The existing `BoolToVisibilityConverter`/`Invert` idiom doesn't extend to a 3-way enum. A new `IValueConverter` compares the bound enum value against `ConverterParameter` (a string matching the enum member name) and returns `Visibility.Visible` on match, `Collapsed` otherwise — e.g. `Visibility="{Binding AbaAtiva, Converter={StaticResource EnumToVisibilityConverter}, ConverterParameter=Turno}"`. Registered as an `App.xaml` resource alongside the other converters.

## VendaView.xaml changes

- Tab row gains a third `ui:Button` ("Turno"), bound to `MostrarTurnoCommand`.
- The Caixa `Grid` and Histórico `Card` visibility bindings switch from `MostrandoHistorico`+`Invert`/plain to `AbaAtiva`+`EnumToVisibilityConverter` with `ConverterParameter=Caixa` / `ConverterParameter=Historico` respectively.
- New Turno `Card` (same `Grid.Row="1"` slot, `ConverterParameter=Turno`), containing: session status header (aberto since HH:mm / fechado), abertura form (valor inicial + button) when no session is open, or when open: current `ValorAbertura`, a sangria/suprimento form (tipo combo + valor + Registrar button), the `Movimentos` list (`DataGrid`: DataHora, Tipo, Valor, AutorizadoPor, Observacao), and a fechamento form (valor contado + "Fechar caixa" button) that shows the resulting `Diferenca` after closing.

## Error Handling

Same established pattern throughout: `CaixaService` throws `ArgumentException`/`InvalidOperationException` with Portuguese messages, `TurnoViewModel`'s commands catch and show via `_snackbar.Show(..., ControlAppearance.Danger)`.

## Testing

- `CaixaServiceTests` (new test file, mirrors `SalesServiceTests`/`UserServiceTests` structure): `AbrirCaixa` succeeds when none open / throws when one is already open / throws on negative valor; `FecharCaixa` succeeds and computes `ValorEsperado`/`Diferenca` correctly (including a case with sangria and suprimento both present) / throws when none open / throws on negative valor contado; `RegistrarMovimento` succeeds for both Sangria and Suprimento / throws when no session open / throws on non-positive valor; `RegistrarMovimento` does not re-validate `autorizadoPor`'s role (same trust-boundary test pattern as `SalesService`); cancelled sales are excluded from `ValorEsperado`.
- No automated UI tests for `TurnoViewModel`/XAML — same established convention as every other WPF-facing task in this project, verified by build + manual smoke run.

## Out of Scope

- Reports/exports of caixa history beyond the in-app `Movimentos` list and the closed session's stored `Diferenca` (a future "Relatórios" feature, already on the backlog).
- Editing/deleting a `MovimentoCaixa` after it's registered.
- Reopening a closed session.
