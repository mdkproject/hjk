using JKalixto_System.Application.Services;
using JKalixto_System.Presentation.ViewModels;

namespace JKalixto_System.Presentation.Pages;

public partial class RecepcionPage : ContentPage
{
    private readonly RecepcionViewModel _viewModel;

    public RecepcionPage(RecepcionViewModel viewModel)
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

    /// <summary>
    /// Se dispara al tocar una tarjeta de habitación. Se maneja en code-behind
    /// (en vez de un binding con RelativeSource en el XAML) porque es más simple
    /// y evita errores sutiles de binding entre el DataTemplate y el ViewModel de la página.
    /// </summary>
    private void OnHabitacionTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.BindingContext is HabitacionCardDto habitacion &&
            _viewModel.SeleccionarHabitacionCommand.CanExecute(habitacion))
        {
            _viewModel.SeleccionarHabitacionCommand.Execute(habitacion);
        }
    }
}
