### Task 2: `SalesService.RegisterSale` — discount, valor recebido, troco, autorização

**Files:**
- Modify: `Lojinha.Services/SalesService.cs`
- Test: `Lojinha.Services.Tests/SalesServiceTests.cs`
- Modify: `Lojinha.App/ViewModels/SalesViewModel.cs` (minimal compatibility fix, see Step 7 — full wiring lands in Task 4)

**Interfaces:**
- Consumes: `Sale.DescontoValor`/`ValorRecebido`/`Troco`/`AutorizadoPor`, `SaleItem.DescontoValor` (Task 1).
- Produces: `SalesService.RegisterSale(IEnumerable<(int ProductId, decimal Quantidade, decimal DescontoItem)> itens, FormaPagamento formaPagamento, string? usuarioNome = null, decimal descontoVenda = 0, decimal? valorRecebido = null, string? autorizadoPor = null)` — consumed by Task 4 (`SalesViewModel.FinalizarVenda`).

- [ ] **Step 1: Update existing test call sites to the new 3-element item tuple**

In `Lojinha.Services.Tests/SalesServiceTests.cs`, every existing call to `_service.RegisterSale` passes 2-element item tuples (`(product.Id, 3m)`). Update each to a 3-element tuple with `0m` as the (unused, for these tests) discount, and add a `valorRecebido` for every existing Dinheiro call (the new validation requires it). Apply these exact replacements:

Replace:
```csharp
        Assert.Throws<ArgumentException>(() => _service.RegisterSale(Array.Empty<(int, decimal)>(), FormaPagamento.Dinheiro));
```
with:
```csharp
        Assert.Throws<ArgumentException>(() => _service.RegisterSale(Array.Empty<(int, decimal, decimal)>(), FormaPagamento.Dinheiro));
```

Replace:
```csharp
        Assert.Throws<ArgumentException>(() => _service.RegisterSale(new[] { (product.Id, 0m) }, FormaPagamento.Dinheiro));
```
with:
```csharp
        Assert.Throws<ArgumentException>(() => _service.RegisterSale(new[] { (product.Id, 0m, 0m) }, FormaPagamento.Dinheiro));
```

Replace:
```csharp
        var sale = _service.RegisterSale(new[] { (product.Id, 3m) }, FormaPagamento.Dinheiro);

        Assert.Equal(24m, sale.Total);
```
with:
```csharp
        var sale = _service.RegisterSale(new[] { (product.Id, 3m, 0m) }, FormaPagamento.Dinheiro, valorRecebido: 24m);

        Assert.Equal(24m, sale.Total);
```

Replace:
```csharp
        var sale = _service.RegisterSale(new[] { (product1.Id, 2m), (product2.Id, 4m) }, FormaPagamento.Cartao);
```
with:
```csharp
        var sale = _service.RegisterSale(new[] { (product1.Id, 2m, 0m), (product2.Id, 4m, 0m) }, FormaPagamento.Cartao);
```

Replace:
```csharp
        Assert.Throws<InvalidOperationException>(() =>
            _service.RegisterSale(new[] { (product1.Id, 2m), (product2.Id, 5m) }, FormaPagamento.Pix));
```
with:
```csharp
        Assert.Throws<InvalidOperationException>(() =>
            _service.RegisterSale(new[] { (product1.Id, 2m, 0m), (product2.Id, 5m, 0m) }, FormaPagamento.Pix));
```

Replace:
```csharp
        _service.RegisterSale(new[] { (product.Id, 1m) }, FormaPagamento.Dinheiro);

        product.PrecoVenda = 20m;
```
with:
```csharp
        _service.RegisterSale(new[] { (product.Id, 1m, 0m) }, FormaPagamento.Dinheiro, valorRecebido: 8m);

        product.PrecoVenda = 20m;
```

Replace:
```csharp
        var sale = _service.RegisterSale(new[] { (product.Id, 4m) }, FormaPagamento.Dinheiro);

        _service.CancelSale(sale.Id);
```
with:
```csharp
        var sale = _service.RegisterSale(new[] { (product.Id, 4m, 0m) }, FormaPagamento.Dinheiro, valorRecebido: 32m);

        _service.CancelSale(sale.Id);
```

Replace:
```csharp
        var sale = _service.RegisterSale(new[] { (product.Id, 1m) }, FormaPagamento.Dinheiro);
        _service.CancelSale(sale.Id);

        Assert.Throws<InvalidOperationException>(() => _service.CancelSale(sale.Id));
```
with:
```csharp
        var sale = _service.RegisterSale(new[] { (product.Id, 1m, 0m) }, FormaPagamento.Dinheiro, valorRecebido: 8m);
        _service.CancelSale(sale.Id);

        Assert.Throws<InvalidOperationException>(() => _service.CancelSale(sale.Id));
```

Replace:
```csharp
        var sale1 = _service.RegisterSale(new[] { (product.Id, 1m) }, FormaPagamento.Dinheiro);
        var sale2 = _service.RegisterSale(new[] { (product.Id, 1m) }, FormaPagamento.Cartao);
```
with:
```csharp
        var sale1 = _service.RegisterSale(new[] { (product.Id, 1m, 0m) }, FormaPagamento.Dinheiro, valorRecebido: 8m);
        var sale2 = _service.RegisterSale(new[] { (product.Id, 1m, 0m) }, FormaPagamento.Cartao);
```

