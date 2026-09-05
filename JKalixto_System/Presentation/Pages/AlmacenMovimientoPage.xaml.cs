using JKalixto_System.Presentation.ViewModels;

namespace JKalixto_System.Presentation.Pages;

public partial class AlmacenMovimientoPage : ContentPage
{
    private readonly AlmacenMovimientoViewModel _viewModel;

    public AlmacenMovimientoPage(AlmacenMovimientoViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.CargarAsync();
    }
}
