using JKalixto_System.Presentation.ViewModels;

namespace JKalixto_System.Presentation.Pages;

public partial class ReclamoNuevoPage : ContentPage
{
    public ReclamoNuevoPage(ReclamoNuevoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
