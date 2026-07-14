# Tela de Caixa Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the Vendas screen into a checkout ("caixa") layout — a fixed, always-visible summary/payment panel plus a Caixa/Histórico tab toggle — with zero changes to business logic.

**Architecture:** A pure XAML restructuring of `Lojinha.App/Views/VendaView.xaml` into a two-column layout (cart on the left, fixed summary panel on the right) with a tab toggle that switches between the Caixa view and the existing histórico grid. One small addition to `SalesViewModel` provides the tab-toggle state; every other binding the new layout uses already exists from the Desconto e Troco feature. The sidebar nav label changes from "Vendas" to "Caixa" as a cosmetic-only change.

**Tech Stack:** .NET 8, WPF, WPF-UI 4.3.0, CommunityToolkit.Mvvm.

## Global Constraints

- No `SalesService` changes, no new business logic, no migration — this is presentation-layer only.
- No automated UI tests in this plan (per established project convention) — frontend tasks are verified by `dotnet build` + a manual smoke run.
- All UI copy is in Portuguese.
- The internal `TargetPageTag="vendas"`, every `"vendas"` tag reference in `MainWindow.xaml.cs`, the `VendaView` class name, and the `SalesViewModel` class name are all left unchanged — only the sidebar's visible `Content` label changes from "Vendas" to "Caixa".
- Every binding in the new `VendaView.xaml` must reference a member that already exists on `SalesViewModel`/`ItemCarrinho`/`VendaHistoricoItem` (from the Desconto e Troco feature) except `MostrandoHistorico`, `MostrarCaixaCommand`, and `MostrarHistoricoCommand`, which Task 1 adds.

---

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

### Task 2: `VendaView.xaml` — two-column checkout layout

**Files:**
- Modify: `Lojinha.App/Views/VendaView.xaml`

**Interfaces:**
- Consumes: `SalesViewModel.MostrandoHistorico`/`MostrarCaixaCommand`/`MostrarHistoricoCommand` (Task 1), and all pre-existing `SalesViewModel`/`ItemCarrinho`/`VendaHistoricoItem` members from the Desconto e Troco feature.

- [ ] **Step 1: Replace the entire file**

Replace the entire contents of `Lojinha.App/Views/VendaView.xaml` with:

