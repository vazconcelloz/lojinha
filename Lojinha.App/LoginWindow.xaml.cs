using Lojinha.App.ViewModels;
using Wpf.Ui.Controls;

namespace Lojinha.App;

public partial class LoginWindow : FluentWindow
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.LoginBemSucedido += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }
}
