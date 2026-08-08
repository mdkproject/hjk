using JKalixto_System.Presentation.ViewModels;

namespace JKalixto_System.Presentation.Pages;

public partial class CheckInPage : ContentPage
{
    private readonly CheckInViewModel _viewModel;

    public CheckInPage(CheckInViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
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
