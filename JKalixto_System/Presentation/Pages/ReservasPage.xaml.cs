using JKalixto_System.Application.Services;
using JKalixto_System.Presentation.ViewModels;

namespace JKalixto_System.Presentation.Pages;

public partial class ReservasPage : ContentPage
{
    private readonly ReservasViewModel _viewModel;

    public ReservasPage(ReservasViewModel viewModel)
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

    private void OnReservaTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.BindingContext is ReservaCardDto reserva &&
            _viewModel.SeleccionarReservaCommand.CanExecute(reserva))
        {
            _viewModel.SeleccionarReservaCommand.Execute(reserva);
        }
    }
}
