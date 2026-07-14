### Task 1: `SalesViewModel` tab-toggle state

**Files:**
- Modify: `Lojinha.App/ViewModels/SalesViewModel.cs`

**Interfaces:**
- Produces: `SalesViewModel.MostrandoHistorico` (`bool`), `MostrarCaixaCommand`, `MostrarHistoricoCommand` — consumed by Task 2 (`VendaView.xaml`).

- [ ] **Step 1: Add the `MostrandoHistorico` property**

In `Lojinha.App/ViewModels/SalesViewModel.cs`, replace:

```csharp
    [ObservableProperty]
    private decimal valorRecebido;

    public decimal CarrinhoSubtotal => Carrinho.Sum(i => i.SubtotalComDesconto);
```

with:

```csharp
    [ObservableProperty]
    private decimal valorRecebido;

    [ObservableProperty]
    private bool mostrandoHistorico;

    public decimal CarrinhoSubtotal => Carrinho.Sum(i => i.SubtotalComDesconto);
```

- [ ] **Step 2: Add the tab-toggle commands**

Replace:

```csharp
    [RelayCommand]
    private void RemoverDoCarrinho(ItemCarrinho item)
    {
        Carrinho.Remove(item);
    }

    [RelayCommand]
    private void FinalizarVenda()
```

with:

```csharp
    [RelayCommand]
    private void RemoverDoCarrinho(ItemCarrinho item)
    {
        Carrinho.Remove(item);
    }

    [RelayCommand]
    private void MostrarCaixa()
    {
        MostrandoHistorico = false;
    }

    [RelayCommand]
    private void MostrarHistorico()
    {
        MostrandoHistorico = true;
    }

    [RelayCommand]
    private void FinalizarVenda()
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 62 tests total (unchanged — this task adds no tests; `SalesViewModel` has no automated test coverage in this project, matching established convention).

- [ ] **Step 5: Commit**

```bash
git add Lojinha.App/ViewModels/SalesViewModel.cs
git commit -m "feat: add Caixa/Histórico tab-toggle state to SalesViewModel"
```

---

