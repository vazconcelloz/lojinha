### Task 5: `VendaView.xaml` — discount, valor recebido, and troco UI

**Files:**
- Modify: `Lojinha.App/Views/VendaView.xaml`

**Interfaces:**
- Consumes: all `SalesViewModel`/`ItemCarrinho` members from Task 4.

- [ ] **Step 1: Add per-item discount columns and item total to the cart grid**

In `Lojinha.App/Views/VendaView.xaml`, replace:

```xml
                    <DataGrid ItemsSource="{Binding Carrinho}" AutoGenerateColumns="False" IsReadOnly="True" MaxHeight="200"
                              Visibility="{Binding Carrinho.Count, Converter={StaticResource CountToVisibilityConverter}, ConverterParameter=Invert}">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="Produto" Binding="{Binding Produto}" Width="*" />
                            <DataGridTextColumn Header="Quantidade" Binding="{Binding Quantidade}" Width="100" />
                            <DataGridTextColumn Header="Preço unit." Binding="{Binding PrecoUnitario}" Width="100" />
                            <DataGridTextColumn Header="Subtotal" Binding="{Binding Subtotal}" Width="100" />
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
```

with:

```xml
                    <DataGrid ItemsSource="{Binding Carrinho}" AutoGenerateColumns="False" IsReadOnly="True" MaxHeight="200"
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
```

- [ ] **Step 2: Add sale-level discount, valor recebido, and troco fields**

Replace:

```xml
                    <WrapPanel Margin="0,12,0,0">
                        <ComboBox Width="160" Margin="0,0,8,0" ItemsSource="{Binding FormasPagamento}"
                                  SelectedItem="{Binding FormaPagamentoSelecionada}" />
                        <TextBlock Text="{Binding Total, StringFormat='Total: {0:C}'}" FontWeight="Bold" FontSize="16"
                                   VerticalAlignment="Center" Margin="12,0,0,0" />
                        <ui:Button Content="Finalizar venda" Appearance="Primary" Margin="12,0,0,0"
                                   Icon="{ui:SymbolIcon Symbol=ReceiptMoney24}"
                                   Command="{Binding FinalizarVendaCommand}" />
                    </WrapPanel>
```

with:

```xml
                    <WrapPanel Margin="0,12,0,0">
                        <ComboBox Width="90" Margin="0,0,4,8" ItemsSource="{Binding TiposDesconto}"
                                  SelectedItem="{Binding TipoDescontoVenda}" />
                        <ui:TextBox Width="90" Margin="0,0,8,8" PlaceholderText="Desconto"
                                    Text="{Binding DescontoVendaEntrada, UpdateSourceTrigger=PropertyChanged}" />
                        <ComboBox Width="160" Margin="0,0,8,8" ItemsSource="{Binding FormasPagamento}"
                                  SelectedItem="{Binding FormaPagamentoSelecionada}" />
                        <ui:TextBox Width="110" Margin="0,0,8,8" PlaceholderText="Valor recebido"
                                    Visibility="{Binding EhDinheiro, Converter={StaticResource BooleanToVisibilityConverter}}"
                                    Text="{Binding ValorRecebido, UpdateSourceTrigger=PropertyChanged}" />
                        <TextBlock Text="{Binding Troco, StringFormat='Troco: {0:C}'}" VerticalAlignment="Center" Margin="0,0,8,8"
                                   Visibility="{Binding EhDinheiro, Converter={StaticResource BooleanToVisibilityConverter}}" />
                        <TextBlock Text="{Binding Total, StringFormat='Total: {0:C}'}" FontWeight="Bold" FontSize="16"
                                   VerticalAlignment="Center" Margin="12,0,0,8" />
                        <ui:Button Content="Finalizar venda" Appearance="Primary" Margin="12,0,0,8"
                                   Icon="{ui:SymbolIcon Symbol=ReceiptMoney24}"
                                   Command="{Binding FinalizarVendaCommand}" />
                    </WrapPanel>
```

- [ ] **Step 3: Add discount and troco columns to the histórico grid**

Replace:

```xml
                            <DataGridTextColumn Header="Vendedor" Binding="{Binding UsuarioNome}" Width="120" />
```

with:

```xml
                            <DataGridTextColumn Header="Vendedor" Binding="{Binding UsuarioNome}" Width="120" />
                            <DataGridTextColumn Header="Desconto" Binding="{Binding DescontoValor, StringFormat=C}" Width="90" />
                            <DataGridTextColumn Header="Troco" Binding="{Binding Troco, StringFormat=C}" Width="90" />
```

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 62 tests total (unchanged — XAML-only task).

- [ ] **Step 6: Manual smoke check**

Run: `dotnet run --project Lojinha.App` in the background, confirm the process starts and stays alive, then terminate it. Full interactive verification (typing discounts, triggering the authorization prompt, checking troco) happens in Task 6's end-to-end walkthrough.

- [ ] **Step 7: Commit**

```bash
git add Lojinha.App/Views/VendaView.xaml
git commit -m "feat: add discount and troco fields to VendaView"
```

---

