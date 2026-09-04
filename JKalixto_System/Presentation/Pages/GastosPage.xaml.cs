using JKalixto_System.Application.Services;
using JKalixto_System.Presentation.ViewModels;

namespace JKalixto_System.Presentation.Pages;

public partial class GastosPage : ContentPage
{
    private readonly GastosViewModel _viewModel;

    public GastosPage(GastosViewModel viewModel)
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

    private void OnMovimientoTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.BindingContext is MovimientoCajaCardDto movimiento &&
            _viewModel.SeleccionarMovimientoCommand.CanExecute(movimiento))
        {
            _viewModel.SeleccionarMovimientoCommand.Execute(movimiento);
        }
    }
}