```xml
<UserControl x:Class="Lojinha.App.Views.VendaView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
             mc:Ignorable="d">
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,12">
            <ui:Button Content="Caixa" Margin="0,0,8,0" Command="{Binding MostrarCaixaCommand}" />
            <ui:Button Content="Histórico" Command="{Binding MostrarHistoricoCommand}" />
        </StackPanel>

        <Grid Grid.Row="1" Visibility="{Binding MostrandoHistorico, Converter={StaticResource BooleanToVisibilityConverter}, ConverterParameter=Invert}">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="2*" />
                <ColumnDefinition Width="1*" />
            </Grid.ColumnDefinitions>

            <StackPanel Grid.Column="0" Margin="0,0,16,0">
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

                <ui:Card>
                    <StackPanel>
                        <TextBlock Text="Carrinho" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />
                        <StackPanel Visibility="{Binding Carrinho.Count, Converter={StaticResource CountToVisibilityConverter}}">
                            <TextBlock Text="Carrinho vazio." Opacity="0.7" />
                        </StackPanel>
                        <DataGrid ItemsSource="{Binding Carrinho}" AutoGenerateColumns="False" IsReadOnly="True" MaxHeight="320"
                                  Visibility="{Binding Carrinho.Count, Converter={StaticResource CountToVisibilityConverter}, ConverterParameter=Invert}">
                            <DataGrid.Columns>
                                <DataGridTextColumn Header="Produto" Binding="{Binding Produto}" Width="*" />
                                <DataGridTextColumn Header="Quantidade" Binding="{Binding Quantidade}" Width="90" />
                                <DataGridTextColumn Header="Preço unit." Binding="{Binding PrecoUnitario}" Width="90" />
                                <DataGridTemplateColumn Header="Desconto" Width="150">
                                    <DataGridTemplateColumn.CellTemplate>
                                        <DataTemplate>
                                            <StackPanel Orientation="Horizontal">
                                                <ComboBox Width="70" ItemsSource="{Binding DataContext.TiposDesconto, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                                          SelectedItem="{Binding DescontoTipo}" />
                                                <ui:TextBox Width="70" Margin="4,0,0,0"
                                                            Text="{Binding DescontoEntrada, UpdateSourceTrigger=PropertyChanged}" />
                                            </StackPanel>
                                        </DataTemplate>
                                    </DataGridTemplateColumn.CellTemplate>
                                </DataGridTemplateColumn>
                                <DataGridTextColumn Header="Total item" Binding="{Binding SubtotalComDesconto, StringFormat=C}" Width="100" />
                                <DataGridTemplateColumn Header="" Width="60">
                                    <DataGridTemplateColumn.CellTemplate>
                                        <DataTemplate>
                                            <ui:Button Appearance="Danger" Icon="{ui:SymbolIcon Symbol=Delete24}"
                                                       Command="{Binding DataContext.RemoverDoCarrinhoCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                                       CommandParameter="{Binding}" />
                                        </DataTemplate>
                                    </DataGridTemplateColumn.CellTemplate>
                                </DataGridTemplateColumn>
                            </DataGrid.Columns>
                        </DataGrid>
                    </StackPanel>
                </ui:Card>
            </StackPanel>

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
        </Grid>

        <ui:Card Grid.Row="1" Visibility="{Binding MostrandoHistorico, Converter={StaticResource BooleanToVisibilityConverter}}">
            <StackPanel>
                <TextBlock Text="Histórico de vendas" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />
                <StackPanel Visibility="{Binding Historico.Count, Converter={StaticResource CountToVisibilityConverter}}">
                    <TextBlock Text="Nenhuma venda registrada ainda." Opacity="0.7" />
                </StackPanel>
                <DataGrid ItemsSource="{Binding Historico}" AutoGenerateColumns="False" IsReadOnly="True" MaxHeight="500"
                          Visibility="{Binding Historico.Count, Converter={StaticResource CountToVisibilityConverter}, ConverterParameter=Invert}">
                    <DataGrid.RowStyle>
                        <Style TargetType="DataGridRow">
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding Cancelada}" Value="True">
                                    <Setter Property="Foreground" Value="Gray" />
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </DataGrid.RowStyle>
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Data" Binding="{Binding DataHora, StringFormat='dd/MM/yyyy HH:mm'}" Width="140" />
                        <DataGridTextColumn Header="Total" Binding="{Binding Total, StringFormat=C}" Width="100" />
                        <DataGridTextColumn Header="Pagamento" Binding="{Binding FormaPagamento}" Width="100" />
                        <DataGridTextColumn Header="Status" Binding="{Binding Status}" Width="100" />
                        <DataGridTextColumn Header="Vendedor" Binding="{Binding UsuarioNome}" Width="120" />
                        <DataGridTextColumn Header="Desconto" Binding="{Binding DescontoValor, StringFormat=C}" Width="90" />
                        <DataGridTextColumn Header="Troco" Binding="{Binding Troco, StringFormat=C}" Width="90" />
                        <DataGridTemplateColumn Header="" Width="140">
                            <DataGridTemplateColumn.CellTemplate>
                                <DataTemplate>
                                    <ui:Button Content="Cancelar" Appearance="Danger" Icon="{ui:SymbolIcon Symbol=Dismiss24}"
                                               Command="{Binding DataContext.CancelarVendaCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                               CommandParameter="{Binding}">
                                        <ui:Button.Visibility>
                                            <MultiBinding Converter="{StaticResource BooleanAndToVisibilityConverter}">
                                                <Binding Path="PodeCancelar" />
                                                <Binding Path="DataContext.PodeCancelarVenda" RelativeSource="{RelativeSource AncestorType=DataGrid}" />
                                            </MultiBinding>
                                        </ui:Button.Visibility>
                                    </ui:Button>
                                </DataTemplate>
                            </DataGridTemplateColumn.CellTemplate>
                        </DataGridTemplateColumn>
                    </DataGrid.Columns>
                </DataGrid>
            </StackPanel>
        </ui:Card>
    </Grid>
</UserControl>
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 62 tests total (unchanged — XAML-only task).

- [ ] **Step 4: Manual smoke check**

Run: `dotnet run --project Lojinha.App` in the background, confirm the process starts and stays alive (`tasklist //FI "IMAGENAME eq Lojinha.App.exe"`), then terminate it (`taskkill //F //IM Lojinha.App.exe`). This confirms the XAML parses and the app doesn't crash on load; full interactive verification (clicking the tab toggle, checking the fixed panel doesn't scroll) happens in Task 3's end-to-end walkthrough.

- [ ] **Step 5: Commit**

```bash
git add Lojinha.App/Views/VendaView.xaml
git commit -m "feat: redesign Vendas screen into a two-column checkout layout"
```

---

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