Replace:
```csharp
        var sale = _service.RegisterSale(new[] { (product.Id, 1m) }, FormaPagamento.Dinheiro, "vendedor1");

        Assert.Equal("vendedor1", sale.UsuarioNome);
```
with:
```csharp
        var sale = _service.RegisterSale(new[] { (product.Id, 1m, 0m) }, FormaPagamento.Dinheiro, "vendedor1", valorRecebido: 8m);

        Assert.Equal("vendedor1", sale.UsuarioNome);
```

- [ ] **Step 2: Add new failing tests for discount/troco/authorization behavior**

Append these to `SalesServiceTests.cs`, before the closing `}` of the class:

```csharp
    [Fact]
    public void RegisterSale_ThrowsWhenItemDescontoExceedsSubtotal()
    {
        var product = CreateProduct(precoVenda: 8m);
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        Assert.Throws<ArgumentException>(() =>
            _service.RegisterSale(new[] { (product.Id, 1m, 9m) }, FormaPagamento.Cartao));
    }

    [Fact]
    public void RegisterSale_ThrowsWhenDescontoVendaExceedsSubtotal()
    {
        var product = CreateProduct(precoVenda: 8m);
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        Assert.Throws<ArgumentException>(() =>
            _service.RegisterSale(new[] { (product.Id, 1m, 0m) }, FormaPagamento.Cartao, descontoVenda: 9m));
    }

    [Fact]
    public void RegisterSale_AppliesItemAndVendaDesconto_ComputesCorrectTotal()
    {
        var product = CreateProduct(precoVenda: 10m);
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        var sale = _service.RegisterSale(new[] { (product.Id, 2m, 4m) }, FormaPagamento.Cartao, descontoVenda: 3m);

        Assert.Equal(3m, sale.DescontoValor);
        Assert.Equal(4m, sale.Items.Single().DescontoValor);
        Assert.Equal(13m, sale.Total);
    }

    [Fact]
    public void RegisterSale_Dinheiro_ThrowsWhenValorRecebidoMissing()
    {
        var product = CreateProduct(precoVenda: 8m);
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        Assert.Throws<ArgumentException>(() =>
            _service.RegisterSale(new[] { (product.Id, 1m, 0m) }, FormaPagamento.Dinheiro));
    }

    [Fact]
    public void RegisterSale_Dinheiro_ThrowsWhenValorRecebidoBelowTotal()
    {
        var product = CreateProduct(precoVenda: 8m);
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        Assert.Throws<ArgumentException>(() =>
            _service.RegisterSale(new[] { (product.Id, 1m, 0m) }, FormaPagamento.Dinheiro, valorRecebido: 7m));
    }

    [Fact]
    public void RegisterSale_Dinheiro_ComputesTroco()
    {
        var product = CreateProduct(precoVenda: 8m);
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        var sale = _service.RegisterSale(new[] { (product.Id, 1m, 0m) }, FormaPagamento.Dinheiro, valorRecebido: 10m);

        Assert.Equal(10m, sale.ValorRecebido);
        Assert.Equal(2m, sale.Troco);
    }

    [Fact]
    public void RegisterSale_NonDinheiro_IgnoresValorRecebido()
    {
        var product = CreateProduct(precoVenda: 8m);
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        var sale = _service.RegisterSale(new[] { (product.Id, 1m, 0m) }, FormaPagamento.Cartao, valorRecebido: 50m);

        Assert.Null(sale.ValorRecebido);
        Assert.Null(sale.Troco);
    }

    [Fact]
    public void RegisterSale_StoresAutorizadoPorWithoutRoleRevalidation()
    {
        var product = CreateProduct(precoVenda: 8m);
        _stockService.AddLot(product.Id, quantidade: 10, dataValidade: null, supplierId: null);

        var sale = _service.RegisterSale(new[] { (product.Id, 1m, 2m) }, FormaPagamento.Cartao, autorizadoPor: "qualquer-nome");

        Assert.Equal("qualquer-nome", sale.AutorizadoPor);
    }
```

- [ ] **Step 3: Run tests to verify the expected build failure**

Run: `dotnet test --filter "FullyQualifiedName~SalesServiceTests"`
Expected: build FAILS — `SalesServiceTests.cs` now calls `RegisterSale` with 3-element tuples and new named parameters that `SalesService.RegisterSale` (still on its old 2-tuple signature) doesn't have. This compile error is the expected RED state.

- [ ] **Step 4: Implement the new `RegisterSale` signature and validation**

In `Lojinha.Services/SalesService.cs`, replace the entire `RegisterSale` method with:

