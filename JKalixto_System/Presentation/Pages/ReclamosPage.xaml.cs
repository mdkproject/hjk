using JKalixto_System.Application.Services;
using JKalixto_System.Presentation.ViewModels;

namespace JKalixto_System.Presentation.Pages;

public partial class ReclamosPage : ContentPage
{
    private readonly ReclamosViewModel _viewModel;

    public ReclamosPage(ReclamosViewModel viewModel)
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

    private void OnReclamoTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.BindingContext is ReclamoCardDto reclamo &&
            _viewModel.SeleccionarReclamoCommand.CanExecute(reclamo))
        {
            _viewModel.SeleccionarReclamoCommand.Execute(reclamo);
        }
    }
}
