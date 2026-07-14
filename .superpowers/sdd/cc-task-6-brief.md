### Task 6: `VendaView.xaml` — Turno tab and enum-based tab switching

**Files:**
- Modify: `Lojinha.App/Views/VendaView.xaml`

**Interfaces:**
- Consumes: `SalesViewModel.AbaAtiva`/`MostrarTurnoCommand`/`Turno` (Task 4); `EnumToVisibilityConverter` (Task 5); `TurnoViewModel`'s full member set (Task 3).

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
            <ui:Button Content="Histórico" Margin="0,0,8,0" Command="{Binding MostrarHistoricoCommand}" />
            <ui:Button Content="Turno" Command="{Binding MostrarTurnoCommand}" />
        </StackPanel>

        <Grid Grid.Row="1" Visibility="{Binding AbaAtiva, Converter={StaticResource EnumToVisibilityConverter}, ConverterParameter=Caixa}">
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

        <ui:Card Grid.Row="1" Visibility="{Binding AbaAtiva, Converter={StaticResource EnumToVisibilityConverter}, ConverterParameter=Historico}">
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

        <ui:Card Grid.Row="1" Visibility="{Binding AbaAtiva, Converter={StaticResource EnumToVisibilityConverter}, ConverterParameter=Turno}">
            <StackPanel>
                <TextBlock Text="Turno de caixa" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" />

                <StackPanel Visibility="{Binding Turno.SessaoAberta, Converter={StaticResource BooleanToVisibilityConverter}, ConverterParameter=Invert}">
                    <TextBlock Text="Nenhum caixa aberto." Opacity="0.7" Margin="0,0,0,12" />
                    <WrapPanel>
                        <ui:TextBox Width="150" Margin="0,0,8,0" PlaceholderText="Valor de abertura"
                                    Text="{Binding Turno.ValorAberturaEntrada, UpdateSourceTrigger=PropertyChanged}" />
                        <ui:Button Content="Abrir caixa" Appearance="Primary" Icon="{ui:SymbolIcon Symbol=Wallet24}"
                                   Command="{Binding Turno.AbrirCaixaCommand}" />
                    </WrapPanel>
                </StackPanel>

                <StackPanel Visibility="{Binding Turno.SessaoAberta, Converter={StaticResource BooleanToVisibilityConverter}}">
                    <TextBlock Text="{Binding Turno.SessaoAtual.DataAbertura, StringFormat='Aberto desde: {0:dd/MM/yyyy HH:mm}'}" Margin="0,0,0,4" />
                    <TextBlock Text="{Binding Turno.SessaoAtual.ValorAbertura, StringFormat='Valor de abertura: {0:C}'}" Margin="0,0,0,12" Opacity="0.7" />

                    <TextBlock Text="Sangria / suprimento" FontWeight="Bold" Margin="0,0,0,8" />
                    <WrapPanel Margin="0,0,0,12">
                        <ComboBox Width="120" Margin="0,0,8,0" ItemsSource="{Binding Turno.TiposMovimento}"
                                  SelectedItem="{Binding Turno.TipoMovimentoSelecionado}" />
                        <ui:TextBox Width="120" Margin="0,0,8,0" PlaceholderText="Valor"
                                    Text="{Binding Turno.ValorMovimentoEntrada, UpdateSourceTrigger=PropertyChanged}" />
                        <ui:Button Content="Registrar" Icon="{ui:SymbolIcon Symbol=ArrowSwap24}"
                                   Command="{Binding Turno.RegistrarMovimentoCommand}" />
                    </WrapPanel>

                    <DataGrid ItemsSource="{Binding Turno.Movimentos}" AutoGenerateColumns="False" IsReadOnly="True" MaxHeight="200" Margin="0,0,0,16">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="Data" Binding="{Binding DataHora, StringFormat='dd/MM/yyyy HH:mm'}" Width="140" />
                            <DataGridTextColumn Header="Tipo" Binding="{Binding Tipo}" Width="100" />
                            <DataGridTextColumn Header="Valor" Binding="{Binding Valor, StringFormat=C}" Width="100" />
                            <DataGridTextColumn Header="Autorizado por" Binding="{Binding AutorizadoPor}" Width="140" />
                            <DataGridTextColumn Header="Observação" Binding="{Binding Observacao}" Width="*" />
                        </DataGrid.Columns>
                    </DataGrid>

                    <TextBlock Text="Fechar caixa" FontWeight="Bold" Margin="0,0,0,8" />
                    <WrapPanel>
                        <ui:TextBox Width="150" Margin="0,0,8,0" PlaceholderText="Valor contado"
                                    Text="{Binding Turno.ValorContadoEntrada, UpdateSourceTrigger=PropertyChanged}" />
                        <ui:Button Content="Fechar caixa" Appearance="Danger" Icon="{ui:SymbolIcon Symbol=LockClosed24}"
                                   Command="{Binding Turno.FecharCaixaCommand}" />
                    </WrapPanel>
                </StackPanel>
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
Expected: PASS, 81 tests total (unchanged — XAML-only task).

- [ ] **Step 4: Manual smoke check**

Run: `dotnet run --project Lojinha.App` in the background, confirm the process starts and stays alive, then terminate it. Full interactive verification (opening/closing a session, registering sangria/suprimento, the `FinalizarVenda` gate) happens in Task 7's end-to-end walkthrough.

- [ ] **Step 5: Commit**

```bash
git add Lojinha.App/Views/VendaView.xaml
git commit -m "feat: add Turno tab UI for cash-session control"
```

---