```csharp
    public Sale RegisterSale(
        IEnumerable<(int ProductId, decimal Quantidade, decimal DescontoItem)> itens,
        FormaPagamento formaPagamento,
        string? usuarioNome = null,
        decimal descontoVenda = 0,
        decimal? valorRecebido = null,
        string? autorizadoPor = null)
    {
        var itensList = itens.ToList();
        if (itensList.Count == 0)
        {
            throw new ArgumentException("Adicione ao menos um item à venda.", nameof(itens));
        }

        foreach (var item in itensList)
        {
            if (item.Quantidade <= 0)
            {
                throw new ArgumentException("Quantidade deve ser maior que zero.", nameof(itens));
            }
        }

        var quantidadePorProduto = itensList
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantidade));

        var produtos = new Dictionary<int, Product>();
        foreach (var produtoId in quantidadePorProduto.Keys)
        {
            var produto = _context.Products.Find(produtoId)
                ?? throw new InvalidOperationException("Produto não encontrado.");
            produtos[produtoId] = produto;

            if (_stockService.GetCurrentStock(produtoId) < quantidadePorProduto[produtoId])
            {
                throw new InvalidOperationException($"Estoque insuficiente para '{produto.Nome}'. Disponível: {_stockService.GetCurrentStock(produtoId)}.");
            }
        }

        var sale = new Sale
        {
            DataHora = DateTime.Now,
            FormaPagamento = formaPagamento,
            Cancelada = false,
            UsuarioNome = usuarioNome
        };

        decimal subtotalCarrinho = 0;
        foreach (var item in itensList)
        {
            var produto = produtos[item.ProductId];
            var itemSubtotal = item.Quantidade * produto.PrecoVenda;

            if (item.DescontoItem < 0 || item.DescontoItem > itemSubtotal)
            {
                throw new ArgumentException("Desconto do item não pode ser maior que o subtotal.", nameof(itens));
            }

            var saleItem = new SaleItem
            {
                ProductId = item.ProductId,
                Quantidade = item.Quantidade,
                PrecoUnitario = produto.PrecoVenda,
                DescontoValor = item.DescontoItem
            };
            sale.Items.Add(saleItem);
            subtotalCarrinho += itemSubtotal - item.DescontoItem;
        }

        if (descontoVenda < 0 || descontoVenda > subtotalCarrinho)
        {
            throw new ArgumentException("Desconto da venda não pode ser maior que o subtotal.", nameof(descontoVenda));
        }

        sale.DescontoValor = descontoVenda;
        sale.Total = subtotalCarrinho - descontoVenda;
        sale.AutorizadoPor = autorizadoPor;

        if (formaPagamento == FormaPagamento.Dinheiro)
        {
            if (valorRecebido is null || valorRecebido < sale.Total)
            {
                throw new ArgumentException("Valor recebido é obrigatório e deve ser maior ou igual ao total.", nameof(valorRecebido));
            }
            sale.ValorRecebido = valorRecebido;
            sale.Troco = valorRecebido.Value - sale.Total;
        }

        _context.Sales.Add(sale);

        foreach (var item in itensList)
        {
            _stockService.DeductStock(item.ProductId, item.Quantidade);
        }

        _context.SaveChanges();

        return sale;
    }
```

- [ ] **Step 5: Run tests to verify GREEN**

Run: `dotnet test --filter "FullyQualifiedName~SalesServiceTests"`
Expected: PASS, 19/19 (11 existing + 8 new) for the class.

- [ ] **Step 6: Confirm the App-side build break**

Run: `dotnet build`
Expected: FAILS — `Lojinha.App/ViewModels/SalesViewModel.cs` reports a compile error on its `RegisterSale` call (tuple arity mismatch), because `FinalizarVenda` still calls the old 2-tuple overload. This is expected; Step 7 is the minimal fix that resolves it (`Lojinha.Services` and `Lojinha.Services.Tests` alone already build and pass at this point — only the `Lojinha.App` project is currently broken).

- [ ] **Step 7: Minimal compatibility fix to `SalesViewModel.FinalizarVenda`**

This is *not* the full discount/authorization UI wiring — that's Task 4. This step only keeps `Lojinha.App` building and functionally correct with zero discount support in the interim.

In `Lojinha.App/ViewModels/SalesViewModel.cs`, replace:

```csharp
        try
        {
            var itens = Carrinho.Select(i => (i.ProductId, i.Quantidade));
            _salesService.RegisterSale(itens, FormaPagamentoSelecionada, _session.CurrentUser?.NomeUsuario);
```

with:

```csharp
        try
        {
            var itens = Carrinho.Select(i => (i.ProductId, i.Quantidade, DescontoItem: 0m));
            var valorRecebido = FormaPagamentoSelecionada == FormaPagamento.Dinheiro ? Total : (decimal?)null;
            _salesService.RegisterSale(itens, FormaPagamentoSelecionada, _session.CurrentUser?.NomeUsuario, valorRecebido: valorRecebido);
```

- [ ] **Step 8: Run the full test suite again**

Run: `dotnet test`
Expected: PASS, 62 tests total.

- [ ] **Step 9: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 10: Commit**

```bash
git add Lojinha.Services/SalesService.cs Lojinha.Services.Tests/SalesServiceTests.cs Lojinha.App/ViewModels/SalesViewModel.cs
git commit -m "feat: add discount, valor recebido, and troco to SalesService.RegisterSale"
```

---

