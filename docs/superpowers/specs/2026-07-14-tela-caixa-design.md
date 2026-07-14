# Tela de Caixa — Design Spec

**Goal:** Redesign the Vendas screen into a proper checkout ("caixa") layout — a fixed, always-visible summary/payment panel instead of the current single-column form-like layout, plus a tab toggle to move sales history out of the main flow.

**Context:** Builds directly on the just-shipped Desconto e Troco feature. All the underlying `SalesViewModel` properties this redesign displays (`Total`, `Troco`, `EhDinheiro`, `DescontoVendaEntrada`, `TipoDescontoVenda`, `ValorRecebido`, `FormasPagamento`, `Carrinho`, `Historico`, etc.) already exist and are fully wired — this is a pure XAML restructuring of `Lojinha.App/Views/VendaView.xaml` plus one small tab-toggle addition to `SalesViewModel`. No `SalesService` changes, no new business logic, no migration.

Layout direction was validated visually with the user via mockup comparison (three layout options presented; user selected the fixed-side-panel layout, then approved a detailed mockup of the final structure).

## Layout

Two-column, no-scroll-on-the-panel structure, replacing the current single-column `ScrollViewer > StackPanel` of stacked cards:

```
┌─────────────────────────────────────────────────────┐
│  [Caixa]  [Histórico]                                │  ← tab toggle
├───────────────────────────────┬─────────────────────┤
│ 🔍 Buscar/escanear produto     │  RESUMO DA VENDA     │
│ [combo produto] [qtd] [+]      │  Desconto venda      │
│ ┌───────────────────────────┐ │  Pagamento           │
│ │ Carrinho (produto, qtd,   │ │  Valor recebido      │
│ │ preço, desconto, total)   │ │  ─────────────       │
│ │  (scrolls if tall)        │ │      R$ 47,90        │  ← big, centered
│ └───────────────────────────┘ │   Troco: R$ 2,10     │
│                                 │  [FINALIZAR VENDA]   │
└───────────────────────────────┴─────────────────────┘
```

- Left column (~2/3 width): product search/scan input + combo + quantity + add button (unchanged from today, just resized), and the cart `DataGrid` (unchanged columns: Produto, Quantidade, Preço unit., Desconto, Total item, remove button).
- Right column (~1/3 width): a fixed `ui:Card` containing, top to bottom: sale-level discount (type combo + amount), payment method combo, valor recebido (visible only when `EhDinheiro`), a divider, the `Total` in large bold centered text, `Troco` (visible only when `EhDinheiro`), and the "Finalizar venda" button. This column does not scroll — it's always fully visible regardless of cart length.
- When the "Histórico" tab is active, the two-column area is replaced by the existing histórico `DataGrid` (unchanged columns/behavior — Data, Total, Pagamento, Status, Vendedor, Desconto, Troco, Cancelar button), full width.

No active-tab visual highlight in this iteration (YAGNI — content switching itself signals which tab is active; can be added later if it turns out to be needed).

## SalesViewModel changes

One new piece of UI-only state, no business logic:

```csharp
[ObservableProperty]
private bool mostrandoHistorico;

[RelayCommand]
private void MostrarCaixa() => MostrandoHistorico = false;

[RelayCommand]
private void MostrarHistorico() => MostrandoHistorico = true;
```

Everything else the new layout binds to (`Total`, `Troco`, `EhDinheiro`, `TiposDesconto`, `TipoDescontoVenda`, `DescontoVendaEntrada`, `ValorRecebido`, `FormasPagamento`, `FormaPagamentoSelecionada`, `Carrinho`, `Historico`, `FinalizarVendaCommand`, `CancelarVendaCommand`) is unchanged — this redesign only moves where existing bindings appear in the XAML tree, it does not add, remove, or rename any of them.

## Navigation label

In `Lojinha.App/MainWindow.xaml`, the `NavigationViewItem`'s `Content="Vendas"` becomes `Content="Caixa"`. The internal `TargetPageTag="vendas"` and every code reference to the `"vendas"` tag string (in `MainWindow.xaml.cs`'s three tag-based switches), the `VendaView` class name, and the `SalesViewModel` class name are all left unchanged — renaming those would touch six-plus locations across two files for a purely cosmetic label change, with no functional benefit. Only the user-visible sidebar text changes.

## Out of Scope

- Any change to `SalesService`, discount/troco business logic, or the database.
- Quick-add buttons for frequent products (considered, explicitly deferred — current search/scan flow stays as-is).
- Active-tab visual highlighting.
- Touch/kiosk-style numeric keypad (this app is keyboard + barcode-scanner driven, not touchscreen).
