using JKalixto_System.Application.Services;
using JKalixto_System.Presentation.ViewModels;

namespace JKalixto_System.Presentation.Pages;

public partial class SaunaVentaPage : ContentPage
{
    private readonly SaunaVentaViewModel _viewModel;

    public SaunaVentaPage(SaunaVentaViewModel viewModel)
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

    private void OnProductoTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.BindingContext is ProductoCatalogoDto producto &&
            _viewModel.AgregarProductoCommand.CanExecute(producto))
        {
            _viewModel.AgregarProductoCommand.Execute(producto);
        }
    }

    private void OnQuitarItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.BindingContext is ItemCarritoDto item &&
            _viewModel.QuitarItemCommand.CanExecute(item))
        {
            _viewModel.QuitarItemCommand.Execute(item);
        }
    }
}
