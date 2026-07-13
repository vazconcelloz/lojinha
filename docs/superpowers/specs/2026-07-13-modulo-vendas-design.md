# Módulo de Vendas — Design

## Objetivo

Adicionar um módulo de Vendas ao Lojinha: registrar vendas com múltiplos itens (carrinho), dar baixa automática no estoque, permitir cancelamento com devolução de estoque, e listar histórico de vendas na mesma tela.

## Fora de escopo desta rodada

- Edição de vendas já registradas (só cria e cancela, não edita itens de uma venda existente).
- Relatórios agregados (faturamento por período, produto mais vendido, etc.) — fica para o módulo de Relatórios.
- Rastreamento de qual lote específico foi consumido em cada venda.
- Múltiplas formas de pagamento numa mesma venda (split payment).
- Desconto/promoções.

## Modelo de dados

Dois novos modelos em `Lojinha.Data.Models`, seguindo o padrão dos existentes (classes simples, sem lógica):

```csharp
public enum FormaPagamento
{
    Dinheiro,
    Cartao,
    Pix
}

public class Sale
{
    public int Id { get; set; }
    public DateTime DataHora { get; set; }
    public FormaPagamento FormaPagamento { get; set; }
    public decimal Total { get; set; }
    public bool Cancelada { get; set; }
    public DateTime? DataCancelamento { get; set; }

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}

public class SaleItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }

    public decimal Subtotal => Quantidade * PrecoUnitario;
}
```

`PrecoUnitario` é um snapshot do `Product.PrecoVenda` no momento da venda — não muda retroativamente se o preço do produto for alterado depois (edição de produto é um módulo futuro, mas o design já protege contra essa inconsistência).

`Sale.Total` é gravado (não recalculado on-the-fly) para exibição rápida no histórico sem precisar de `Include(Items)` toda vez.

Requer nova migration EF Core (`Sales`, `SaleItems` tables) e registro dos `DbSet` em `LojinhaDbContext`.

## Backend — `SalesService`

Novo serviço em `Lojinha.Services`, seguindo o padrão dos existentes (construtor recebe `LojinhaDbContext`, métodos síncronos, exceções `InvalidOperationException`/`ArgumentException` com mensagens em português).

### `RegisterSale(IEnumerable<(int ProductId, decimal Quantidade)> itens, FormaPagamento formaPagamento)`

1. Valida que a lista de itens não é vazia (`ArgumentException` se for).
2. Para cada item, valida `Quantidade > 0` e que `StockService.GetCurrentStock(produtoId) >= Quantidade` — se estoque insuficiente para qualquer item, lança `InvalidOperationException` com mensagem citando o produto, **antes** de qualquer alteração (nenhuma baixa parcial: tudo ou nada).
3. Cria o `Sale` (`DataHora = DateTime.Now`, `FormaPagamento`, `Cancelada = false`) e um `SaleItem` por item (com `PrecoUnitario` copiado de `Product.PrecoVenda` no momento).
4. Para cada item, chama `StockService.DeductStock(produtoId, quantidade)` (novo método, ver abaixo).
5. Calcula e grava `Sale.Total` como soma dos `Subtotal` dos itens.
6. `SaveChanges()` e retorna o `Sale` criado (com `Items` carregado).

### `CancelSale(int saleId)`

1. Busca a venda; `InvalidOperationException` se não existe ou já está cancelada.
2. Marca `Cancelada = true`, `DataCancelamento = DateTime.Now`.
3. Para cada `SaleItem` da venda, chama `StockService.AddLot(produtoId, quantidade, dataValidade: null, supplierId: null)` — devolve o estoque como um lote novo, sem tentar reconstituir de qual lote saiu originalmente (a baixa não rastreia lote, então a devolução também não).
4. `SaveChanges()`.

### `GetSaleHistory()`

Retorna todas as vendas (`Include(Items).ThenInclude(Product)`), ordenadas por `DataHora` decrescente.

## Backend — `StockService.DeductStock` (novo método)

```csharp
public void DeductStock(int productId, decimal quantidade)
```

