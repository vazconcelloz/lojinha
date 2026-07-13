# Edição de Registros (Categoria/Fornecedor/Produto) — Design

## Objetivo

Adicionar edição (Update) aos registros de Categoria, Fornecedor e Produto no Lojinha. Hoje essas três telas só permitem criar e excluir.

## Fora de escopo

- Edição de lotes de Estoque ou de Vendas já registradas — continuam apenas criar/excluir/cancelar, sem editar.
- Histórico de alterações (quem editou o quê e quando) — não existe hoje no app, não entra nesta rodada.

## Backend — método `Update` em cada service

Segue exatamente o padrão de validação já usado por `Add`/`Delete` (mensagens de erro em português, `ArgumentException` para input inválido, `InvalidOperationException` para regra de negócio).

### `CategoryService.Update(int id, string nome)`

Valida `nome` não vazio (mesma regra do `Add`). Busca a categoria por `id` — `InvalidOperationException("Categoria não encontrada.")` se não existir. Atualiza `Nome` e salva.

### `SupplierService.Update(int id, string nome, string? contato)`

Valida `nome` não vazio. Busca o fornecedor por `id` — `InvalidOperationException("Fornecedor não encontrado.")` se não existir. Atualiza `Nome`/`Contato` e salva.

### `ProductService.Update(int id, string nome, string codigoBarras, int categoryId, TipoVenda tipoVenda, decimal precoCusto, decimal precoVenda, decimal estoqueMinimo)`

Valida `nome` não vazio e `precoVenda > 0` (mesmas regras do `Add`). Verifica unicidade de `codigoBarras` **excluindo o próprio produto** (`p.Id != id`) — `InvalidOperationException` com a mesma mensagem do `Add` se outro produto já usa esse código. Busca o produto por `id` — `InvalidOperationException("Produto não encontrado.")` se não existir. Atualiza todos os campos (incluindo `TipoVenda`, sem nenhuma restrição — `TipoVenda` é só metadado de exibição/validação de quantidade na venda, vendas já registradas guardam sua própria `Quantidade`/`PrecoUnitario` e não são afetadas por uma troca posterior) e salva.

## UI — modo edição reaproveitando o formulário do topo

Cada uma das três telas (Categoria/Fornecedor/Produto) ganha um estado de edição no seu ViewModel:

- `int? EditandoId` — `null` quando não está editando nada.
- Um botão "Editar" (ícone `Edit24`) na coluna da grid, ao lado do botão "Excluir" existente. Ao clicar: preenche os campos do formulário do topo com os valores atuais da linha e define `EditandoId` = id da linha.
- Enquanto `EditandoId` não é `null`: o botão "Adicionar" fica escondido; em seu lugar aparecem dois botões, "Salvar" (ícone `Save24`) e "Cancelar" (ícone `Dismiss24`), lado a lado.
- **Salvar**: chama `Update(EditandoId.Value, ...)` com os valores atuais do formulário. Sucesso: limpa o formulário, `EditandoId = null`, recarrega a grid, snackbar de sucesso ("Categoria atualizada."/"Fornecedor atualizado."/"Produto atualizado."). Erro: snackbar de erro com a mensagem da exceção, mantém o formulário preenchido para o usuário corrigir e tentar de novo.
- **Cancelar**: limpa o formulário e `EditandoId = null` sem chamar nenhum service.
- Salvar não pede confirmação (mesmo padrão do Adicionar hoje — só Excluir, por ser destrutivo, tem diálogo de confirmação).

`BooleanToVisibilityConverter` (hoje declarado localmente só em `VendaView.xaml`) passa a ser um recurso global em `App.xaml` (ao lado de `CountToVisibilityConverter`), reaproveitado pelas quatro telas que precisam de visibilidade condicional por booleano (as três telas de Cadastro ganhando edição + `VendaView`, que é atualizada para consumir o recurso global em vez de sua declaração local).

## Testes

`Lojinha.Services.Tests` ganha testes para cada novo `Update`:
- `CategoryServiceTests`: atualiza nome com sucesso; lança exceção se categoria não existe; lança exceção se nome vazio.
- `SupplierServiceTests`: atualiza nome/contato com sucesso; lança exceção se fornecedor não existe.
- `ProductServiceTests`: atualiza todos os campos com sucesso (incluindo `TipoVenda`); lança exceção se produto não existe; lança exceção se novo código de barras já pertence a OUTRO produto; **não** lança exceção ao salvar sem mudar o código de barras (edita mantendo o mesmo código — regressão óbvia se a checagem de unicidade não excluir o próprio registro).

Sem testes automatizados de UI (mesma convenção do resto do projeto) — verificação por `dotnet build` + smoke manual: editar um registro em cada uma das três telas, confirmar que os valores aparecem certos na grid depois de salvar, e que Cancelar não persiste nada.
