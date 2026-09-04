using JKalixto_System.Application.Services;
using JKalixto_System.Presentation.ViewModels;

namespace JKalixto_System.Presentation.Pages;

public partial class CafeteriaPage : ContentPage
{
    private readonly CafeteriaViewModel _viewModel;

    public CafeteriaPage(CafeteriaViewModel viewModel)
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

    private void OnHuespedTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.BindingContext is HabitacionCardDto huesped &&
            _viewModel.SeleccionarHuespedCommand.CanExecute(huesped))
        {
            _viewModel.SeleccionarHuespedCommand.Execute(huesped);
        }
    }

    private void OnClienteSaunaTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.BindingContext is ClienteSaunaCardDto cliente &&
            _viewModel.SeleccionarClienteSaunaCommand.CanExecute(cliente))
        {
            _viewModel.SeleccionarClienteSaunaCommand.Execute(cliente);
        }
    }
}
