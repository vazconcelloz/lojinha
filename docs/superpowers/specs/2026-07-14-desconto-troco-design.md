# Desconto e Troco — Design Spec

**Goal:** Add per-item and per-sale discounts (currency or percentage) to the Vendas screen, plus received-amount/change tracking for cash sales. Vendedor-applied discounts require on-the-spot Admin authorization (a supervisor-override pattern), matching the role model established in the Autenticação/Usuários feature.

**Context:** Builds on the existing Vendas module (`SalesViewModel`, `SalesService`, `VendaView.xaml`) and the Admin/Vendedor role system (`SessionService`, `UserService`, `PapelUsuario`). This is a self-contained increment — it does not touch cash-register-session tracking (abertura/fechamento de caixa), which remains a separate future feature.

## Data Model

New EF Core migration, additive and backward-compatible (existing rows get `0`/`null` defaults, consistent with the `Sale.UsuarioNome` precedent from the auth feature):

- `SaleItem.DescontoValor` (`decimal`, not null, default `0`) — the discount actually applied to this item, always stored as a currency amount. If the operator entered a percentage, the currency-equivalent is computed once at registration time and stored; the input mode itself is not persisted.
- `Sale.DescontoValor` (`decimal`, not null, default `0`) — sale-level discount, same currency-amount convention.
- `Sale.ValorRecebido` (`decimal?`, nullable) — amount tendered by the customer. Only meaningful (and only required) when `FormaPagamento == Dinheiro`; `null` for Cartao/Pix.
- `Sale.Troco` (`decimal?`, nullable) — `ValorRecebido - Total`, computed and stored at registration time for the same Dinheiro-only condition.
- `Sale.AutorizadoPor` (`string?`, nullable) — username of the Admin who authorized the discount. Set whenever any discount (item or sale level) is greater than zero, whether the selling user was already an Admin (self-authorized, no prompt) or a Vendedor who got a supervisor override. `null` when no discount was applied.

`Sale.Total` keeps its existing meaning: the final, post-discount amount. No separate subtotal column — the pre-discount subtotal is always recomputable from `SaleItem.Quantidade * PrecoUnitario`.

## Discount Calculation Order

Item discounts apply first, against each item's own `Quantidade * PrecoUnitario`. The resulting item subtotals are summed into a cart subtotal. The sale-level discount then applies against that cart subtotal, producing the final `Total`. Each level (item, sale) independently chooses currency (R$) or percentage (%) as its input mode — the two levels are not required to match.

```
ItemSubtotal = Quantidade * PrecoUnitario
ItemDesconto = tipo == Percentual ? ItemSubtotal * entrada / 100 : entrada
ItemSubtotalComDesconto = ItemSubtotal - ItemDesconto

CarrinhoSubtotal = sum(ItemSubtotalComDesconto)
VendaDesconto = tipo == Percentual ? CarrinhoSubtotal * entrada / 100 : entrada
Total = CarrinhoSubtotal - VendaDesconto

Troco = FormaPagamento == Dinheiro ? ValorRecebido - Total : null
```

## SalesService

`RegisterSale` signature changes to carry the new inputs:

```csharp
public Sale RegisterSale(
    IEnumerable<(int ProductId, decimal Quantidade, decimal DescontoItem)> itens,
    FormaPagamento formaPagamento,
    string? usuarioNome = null,
    decimal descontoVenda = 0,
    decimal? valorRecebido = null,
    string? autorizadoPor = null)
```

Validation, following the project's existing convention (`ArgumentException`/`InvalidOperationException` with a Portuguese, user-facing message):

- Each `DescontoItem` must be `>= 0` and `<=` that item's subtotal — else `"Desconto do item não pode ser maior que o subtotal."`
- `descontoVenda` must be `>= 0` and `<=` the cart subtotal (after item discounts) — else `"Desconto da venda não pode ser maior que o subtotal."`
- If `formaPagamento == Dinheiro`: `valorRecebido` must be provided and `>= Total` — else `"Valor recebido é obrigatório e deve ser maior ou igual ao total."` `Troco` is computed and stored.
- If `formaPagamento != Dinheiro`: `valorRecebido`/`Troco` are ignored/stored as `null`, regardless of what's passed.

