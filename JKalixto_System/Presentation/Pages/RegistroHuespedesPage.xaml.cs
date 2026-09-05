using JKalixto_System.Presentation.ViewModels;

namespace JKalixto_System.Presentation.Pages;

public partial class RegistroHuespedesPage : ContentPage
{
    private readonly RegistroHuespedesViewModel _viewModel;

    public RegistroHuespedesPage(RegistroHuespedesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.IniciarReloj();
        await _viewModel.CargarAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.DetenerReloj();
    }
}
