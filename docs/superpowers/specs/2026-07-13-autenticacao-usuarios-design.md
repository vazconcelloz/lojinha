# Autenticação/Usuários — Design

## Objetivo

Adicionar login obrigatório ao Lojinha, com dois papéis (Admin/Vendedor) controlando o que cada um pode fazer, e uma tela de gestão de usuários para o Admin.

## Fora de escopo

- Recuperação de senha ("esqueci minha senha") — sem envio de e-mail/SMS nessa rodada; se o Admin esquecer a senha, precisa de acesso direto ao banco.
- Múltiplos Admins simultâneos logados em máquinas diferentes / sincronização multiusuário — o app continua single-user-per-sessão, rodando local.
- Auditoria completa (log de todas as ações) — só o rastreio pontual de quem registrou cada venda (`Sale.UsuarioNome`).
- Expiração de sessão por inatividade.

## Modelo de dados

Novo modelo `User` em `Lojinha.Data.Models`:

```csharp
public enum PapelUsuario
{
    Admin,
    Vendedor
}

public class User
{
    public int Id { get; set; }
    public required string NomeUsuario { get; set; }
    public required byte[] SenhaHash { get; set; }
    public required byte[] SenhaSalt { get; set; }
    public PapelUsuario Papel { get; set; }
}
```

`NomeUsuario` tem índice único (mesmo padrão de `Product.CodigoBarras`). Senha nunca é armazenada em texto puro: `SenhaSalt` é um salt aleatório de 16 bytes gerado por usuário, `SenhaHash` é o resultado de PBKDF2 (`System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2`, embutido no .NET — sem pacote NuGet novo) sobre a senha + salt, 32 bytes de saída, 100.000 iterações, SHA256.

`Sale` ganha um campo novo:

```csharp
public string? UsuarioNome { get; set; }
```

Snapshot do nome do usuário logado no momento da venda (mesmo padrão de `PrecoUnitario` — não é uma FK para `User`, é só uma cópia de texto). Isso evita ter que decidir comportamento de delete entre `Sale` e `User`, e mantém o histórico de vendas intacto mesmo que o usuário seja excluído depois. Vendas já registradas antes desta funcionalidade existir simplesmente têm `UsuarioNome = null`.

## Backend — `UserService`

Novo serviço, mesmo padrão dos existentes (mensagens de erro em português, `ArgumentException`/`InvalidOperationException`).

- `Add(string nomeUsuario, string senha, PapelUsuario papel) : User` — valida `nomeUsuario`/`senha` não vazios, unicidade de `nomeUsuario`; gera salt, calcula hash, salva.
- `Update(int id, string nomeUsuario, string? novaSenha, PapelUsuario papel)` — valida `nomeUsuario` não vazio e único (excluindo o próprio); se `novaSenha` for nula/vazia, mantém hash/salt atuais (edição sem trocar senha); se veio preenchida, recalcula hash com novo salt.
- `Delete(int id)` — guarda: usuário não encontrado; guarda: **não deixa excluir o último Admin restante** (`InvalidOperationException` se for o único `Papel == Admin` no banco).
- `GetAll() : IEnumerable<User>`.
- `Authenticate(string nomeUsuario, string senha) : User` — busca por `nomeUsuario`, recalcula o hash da senha informada com o salt armazenado e compara em tempo constante (`CryptographicOperations.FixedTimeEquals`); `InvalidOperationException("Usuário ou senha inválidos.")` se não bater (mensagem genérica de propósito, não revela se foi o usuário ou a senha que errou).
- `AnyUsers() : bool` — usado no startup pra decidir entre tela de login e assistente de primeiro acesso.

## Sessão — `SessionService`

Classe simples, registrada como singleton no mesmo container de DI:

```csharp
public class SessionService
{
    public User? CurrentUser { get; set; }
}
```

Guarda o usuário autenticado durante a sessão do app. Injetada em `MainWindow`, `StockViewModel` e `SalesViewModel` (e futuras telas que precisem checar permissão).

## Fluxo de login/sessão

`Lojinha.App/App.xaml.cs`'s `OnStartup` monta o container de DI e o `DbContext.Migrate()` uma vez, como hoje. Em seguida, um novo método (`MostrarLoginEEntrar` ou similar) roda em loop:

1. Verifica `UserService.AnyUsers()`. Se `false`, `LoginWindow` abre em modo "Criar primeiro Admin" (campos Nome de usuário + Senha, sem seletor de papel — o primeiro usuário é sempre Admin). Se `true`, abre em modo "Login" normal (Nome de usuário + Senha).
2. `LoginWindow.ShowDialog()` — modal. Sucesso: `SessionService.CurrentUser` é setado, `DialogResult = true`. Fechar sem logar (botão fechar da janela) encerra o app inteiro (`Application.Shutdown()`), já que sem login não há o que mostrar. Erro de credenciais (ou de criação do primeiro Admin) é mostrado num `TextBlock` de erro dentro da própria janela — `LoginWindow` não tem acesso ao `ISnackbarService`/`SnackbarPresenter` da `MainWindow` (que ainda não existe nesse ponto do fluxo), então usa uma propriedade `MensagemErro` simples no `LoginViewModel`, ligada por binding.
3. Com login bem-sucedido, resolve uma nova instância de `MainWindow` via DI (`AddTransient`, já é o registro atual) e mostra (`.Show()`, e `Application.Current.MainWindow = janela` pra manter o app vivo entre trocas de usuário).
4. `MainWindow` ganha um botão "Sair" no rodapé da sidebar (ao lado do toggle de tema): ao clicar, fecha a janela atual, zera `SessionService.CurrentUser`, e chama o mesmo método do passo 1 de novo (volta pro login, sem fechar o app nem re-executar migrations).

## Permissões por papel

`MainWindow`'s construtor lê `SessionService.CurrentUser!.Papel` e ajusta a visibilidade dos itens da sidebar (mesma técnica imperativa já usada pelo código-behind existente do `MainWindow.xaml.cs`, que já mistura lógica de navegação):

- **Vendedor**: só vê os itens "Vendas" e "Estoque" na sidebar. "Categorias", "Fornecedores", "Produtos" e o novo "Usuários" ficam `Collapsed`.
- **Admin**: vê tudo, incluindo "Usuários".

Dentro das telas que o Vendedor acessa, duas restrições adicionais (via bindings de visibilidade, reaproveitando `BoolToVisibilityConverter` já existente):

- **Vendas**: `SalesViewModel` ganha `PodeCancelarVenda => _session.CurrentUser?.Papel == PapelUsuario.Admin`, escondendo o botão "Cancelar" no histórico pra quem não é Admin. Registrar venda continua liberado pra ambos os papéis.
- **Estoque**: `StockViewModel` ganha `PodeGerenciarEstoque => _session.CurrentUser?.Papel == PapelUsuario.Admin`, escondendo o card "Entrada de lote" inteiro e o botão excluir na tabela de Vencimentos. As tabelas de consulta (Estoque atual, Estoque baixo, Vencimentos) continuam visíveis pra ambos.

## Tela Usuários (Admin only)

Sexto item da sidebar, visível só pro Admin. Segue o mesmo padrão MVVM+CRUD das telas de Cadastro (já com edição, desde a rodada anterior): formulário de topo (Nome de usuário, `ui:PasswordBox` pra senha, combo de Papel) com Adicionar/Editar/Salvar/Cancelar, grid abaixo com Excluir. Editar um usuário existente deixa o campo de senha vazio por padrão (deixar vazio = manter a senha atual ao salvar).

Exclusão continua com o diálogo de confirmação padrão (`IContentDialogService`), mas se for o último Admin, o `UserService.Delete` lança a exceção e a UI mostra o erro via snackbar (mesmo padrão de erro das outras telas) em vez do sucesso esperado.

## Testes

`Lojinha.Services.Tests` ganha `UserServiceTests`:
- `Add` cria usuário com hash/salt corretos (senha em texto puro nunca é igual ao `SenhaHash` armazenado).
- `Add` lança exceção com nome de usuário duplicado.
- `Authenticate` com credenciais corretas retorna o usuário.
- `Authenticate` com senha errada lança exceção.
- `Authenticate` com usuário inexistente lança exceção.
- `Update` sem preencher nova senha mantém o hash/salt antigos (login com a senha antiga continua funcionando).
- `Update` preenchendo nova senha invalida a senha antiga.
- `Delete` remove usuário normalmente quando não é o último Admin.
- `Delete` lança exceção ao tentar excluir o único Admin restante.
- `Delete` permite excluir um Admin quando existe outro Admin no banco.

Sem testes automatizados de UI (mesma convenção do resto do projeto) — verificação por `dotnet build` + smoke manual: assistente de primeiro acesso cria o Admin, login funciona, "Sair" volta pro login sem fechar o app, sidebar de Vendedor esconde as telas certas, tentar excluir o último Admin mostra o erro.