`SalesService` does **not** re-verify that `autorizadoPor` names an actual Admin. Role gating is a UI-layer concern in this codebase already (see `PodeCancelarVenda`, `PodeGerenciarEstoque` — both UI-visibility only, no service-layer role check, an accepted tradeoff from the auth feature's final review for this local-desktop threat model). `SalesService` persists whatever `autorizadoPor` string it's given as an audit trail; the `SalesViewModel` is responsible for only calling it with a value obtained from a genuine authorization.

## Admin Authorization Flow

New `AutorizacaoWindow` (+ `AutorizacaoViewModel`), mirroring the existing `LoginWindow`/`LoginViewModel` pattern exactly (transient DI registration, modal `ShowDialog()`):

- Fields: `NomeUsuario`, `Senha` (`ui:PasswordBox`, bound `Mode=TwoWay` — the same binding this session just fixed on `LoginWindow`/`UsuarioView`, must not regress).
- `AutorizarCommand` calls `UserService.Authenticate(NomeUsuario, Senha)`. On success, if the returned user's `Papel != PapelUsuario.Admin`, shows `"Apenas administradores podem autorizar desconto."` and does not close. On success with `Papel == Admin`, the window closes with `DialogResult = true` and exposes the authorizing admin's `NomeUsuario` for the caller to read.
- Does **not** touch `SessionService.CurrentUser` — the Vendedor's session is untouched; this is a one-time check, not a login.

New `AuthorizationService` (in `Lojinha.App/Services`, alongside `SessionService`), injected into `SalesViewModel` the same way `ISnackbarService`/`IContentDialogService` already are:

```csharp
public interface IAuthorizationService
{
    string? AutorizarDesconto(); // resolves AutorizacaoWindow via DI, ShowDialog(), returns admin username or null if cancelled/failed
}
```

## SalesViewModel / ItemCarrinho

`ItemCarrinho` changes from an immutable `record` to an observable class (`ObservableObject`, matching the `[ObservableProperty]` convention used everywhere else in this codebase — e.g. `LoginViewModel`, `UserViewModel`). This is a targeted change to existing code, needed because inline per-item discount editing requires the cart row itself to raise property-changed notifications; the current `record`-plus-`with`-replace approach (used today only for quantity increments in `Escanear`) doesn't support editing without replacing the whole collection entry, and the cart total needs to react live as the operator types a discount.

New/changed members:

- `ItemCarrinho`: adds `DescontoTipo` (`TipoDesconto`, default `Valor`) and `DescontoEntrada` (`decimal`, default `0`). Computed `DescontoAplicado` and `SubtotalComDesconto` re-raise on changes to `Quantidade`, `PrecoUnitario`, `DescontoTipo`, or `DescontoEntrada`.
- New shared enum `TipoDesconto { Valor, Percentual }` in `Lojinha.Data.Models` (used at both item and sale level).
- `SalesViewModel` adds `TipoDescontoVenda`, `DescontoVendaEntrada`, `ValorRecebido` (all `[ObservableProperty]`), and computed `CarrinhoSubtotal`, `DescontoVendaAplicado`, `Total` (replacing the current plain sum), `Troco`, and `EhDinheiro` (`FormaPagamentoSelecionada == FormaPagamento.Dinheiro`, re-raised in the existing `[ObservableProperty]`-generated partial change hook for `FormaPagamentoSelecionada`). `EhDinheiro` gates the Valor Recebido/Troco fields' visibility via the existing `BoolToVisibilityConverter` — no new converter needed.
- Cart-item property changes (not just collection add/remove) must now propagate to `Total`: `SalesViewModel` subscribes to each `ItemCarrinho.PropertyChanged` as items are added to `Carrinho` (and unsubscribes on removal), re-raising `Total` on relevant changes — the existing `Carrinho.CollectionChanged` handler alone only covers add/remove/replace, not in-place edits.
- `FinalizarVenda`: after the existing empty-cart check, computes whether any discount is present. If so and `_session.CurrentUser?.Papel != PapelUsuario.Admin`, calls `IAuthorizationService.AutorizarDesconto()`; a `null` result (cancelled or failed) aborts the sale with no service call. If the current user is already Admin, `autorizadoPor` is set to their own username with no prompt. Otherwise (no discount at all), `autorizadoPor` stays `null`. Passes the new fields through to `RegisterSale`; on success, resets discount/`ValorRecebido` fields alongside the existing cart-clear.
- `VendaHistoricoItem` gains `DescontoValor`, `ValorRecebido`, `Troco`, `AutorizadoPor` for the histórico display.

## VendaView.xaml

- Cart `DataGrid` gains two columns: discount type (`ComboBox` bound to `TipoDesconto`, matching the existing `FormaPagamento` combo pattern) and discount amount (`TextBox`), per row — editable inline, same visual weight as the existing quantity/price columns.
- Near the `Total` display: sale-level discount type + amount fields, following the same control style.
- `Valor Recebido` field and a read-only `Troco` display, both wrapped in `Visibility="{Binding EhDinheiro, Converter={StaticResource BooleanToVisibilityConverter}}"`.
- Histórico `DataGrid` gains a `Desconto` column (sum of item + sale discount, or just the two if space allows) and a `Troco` column, matching the existing `Vendedor` column added by the auth feature.

## Error Handling

No new UI validation surface — the existing pattern is kept: `SalesService` throws `ArgumentException`/`InvalidOperationException` with a Portuguese message, `FinalizarVenda`'s existing `try`/`catch` shows it via `_snackbar.Show(..., ControlAppearance.Danger)`. The authorization window has its own inline `MensagemErro` (identical to `LoginWindow`'s pattern) for auth-specific failures (wrong credentials, non-Admin account).

## Testing

- `SalesServiceTests`: item-discount-exceeds-subtotal throws; sale-discount-exceeds-subtotal throws; Dinheiro without `valorRecebido` throws; Dinheiro with `valorRecebido < Total` throws; `Troco` computed correctly for a valid Dinheiro sale; non-Dinheiro sale stores `null` for `ValorRecebido`/`Troco` even if a value were passed; `AutorizadoPor` is persisted as given, with a test documenting the no-revalidation trust boundary (service accepts a Vendedor-authored `autorizadoPor` string, confirming the boundary is enforced by the ViewModel, not the service).
- No automated UI tests for `AutorizacaoWindow`, the inline discount editing, or the Troco display — consistent with this project's established convention (frontend verified by `dotnet build` + manual smoke run, as in every prior WPF-facing task in the auth feature).

## Out of Scope

- Cash-register session tracking (abertura/fechamento de caixa, sangria/suprimento) — separate future feature, noted as a P0 gap alongside this one but independent in scope.
- Split/mixed payment methods per sale.
- Partial item returns/refunds.