Varre os `StockLot`s do produto em ordem de `DataEntrada` crescente (mais antigo primeiro — critério puramente mecânico de ordem de entrada, sem considerar `DataValidade`) e desconta de `QuantidadeRestante` até completar a quantidade pedida. Lança `InvalidOperationException` se a soma disponível for insuficiente (esse método também serve como segunda validação de segurança, mesmo que `SalesService` já tenha checado antes).

Isso mantém a lógica de manipulação de estoque centralizada em `StockService` (mesmo padrão dos métodos existentes), e o `SalesService` nunca acessa `StockLot` diretamente.

## UI — nova tela "Vendas"

Quinto item na sidebar (`NavigationView`), ícone sugerido `ShoppingBag24` ou similar. Segue o mesmo padrão MVVM das telas existentes: `SalesViewModel` + `VendaView.xaml`, registrado em DI, adicionado ao switch de `NavigateTo` em `MainWindow.xaml.cs` (view + refresh, como as outras 4 telas).

**Bloco carrinho:**
- Busca por nome/código (reaproveita o padrão de `ProductViewModel.TermoBusca`/`Buscar`) + combo de produtos filtrados.
- Campo quantidade.
- Botão "Adicionar ao carrinho" — valida produto selecionado + quantidade > 0, adiciona à lista do carrinho (`ObservableCollection<ItemCarrinho>`, um record local só de UI: `Produto`, `Quantidade`, `PrecoUnitario`, `Subtotal` calculado).
- Grid do carrinho com botão remover por linha.
- Combo forma de pagamento (`Dinheiro`/`Cartao`/`Pix`).
- Total calculado (soma dos subtotais do carrinho, exibido, não editável).
- Botão "Finalizar venda" — chama `SalesService.RegisterSale` com os itens do carrinho; sucesso limpa carrinho + snackbar de sucesso + recarrega histórico; erro (estoque insuficiente, etc.) mostra snackbar de erro e mantém o carrinho intacto para o usuário corrigir.

**Bloco histórico:**
- Grid abaixo do carrinho: `Data`, `Total`, `Forma de pagamento`, `Status` (Concluída/Cancelada), botão "Cancelar" (só visível/habilitado se não cancelada) com diálogo de confirmação (mesmo padrão `IContentDialogService.ShowSimpleDialogAsync` das outras telas) antes de chamar `CancelSale`.
- Após finalizar uma venda ou cancelar uma venda, recarrega o histórico imediatamente (mesma tela). Além disso, `SalesViewModel` implementa `Refresh()` (chamado por `NavigateTo` ao entrar na tela, mesmo padrão de `ProductViewModel`/`StockViewModel`) para recarregar histórico + combo de produtos, já que o estoque/produtos podem ter mudado em outra tela.

## Erros e mensagens

Todas as mensagens de erro em português, consistentes com os serviços existentes:
- Carrinho vazio: "Adicione ao menos um item à venda."
- Estoque insuficiente: "Estoque insuficiente para '{produto}'. Disponível: {quantidade}."
- Quantidade inválida: "Quantidade deve ser maior que zero."
- Venda não encontrada / já cancelada: "Venda não encontrada." / "Venda já foi cancelada."

## Testes

`Lojinha.Services.Tests` ganha `SalesServiceTests` cobrindo:
- Venda de item único dá baixa correta no estoque.
- Venda multi-item dá baixa em todos os produtos e soma o `Total` corretamente.
- Venda com quantidade acima do estoque disponível lança exceção e não altera nenhum estoque (nem dos outros itens do carrinho).
- `PrecoUnitario` do item é o preço do produto no momento da venda (snapshot).
- Cancelar uma venda devolve a quantidade ao estoque (via novo lote).
- Cancelar uma venda já cancelada lança exceção.
- `DeductStock` descontando de múltiplos lotes do mesmo produto (quantidade pedida maior que um lote isolado) consome na ordem de entrada e não deixa `QuantidadeRestante` negativo.

Nenhum teste de UI automatizado (mesma convenção do resto do projeto) — verificação por `dotnet build` + smoke manual.
