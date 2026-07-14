# Ergonomia do Caixa — Design Spec

**Goal:** Two small, independent ergonomics improvements to the Caixa tab of the Vendas/Caixa screen: an F2 keyboard shortcut to finalize a sale, and larger fonts/controls for faster, less error-prone operation during a live shift.

**Context:** Builds on the checkout-layout redesign and the Controle de Caixa feature (both already shipped). Quick-add product buttons were explicitly considered and rejected — every product must go through the barcode scanner, so a button-based add path doesn't match the real workflow and is out of scope here.

## Scope Decisions (from user)

- **No quick-add product buttons.** All items are added via barcode scan (or manual search as a fallback, unchanged).
- **F2 finalizes the sale**, active from any field on the Caixa tab (búsca, quantidade, desconto, valor recebido). Does not conflict with the existing `Return`→`EscanearCommand` binding on the busca field (different key).
- **Larger fonts/controls apply only to the Caixa tab** — Histórico and Turno are used occasionally, not for the whole shift, and stay as-is.

## F2 Shortcut

A `KeyBinding` is attached to the Caixa tab's own `Grid` (the one whose `Visibility` is already bound to `AbaAtiva == Caixa`) — not to the `UserControl` root. This is deliberate: WPF removes keyboard focus from a `Collapsed` element's subtree, so a `KeyBinding` scoped to that `Grid` can only ever fire while the Caixa tab is actually visible and something inside it holds focus. No ViewModel or code-behind change is needed — `FinalizarVendaCommand` already exists and already validates cart/session state, so F2 behaves identically to clicking "Finalizar venda": same error snackbars if the cart is empty or no caixa session is open, same success path otherwise.

```xml
<Grid Grid.Row="1" Visibility="...">
    <Grid.InputBindings>
        <KeyBinding Key="F2" Command="{Binding FinalizarVendaCommand}" />
    </Grid.InputBindings>
    ...
</Grid>
```

## Larger Fonts/Controls

Applied inline, matching this codebase's existing convention (no shared `Style` resources are used anywhere else in the app, so this doesn't introduce a new styling mechanism for one screen). Concrete values, applied only within the Caixa tab's `Grid`:

- Text inputs and combos (busca produto, quantidade, per-item desconto, sale-level desconto, forma de pagamento, valor recebido): `FontSize="16"` (up from the unset ~12-13px default), `Height="40"`.
- Buttons ("Adicionar ao carrinho", per-item delete, "Finalizar venda"): `FontSize="16"`, `Padding` increased proportionally (`"16,10"` instead of the current unset default).
- Cart `DataGrid`: `RowHeight="36"` (new), `FontSize="14"` (rows readable at a glance without shrinking column count — smaller than the 16px used on standalone controls because five columns must still fit the same width).
- Summary panel: field labels ("Desconto da venda", "Forma de pagamento", etc.) `FontSize="14"` (up from the unset default); the `Total` display goes from `FontSize="30"` to `FontSize="36"`; "Finalizar venda" button gets `FontSize="18"`, `Height="50"` (larger than other buttons — it's the single most-clicked/most-important action on the screen).

Histórico and Turno tabs, and every other screen in the app, are untouched.

## Testing

No automated UI tests (established project convention) — verified by `dotnet build` + a manual smoke run: confirm F2 finalizes a sale from each of the four input fields, confirm F2 does nothing when focus is on the Histórico or Turno tab, confirm the larger Caixa-tab controls render without clipping/overlap at the app's default window size.

## Out of Scope

- Quick-add product buttons (explicitly rejected — barcode-only workflow).
- Any change to Histórico, Turno, or any screen outside Vendas/Caixa.
- Any change to `SalesViewModel`, `SalesService`, or business logic — this is XAML-only.
