# Frontend Fluent (Cadastro + Estoque) — Design

## Contexto

O backend (models, EF Core, `CategoryService`/`SupplierService`/`ProductService`/`StockService`) já existe e tem testes passando. O frontend WPF atual (`Lojinha.App`) tem 3 telas simples (Categoria/Fornecedor/Produto) em `TabControl`, com controles nativos sem estilo, sem exclusão, sem tela de Estoque.

Objetivo: redesenhar o frontend com visual Fluent (Windows 11) leve — sem Mica/blur, sem animações pesadas —, navegação lateral, feedback interativo (snackbar, confirmação de exclusão, estados vazios, tema claro/escuro), e adicionar a tela de Estoque que ainda não existe.

## Escopo

Inclui:
- Restyle das 3 telas de Cadastro existentes.
- Nova tela de Estoque (entrada de lote, estoque atual, alertas de estoque baixo, alertas de vencimento).
- Exclusão (com confirmação) para Categoria, Fornecedor, Produto e Lote — exige novos métodos `Delete` no backend.
- Tema claro/escuro e feedback via snackbar.

Não inclui (fora de escopo desta rodada): módulo de Vendas, edição de registros existentes (só criar/excluir), autenticação/usuários, relatórios.

## Dependência nova

Adicionar pacote NuGet `WPF-UI` ao `Lojinha.App.csproj` (controles Fluent: `FluentWindow`, `NavigationView`, `Card`, `TextBox`, `Button`, `SnackbarService`, `ContentDialogService`, `ApplicationThemeManager`).

## Arquitetura / Shell

- `MainWindow` passa a herdar de `ui:FluentWindow` em vez de `Window`.
- Conteúdo principal: `NavigationView` (WPF-UI) lateral com 4 itens, cada um navegando para uma `Page`/`UserControl` de conteúdo:
  1. Categorias
  2. Fornecedores
  3. Produtos
  4. Estoque
- `BackgroundType` do `NavigationView`/`FluentWindow` configurado para não usar Mica (superfície sólida, sem transparência/blur).
- Rodapé do `NavigationView` tem um toggle de tema claro/escuro, usando `Wpf.Ui.Appearance.ApplicationThemeManager.Apply(...)`.

## Interação / feedback

- **Snackbar**: `ISnackbarService` registrado no DI, com um `SnackbarPresenter` hospedado na `FluentWindow`. Toda operação de Adicionar/Excluir mostra um snackbar de sucesso (verde/neutro) ou erro (vermelho), substituindo o `TextBlock` vermelho fixo usado hoje para `MensagemErro`.
- **Confirmação de exclusão**: `IContentDialogService` registrado no DI. Cada grid (Categoria/Fornecedor/Produto/Lote) ganha uma coluna/botão "Excluir" que abre um `ContentDialog` ("Tem certeza que deseja excluir {nome}?"); confirmando, chama o comando de exclusão do ViewModel.
- **Estado vazio**: cada lista (`ObservableCollection`) expõe uma propriedade computada `EstaVazia` (ou binding direto em `Count == 0`); quando vazia, a grid é substituída por um painel simples com ícone Fluent + texto amigável (ex: "Nenhuma categoria cadastrada ainda").

## Backend — novos métodos Delete

- `CategoryService.Delete(int id)`: se existir `Product` com `CategoryId == id`, lança `InvalidOperationException` com mensagem amigável (“Categoria possui produtos vinculados.”); senão remove.
- `SupplierService.Delete(int id)`: remove diretamente — FK `StockLot.SupplierId` já é `SetNull`, lotes existentes ficam sem fornecedor.
- `ProductService.Delete(int id)`: remove diretamente — FK `StockLot.ProductId` já é `Cascade`, lotes do produto são removidos junto.
- `StockService.DeleteLot(int id)`: remove um `StockLot` específico (corrigir lançamento errado).
- Cada serviço acima recebe testes cobrindo: exclusão bem-sucedida e (para Category) o caso bloqueado por FK em uso.

## Tela de Estoque (nova) — `StockViewModel`

Um único ViewModel agregando 4 blocos (todos alimentados por `StockService` + `ProductService` + `SupplierService` já existentes):

1. **Entrada de lote**: form com combo de Produto, campo Quantidade, combo de Fornecedor (opcional), date picker de Validade (opcional) → chama `StockService.AddLot`. Sucesso dispara snackbar e atualiza os outros 3 blocos.
2. **Estoque atual**: tabela Produto × Quantidade total (`StockService.GetCurrentStock` por produto listado).
3. **Estoque baixo**: lista de produtos abaixo do `EstoqueMinimo` (`GetLowStockProducts`), destacada com cor de alerta.
4. **Vencimentos**: lista combinada de lotes vencendo em breve (`GetExpiringLots`, padrão 7 dias) e já vencidos (`GetExpiredLots`), com os vencidos exibidos em vermelho.

`StockViewModel` é registrado no DI e exposto pelo `MainViewModel` como as demais telas.

## Telas de Cadastro (restyle)

Estrutura de cada tela (Categoria/Fornecedor/Produto) permanece form + grid, mas:
- Form agrupado num `ui:Card`.
- Inputs trocados para `ui:TextBox` / `ui:ComboBox` / `ui:Button` (ícones onde fizer sentido, ex: "+" no botão Adicionar, lixeira na coluna Excluir).
- Grid ganha coluna de ação "Excluir" (ícone) por linha, disparando o fluxo de confirmação descrito acima.
- Erros de validação (ex: nome vazio, código de barras duplicado) continuam vindo das exceções já lançadas pelos Services, mas agora exibidos via snackbar em vez de texto fixo.

## Testes

- Serviços: novos testes de `Delete` em `CategoryServiceTests`, `SupplierServiceTests`, `ProductServiceTests`, `StockServiceTests` (caminho feliz + bloqueio por FK onde aplicável).
- Frontend: sem testes automatizados de UI nesta rodada (fora de escopo); verificação manual via execução do app (build + smoke visual) antes de considerar concluído.

## Fora de escopo / decisões conscientes

- Sem edição de registros (só criar/excluir) — não pedido.
- Sem paginação nas grids — volume de dados esperado é pequeno (loja pequena).
- Sem testes automatizados de UI (WPF não tem ferramenta leve equivalente a Playwright no fluxo atual) — validação manual via screenshot/execução.
