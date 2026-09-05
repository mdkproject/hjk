using JKalixto_System.Application.Services;
using JKalixto_System.Presentation.ViewModels;

namespace JKalixto_System.Presentation.Pages;

public partial class ClientesPage : ContentPage
{
    private readonly ClientesViewModel _viewModel;

    public ClientesPage(ClientesViewModel viewModel)
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

    private void OnClienteTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.BindingContext is ClienteUnificadoDto cliente &&
            _viewModel.SeleccionarClienteCommand.CanExecute(cliente))
        {
            _viewModel.SeleccionarClienteCommand.Execute(cliente);
        }
    }
}
