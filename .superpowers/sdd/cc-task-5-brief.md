### Task 5: `EnumToVisibilityConverter`

**Files:**
- Create: `Lojinha.App/Converters/EnumToVisibilityConverter.cs`
- Modify: `Lojinha.App/App.xaml`

**Interfaces:**
- Produces: `EnumToVisibilityConverter` registered as the `EnumToVisibilityConverter` resource key — consumed by Task 6 (`VendaView.xaml`).

- [ ] **Step 1: Create the converter**

Create `Lojinha.App/Converters/EnumToVisibilityConverter.cs`:

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Lojinha.App.Converters;

public class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
        {
            return Visibility.Collapsed;
        }

        var visible = string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
```

- [ ] **Step 2: Register it as an `App.xaml` resource**

In `Lojinha.App/App.xaml`, replace:

```xml
            <converters:BooleanAndToVisibilityConverter x:Key="BooleanAndToVisibilityConverter" />
```

with:

```xml
            <converters:BooleanAndToVisibilityConverter x:Key="BooleanAndToVisibilityConverter" />
            <converters:EnumToVisibilityConverter x:Key="EnumToVisibilityConverter" />
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: `Compilação com êxito. 0 Erro(s)`

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test`
Expected: PASS, 81 tests total (unchanged).

- [ ] **Step 5: Commit**

```bash
git add Lojinha.App/Converters/EnumToVisibilityConverter.cs Lojinha.App/App.xaml
git commit -m "feat: add EnumToVisibilityConverter for the 3-way Caixa tab toggle"
```

---

