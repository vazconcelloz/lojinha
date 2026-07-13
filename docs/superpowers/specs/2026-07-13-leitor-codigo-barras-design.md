# Integração com Leitor de Código de Barras — Design

## Objetivo

Na tela de Vendas, permitir que um leitor de código de barras (que funciona como teclado: digita o código rapidamente e envia Enter) adicione o produto correspondente ao carrinho automaticamente, sem exigir seleção manual no combo nem clique em "Adicionar ao carrinho".

## Fora de escopo

- Suporte a hardware específico de leitor (drivers, configuração de dispositivo) — a solução depende apenas do leitor se comportar como teclado (padrão em praticamente todo leitor USB/Bluetooth barato), não há integração de driver nenhuma.
- Leitura de peso embutido no código de barras (alguns códigos de balança embutem o peso no próprio código) — fora de escopo desta rodada; o campo Quantidade continua sendo digitado manualmente para produtos tipo Peso.
- Atalho de teclado global (funciona apenas com o campo de busca da tela de Vendas focado).

## Mecanismo de captura do scan

`Lojinha.App/Views/VendaView.xaml`'s campo de busca (`TermoBusca`) ganha um `KeyBinding` para a tecla Enter, ligado a um novo `EscanearCommand` no `SalesViewModel`:

```xml
<ui:TextBox Width="220" Margin="0,0,8,8" PlaceholderText="Buscar produto (nome ou código)"
            Text="{Binding TermoBusca, UpdateSourceTrigger=PropertyChanged}">
    <ui:TextBox.InputBindings>
        <KeyBinding Key="Return" Command="{Binding EscanearCommand}" />
    </ui:TextBox.InputBindings>
</ui:TextBox>
```

Isso mantém o padrão MVVM existente (zero code-behind novo) — um leitor de código de barras que emula teclado, ao terminar de "digitar" o código, envia Enter, que dispara o comando enquanto o campo está focado.

## Lógica de `SalesViewModel.EscanearCommand`

1. Lê `TermoBusca` (o código escaneado/digitado). Se vazio, não faz nada.
2. Busca produto por **código de barras exato** (`_productService.GetAll().FirstOrDefault(p => p.CodigoBarras == codigo)`) — diferente da busca fuzzy por nome/código já usada para filtrar o combo, já que código de barras é único por produto (índice único já existe em `Product.CodigoBarras`).
3. Se não encontrar: snackbar de erro "Produto não encontrado.", limpa `TermoBusca`, encerra.
4. Se encontrar: calcula a quantidade a adicionar como `Quantidade > 0 ? Quantidade : 1` (usa o que estiver no campo Quantidade; se o usuário não mexeu nele, assume 1 — mesma regra para produtos Unidade e Peso, sem branch por `TipoVenda`).
5. Se o produto já está no carrinho (mesmo `ProductId`): substitui a linha existente por uma cópia com a quantidade somada (`itemExistente with { Quantidade = itemExistente.Quantidade + quantidadeAdicionar }`), preservando a posição na lista.
6. Se não está no carrinho: adiciona uma nova linha (mesmo formato do `AdicionarAoCarrinhoCommand` existente).
7. Limpa `TermoBusca` e zera `Quantidade`, pronto para o próximo scan.

O fluxo manual existente (`AdicionarAoCarrinhoCommand`, digitação parcial + seleção no combo + clique) continua funcionando sem alteração — o scan é um caminho adicional, não uma substituição.

## Testes

Sem testes automatizados de UI (mesma convenção do resto do projeto — verificação por `dotnet build` + smoke manual). A lógica de "código exato bate produto" e "soma na linha existente vs. cria nova" fica inteiramente no `SalesViewModel` (não há lógica nova em `SalesService`/camada de serviço), então não há novo teste de `Lojinha.Services.Tests` — é puramente uma melhoria de UX na tela de Vendas.
