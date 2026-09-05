using JKalixto_System.Presentation.ViewModels;

namespace JKalixto_System.Presentation.Pages;

public partial class GastoNuevoPage : ContentPage
{
    public GastoNuevoPage(GastoNuevoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
