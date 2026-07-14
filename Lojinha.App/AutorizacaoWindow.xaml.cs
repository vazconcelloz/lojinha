using Lojinha.App.ViewModels;
using Wpf.Ui.Controls;

namespace Lojinha.App;

public partial class AutorizacaoWindow : FluentWindow
{
    public AutorizacaoWindow(AutorizacaoViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.AutorizacaoConcedida += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }

    public string? NomeAutorizador => (DataContext as AutorizacaoViewModel)?.NomeAutorizador;
}
