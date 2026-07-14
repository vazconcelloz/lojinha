### Task 3: Sidebar label rename and final integration check

**Files:**
- Modify: `Lojinha.App/MainWindow.xaml`

**Interfaces:** none (this task only touches a display string; see Global Constraints for what stays unchanged).

- [ ] **Step 1: Rename the sidebar label**

In `Lojinha.App/MainWindow.xaml`, replace:

```xml
                <ui:NavigationViewItem x:Name="VendasItem" Content="Vendas" TargetPageTag="vendas"
```

with:

```xml
                <ui:NavigationViewItem x:Name="VendasItem" Content="Caixa" TargetPageTag="vendas"
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 3: Full test suite**

Run: `dotnet test`
Expected: PASS, 62 tests total, 0 failures.

- [ ] **Step 4: End-to-end manual walkthrough**

Run: `dotnet run --project Lojinha.App` and, in one session:

1. Confirm the sidebar shows "Caixa" (not "Vendas") for both Admin and Vendedor roles.
2. Log in as Admin. Confirm the Caixa screen shows the two-column layout: search/cart on the left, the "RESUMO DA VENDA" panel fixed on the right with Desconto da venda, Forma de pagamento, Total (large, centered), and "Finalizar venda".
3. Add several items to the cart (enough to make the cart list scroll) — confirm the right-hand summary panel stays fully visible and doesn't move or get pushed off-screen.
4. Select "Dinheiro" — confirm the "Valor recebido" field and "Troco" text appear in the summary panel; select "Cartão" or "Pix" — confirm they disappear.
5. Click "Histórico" — confirm the two-column Caixa layout is replaced by the histórico grid (Data, Total, Pagamento, Status, Vendedor, Desconto, Troco, Cancelar). Click "Caixa" — confirm it switches back, and any items still in the cart from step 3 are still there (switching tabs must not clear the cart).
6. Finalize a sale with a discount as Admin — confirm it still works exactly as before (no authorization prompt, since Admin self-authorizes) — the discount/authorization logic itself is untouched by this plan, only its on-screen position moved.
7. Log out, log in as a Vendedor, repeat step 6 with a discount — confirm the `AutorizacaoWindow` still appears and the flow completes as before.

- [ ] **Step 5: Commit**

```bash
git add Lojinha.App/MainWindow.xaml
git commit -m "feat: rename Vendas nav label to Caixa"
```

- [ ] **Step 6: Push**

```bash
git push
```
