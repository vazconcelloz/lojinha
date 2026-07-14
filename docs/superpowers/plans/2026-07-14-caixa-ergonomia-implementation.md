# Ergonomia do Caixa Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an F2 keyboard shortcut to finalize a sale, and enlarge fonts/controls on the Caixa tab, for faster and less error-prone operation during a live shift.

**Architecture:** Both changes are XAML-only edits to `Lojinha.App/Views/VendaView.xaml`, scoped entirely to the Caixa tab's `Grid` (the one bound to `AbaAtiva == Caixa`) — Histórico and Turno tabs are untouched. No ViewModel, service, or model changes.

**Tech Stack:** .NET 8, WPF, WPF-UI 4.3.0.

## Global Constraints

- No quick-add product buttons — every item is added via barcode scan or manual search, unchanged.
- Larger fonts/controls apply only within the Caixa tab's `Grid` — Histórico, Turno, and every other screen in the app stay as-is.
- No automated UI tests in this plan (established project convention) — frontend tasks are verified by `dotnet build` + a manual smoke run.
- The F2 `KeyBinding` must be attached to the Caixa tab's `Grid` itself (not the `UserControl` root) — WPF removes keyboard focus from a `Collapsed` element's subtree, so scoping it there is what makes F2 a no-op while the Histórico or Turno tab is active, with no extra code needed to check `AbaAtiva`.

---

### Task 1: F2 shortcut to finalize a sale

**Files:**
- Modify: `Lojinha.App/Views/VendaView.xaml`

**Interfaces:**
- Consumes: `SalesViewModel.FinalizarVendaCommand` (existing, unchanged).

- [ ] **Step 1: Add the `KeyBinding`**

In `Lojinha.App/Views/VendaView.xaml`, replace:

```xml
        <Grid Grid.Row="1" Visibility="{Binding AbaAtiva, Converter={StaticResource EnumToVisibilityConverter}, ConverterParameter=Caixa}">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="2*" />
                <ColumnDefinition Width="1*" />
            </Grid.ColumnDefinitions>
```

with:

```xml
        <Grid Grid.Row="1" Visibility="{Binding AbaAtiva, Converter={StaticResource EnumToVisibilityConverter}, ConverterParameter=Caixa}">
            <Grid.InputBindings>
                <KeyBinding Key="F2" Command="{Binding FinalizarVendaCommand}" />
            </Grid.InputBindings>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="2*" />
                <ColumnDefinition Width="1*" />
            </Grid.ColumnDefinitions>
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 81 tests total (unchanged — XAML-only, no ViewModel/service change).

- [ ] **Step 4: Manual smoke check**

Run: `dotnet run --project Lojinha.App` in the background, confirm the process starts and stays alive, then terminate it. Full interactive verification (pressing F2 from each Caixa-tab field, confirming it's a no-op on Histórico/Turno) happens in this feature's final walkthrough — Task 2 covers the remaining XAML changes to the same file, so a single combined walkthrough after both tasks is more efficient than two partial ones.

- [ ] **Step 5: Commit**

```bash
git add Lojinha.App/Views/VendaView.xaml
git commit -m "feat: add F2 shortcut to finalize sale on the Caixa tab"
```

---

### Task 2: Larger fonts/controls on the Caixa tab

**Files:**
- Modify: `Lojinha.App/Views/VendaView.xaml`

**Interfaces:** none (pure presentation sizing, no new bindings).

- [ ] **Step 1: Enlarge the "Nova venda" card's controls**

Replace:

```xml
                <ui:Card Margin="0,0,0,16">
                    <StackPanel>
                        <TextBlock Text="Nova venda" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />
                        <WrapPanel>
                            <ui:TextBox Width="220" Margin="0,0,8,8" PlaceholderText="Buscar produto (nome ou código)"
                                        Text="{Binding TermoBusca, UpdateSourceTrigger=PropertyChanged}">
                                <ui:TextBox.InputBindings>
                                    <KeyBinding Key="Return" Command="{Binding EscanearCommand}" />
                                </ui:TextBox.InputBindings>
                            </ui:TextBox>
                            <ComboBox Width="220" Margin="0,0,8,8" ItemsSource="{Binding Produtos}" DisplayMemberPath="Nome"
                                      SelectedItem="{Binding ProdutoSelecionado}" />
                            <ui:TextBox Width="120" Margin="0,0,8,8" PlaceholderText="Quantidade"
                                        Text="{Binding Quantidade, UpdateSourceTrigger=PropertyChanged}" />
                            <ui:Button Content="Adicionar ao carrinho" Appearance="Primary" Icon="{ui:SymbolIcon Symbol=Add24}"
                                       Command="{Binding AdicionarAoCarrinhoCommand}" />
                        </WrapPanel>
                    </StackPanel>
                </ui:Card>
