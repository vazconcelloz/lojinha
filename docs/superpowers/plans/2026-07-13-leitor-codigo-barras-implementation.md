# Leitor de Código de Barras Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On the Vendas screen, let a keyboard-emulating barcode scanner add the scanned product to the cart automatically (exact barcode match + Enter), without manual combo selection or an "Adicionar ao carrinho" click.

**Architecture:** A `KeyBinding` on the existing search `TextBox` (`TermoBusca`) fires a new `SalesViewModel.EscanearCommand` on Enter — pure MVVM, no new code-behind. The command looks up the product by exact `CodigoBarras` match, then either increments an existing cart line for that product or adds a new one, reusing the existing `Carrinho`/`ItemCarrinho` machinery untouched.

**Tech Stack:** .NET 8, WPF, WPF-UI 4.3.0, CommunityToolkit.Mvvm.

## Global Constraints

- No new server-side logic — this is entirely a `SalesViewModel`/`VendaView.xaml` change. `SalesService`/`StockService`/`ProductService` are untouched.
- No automated UI/ViewModel tests for this change (per spec, and consistent with the rest of the app — no ViewModel in this project has unit tests). Verification is `dotnet build` + a manual smoke run.
- All new/changed UI copy stays Portuguese, consistent with the rest of the app.
- The manual flow (`AdicionarAoCarrinhoCommand`, typing a partial name and picking from the combo) must keep working unchanged — the scan is an additional path, not a replacement.

---

### Task 1: `SalesViewModel.EscanearCommand` + `KeyBinding` wiring

**Files:**
- Modify: `Lojinha.App/ViewModels/SalesViewModel.cs`
- Modify: `Lojinha.App/Views/VendaView.xaml`

**Interfaces:**
- Consumes: `ProductService.GetAll()` (existing), `Carrinho`/`ItemCarrinho` (existing, from `Lojinha.App/ViewModels/SalesViewModel.cs`), `TermoBusca`/`Quantidade` (existing observable properties).
- Produces: `SalesViewModel.EscanearCommand` (`IRelayCommand`, no parameter) — bound from `VendaView.xaml`'s search `TextBox` via a `KeyBinding` on Enter.

- [ ] **Step 1: Add the `Escanear` method to `SalesViewModel`**

In `Lojinha.App/ViewModels/SalesViewModel.cs`, add this method after `AdicionarAoCarrinho`:

```csharp
    [RelayCommand]
    private void Escanear()
    {
        var codigo = TermoBusca.Trim();
        if (string.IsNullOrEmpty(codigo))
        {
            return;
        }

        var produto = _productService.GetAll().FirstOrDefault(p => p.CodigoBarras == codigo);
        if (produto is null)
        {
            _snackbar.Show("Erro", "Produto não encontrado.", ControlAppearance.Danger);
            TermoBusca = string.Empty;
            return;
        }

        var quantidadeAdicionar = Quantidade > 0 ? Quantidade : 1;

        var itemExistente = Carrinho.FirstOrDefault(i => i.ProductId == produto.Id);
        if (itemExistente is not null)
        {
            var index = Carrinho.IndexOf(itemExistente);
            Carrinho[index] = itemExistente with { Quantidade = itemExistente.Quantidade + quantidadeAdicionar };
        }
        else
        {
            Carrinho.Add(new ItemCarrinho(produto.Id, produto.Nome, quantidadeAdicionar, produto.PrecoVenda));
        }

        TermoBusca = string.Empty;
        Quantidade = 0;
    }
```

This matches by **exact** `CodigoBarras` (not the fuzzy `Search` used to filter the `Produtos` combo) — barcode is unique per product (existing unique index on `Product.CodigoBarras`), so exact match is correct and unambiguous. The quantity-to-add rule (`Quantidade > 0 ? Quantidade : 1`) applies uniformly to `Unidade` and `Peso` products, matching the spec (use whatever is in the Quantidade field; default to 1 if the user hasn't touched it). Replacing `Carrinho[index]` (not `Remove`+`Add`) raises `CollectionChanged` as a `Replace` action, which the constructor's existing `Carrinho.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Total));` subscription already handles — no new wiring needed for the `Total` display to stay correct.

- [ ] **Step 2: Wire the `KeyBinding` in `VendaView.xaml`**

In `Lojinha.App/Views/VendaView.xaml`, replace the search `TextBox`:

```xml
                        <ui:TextBox Width="220" Margin="0,0,8,8" PlaceholderText="Buscar produto (nome ou código)"
                                    Text="{Binding TermoBusca, UpdateSourceTrigger=PropertyChanged}" />
```

with:

```xml
                        <ui:TextBox Width="220" Margin="0,0,8,8" PlaceholderText="Buscar produto (nome ou código)"
                                    Text="{Binding TermoBusca, UpdateSourceTrigger=PropertyChanged}">
                            <ui:TextBox.InputBindings>
                                <KeyBinding Key="Return" Command="{Binding EscanearCommand}" />
                            </ui:TextBox.InputBindings>
                        </ui:TextBox>
```

`InputBindings` is inherited from `UIElement`, so this works on WPF-UI's `TextBox` the same as on a standard one — the `KeyBinding` only fires while this specific `TextBox` has focus (not a global shortcut), matching the spec.

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 32 tests total, 0 failures (this change touches no service-layer code, so the count should be unchanged from before this task).

- [ ] **Step 5: Manual smoke check**

Run: `dotnet run --project Lojinha.App`
Expected: on the Vendas screen, click into the search field and type a product's exact `CodigoBarras`, then press Enter (this simulates what a keyboard-emulating scanner does): the product is added to the carrinho with quantity 1 (if the Quantidade field was empty/0) and the search field clears. Press Enter again with the same code: the existing cart line's quantity increases by 1 instead of a new line appearing. Type a `Quantidade` value first (e.g. `2.5`), then scan a code: the new/incremented line uses `2.5`, and the Quantidade field resets to 0 after. Type a code that doesn't match any product and press Enter: an error snackbar "Produto não encontrado." appears and the field clears. Confirm the existing manual flow (partial name typed, product picked from the combo, "Adicionar ao carrinho" clicked) still works exactly as before. Close the app when done.

- [ ] **Step 6: Commit**

```bash
git add Lojinha.App/ViewModels/SalesViewModel.cs Lojinha.App/Views/VendaView.xaml
git commit -m "feat: add barcode scanner support to Vendas cart (scan + Enter to add/increment)"
```

---

### Task 2: Final check

**Files:** none (verification only).

- [ ] **Step 1: Full test suite**

Run: `dotnet test`
Expected: PASS, 32 tests total, 0 failures.

- [ ] **Step 2: Full build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 3: Push**

```bash
git push
```
