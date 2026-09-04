using JKalixto_System.Presentation.ViewModels;

namespace JKalixto_System.Presentation.Pages;

public partial class ReservaNuevaPage : ContentPage
{
    private readonly ReservaNuevaViewModel _viewModel;

    public ReservaNuevaPage(ReservaNuevaViewModel viewModel)
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

    private void OnQuitarAcompananteTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.BindingContext is string nombre &&
            _viewModel.QuitarAcompananteCommand.CanExecute(nombre))
        {
            _viewModel.QuitarAcompananteCommand.Execute(nombre);
        }
    }
}