```

with:

```xml
                <ui:Card Margin="0,0,0,16">
                    <StackPanel>
                        <TextBlock Text="Nova venda" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />
                        <WrapPanel>
                            <ui:TextBox Width="240" Height="40" FontSize="16" Margin="0,0,8,8" PlaceholderText="Buscar produto (nome ou código)"
                                        Text="{Binding TermoBusca, UpdateSourceTrigger=PropertyChanged}">
                                <ui:TextBox.InputBindings>
                                    <KeyBinding Key="Return" Command="{Binding EscanearCommand}" />
                                </ui:TextBox.InputBindings>
                            </ui:TextBox>
                            <ComboBox Width="240" Height="40" FontSize="16" Margin="0,0,8,8" ItemsSource="{Binding Produtos}" DisplayMemberPath="Nome"
                                      SelectedItem="{Binding ProdutoSelecionado}" />
                            <ui:TextBox Width="130" Height="40" FontSize="16" Margin="0,0,8,8" PlaceholderText="Quantidade"
                                        Text="{Binding Quantidade, UpdateSourceTrigger=PropertyChanged}" />
                            <ui:Button Content="Adicionar ao carrinho" Appearance="Primary" FontSize="16" Padding="16,10" Icon="{ui:SymbolIcon Symbol=Add24}"
                                       Command="{Binding AdicionarAoCarrinhoCommand}" />
                        </WrapPanel>
                    </StackPanel>
                </ui:Card>
```

- [ ] **Step 2: Enlarge the cart `DataGrid` rows**

Replace:

```xml
                        <DataGrid ItemsSource="{Binding Carrinho}" AutoGenerateColumns="False" IsReadOnly="True" MaxHeight="320"
                                  Visibility="{Binding Carrinho.Count, Converter={StaticResource CountToVisibilityConverter}, ConverterParameter=Invert}">
```

with:

```xml
                        <DataGrid ItemsSource="{Binding Carrinho}" AutoGenerateColumns="False" IsReadOnly="True" MaxHeight="320"
                                  RowHeight="36" FontSize="14"
                                  Visibility="{Binding Carrinho.Count, Converter={StaticResource CountToVisibilityConverter}, ConverterParameter=Invert}">
```

(`FontSize` is a WPF inherited property — setting it on the `DataGrid` cascades to the per-item desconto `ComboBox`/`TextBox` and the delete `Button` inside its `DataGridTemplateColumn` cell templates automatically, with no changes needed to those elements themselves.)

- [ ] **Step 3: Enlarge the summary panel's labels, inputs, total, and Finalizar button**

Replace:

```xml
            <ui:Card Grid.Column="1" VerticalAlignment="Top">
                <StackPanel>
                    <TextBlock Text="RESUMO DA VENDA" FontWeight="Bold" FontSize="14" Margin="0,0,0,16" />

                    <TextBlock Text="Desconto da venda" Margin="0,0,0,4" Opacity="0.7" />
                    <WrapPanel Margin="0,0,0,12">
                        <ComboBox Width="90" Margin="0,0,8,0" ItemsSource="{Binding TiposDesconto}"
                                  SelectedItem="{Binding TipoDescontoVenda}" />
                        <ui:TextBox Width="90" Text="{Binding DescontoVendaEntrada, UpdateSourceTrigger=PropertyChanged}" />
                    </WrapPanel>

                    <TextBlock Text="Forma de pagamento" Margin="0,0,0,4" Opacity="0.7" />
                    <ComboBox Margin="0,0,0,12" ItemsSource="{Binding FormasPagamento}"
                              SelectedItem="{Binding FormaPagamentoSelecionada}" />

                    <StackPanel Visibility="{Binding EhDinheiro, Converter={StaticResource BooleanToVisibilityConverter}}">
                        <TextBlock Text="Valor recebido" Margin="0,0,0,4" Opacity="0.7" />
                        <ui:TextBox Margin="0,0,0,12" Text="{Binding ValorRecebido, UpdateSourceTrigger=PropertyChanged}" />
                    </StackPanel>

                    <Separator Margin="0,0,0,12" />

                    <TextBlock Text="{Binding Total, StringFormat='{}{0:C}'}" FontWeight="Bold" FontSize="30"
                               HorizontalAlignment="Center" Margin="0,0,0,8" />

                    <TextBlock Text="{Binding Troco, StringFormat='Troco: {0:C}'}" HorizontalAlignment="Center"
                               Opacity="0.7" Margin="0,0,0,16"
                               Visibility="{Binding EhDinheiro, Converter={StaticResource BooleanToVisibilityConverter}}" />

                    <ui:Button Content="Finalizar venda" Appearance="Primary" HorizontalAlignment="Stretch"
                               Icon="{ui:SymbolIcon Symbol=ReceiptMoney24}"
                               Command="{Binding FinalizarVendaCommand}" />
                </StackPanel>
            </ui:Card>
```

with:

```xml
            <ui:Card Grid.Column="1" VerticalAlignment="Top">
                <StackPanel>
                    <TextBlock Text="RESUMO DA VENDA" FontWeight="Bold" FontSize="14" Margin="0,0,0,16" />

                    <TextBlock Text="Desconto da venda" FontSize="14" Margin="0,0,0,4" Opacity="0.7" />
                    <WrapPanel Margin="0,0,0,12">
                        <ComboBox Width="90" Height="40" FontSize="16" Margin="0,0,8,0" ItemsSource="{Binding TiposDesconto}"
                                  SelectedItem="{Binding TipoDescontoVenda}" />
                        <ui:TextBox Width="90" Height="40" FontSize="16" Text="{Binding DescontoVendaEntrada, UpdateSourceTrigger=PropertyChanged}" />
                    </WrapPanel>

                    <TextBlock Text="Forma de pagamento" FontSize="14" Margin="0,0,0,4" Opacity="0.7" />
                    <ComboBox Height="40" FontSize="16" Margin="0,0,0,12" ItemsSource="{Binding FormasPagamento}"
                              SelectedItem="{Binding FormaPagamentoSelecionada}" />

                    <StackPanel Visibility="{Binding EhDinheiro, Converter={StaticResource BooleanToVisibilityConverter}}">
                        <TextBlock Text="Valor recebido" FontSize="14" Margin="0,0,0,4" Opacity="0.7" />
                        <ui:TextBox Height="40" FontSize="16" Margin="0,0,0,12" Text="{Binding ValorRecebido, UpdateSourceTrigger=PropertyChanged}" />
                    </StackPanel>

                    <Separator Margin="0,0,0,12" />

                    <TextBlock Text="{Binding Total, StringFormat='{}{0:C}'}" FontWeight="Bold" FontSize="36"
                               HorizontalAlignment="Center" Margin="0,0,0,8" />

                    <TextBlock Text="{Binding Troco, StringFormat='Troco: {0:C}'}" HorizontalAlignment="Center" FontSize="14"
                               Opacity="0.7" Margin="0,0,0,16"
                               Visibility="{Binding EhDinheiro, Converter={StaticResource BooleanToVisibilityConverter}}" />

                    <ui:Button Content="Finalizar venda" Appearance="Primary" HorizontalAlignment="Stretch"
                               FontSize="18" Height="50"
                               Icon="{ui:SymbolIcon Symbol=ReceiptMoney24}"
                               Command="{Binding FinalizarVendaCommand}" />
                </StackPanel>
            </ui:Card>
```

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 81 tests total (unchanged).

- [ ] **Step 6: Manual smoke check**

Run: `dotnet run --project Lojinha.App` in the background, confirm the process starts and stays alive, then terminate it.

- [ ] **Step 7: Commit**

```bash
git add Lojinha.App/Views/VendaView.xaml
git commit -m "feat: enlarge fonts and controls on the Caixa tab"
```

---

### Task 3: Final integration check

**Files:** none (verification only).

- [ ] **Step 1: Full test suite**

Run: `dotnet test`
Expected: PASS, 81 tests total, 0 failures.

- [ ] **Step 2: Full build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 3: End-to-end manual walkthrough**

Run: `dotnet run --project Lojinha.App` and, in one session, on the Caixa tab:

1. Confirm the busca produto, quantidade, discount, and summary-panel fields are visibly larger than before.
2. Add an item to the cart, click into the busca field, press F2 — confirm the sale finalizes (or shows the expected "carrinho vazio"/"abra o caixa" error if those preconditions aren't met — either way, confirm F2 actually triggered `FinalizarVenda`, not a no-op).
3. Click into the "Valor recebido" field (select Dinheiro first) and press F2 — confirm it also finalizes from that field.
4. Switch to the "Histórico" tab, click into any control there (if any), press F2 — confirm nothing happens (no sale attempt, no error snackbar).
5. Switch to the "Turno" tab, click into the "Valor de abertura" or sangria/suprimento field, press F2 — confirm nothing happens there either (this is the specific risk the plan's global constraint calls out: F2 must not leak into other tabs' fields).
6. Confirm the "Finalizar venda" button is visibly larger than the other buttons on the screen.

- [ ] **Step 4: Push**

```bash
git push
```
