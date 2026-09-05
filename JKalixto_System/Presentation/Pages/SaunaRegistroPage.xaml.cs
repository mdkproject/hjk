using JKalixto_System.Presentation.ViewModels;

namespace JKalixto_System.Presentation.Pages;

public partial class SaunaRegistroPage : ContentPage
{
    private readonly SaunaRegistroViewModel _viewModel;

    public SaunaRegistroPage(SaunaRegistroViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        // Enter confirma la sugerencia resaltada — funciona en toda plataforma
        // porque Entry.Completed es parte de MAUI, no requiere código nativo.
        EntryBusquedaHuesped.Completed += (s, e) => _viewModel.ConfirmarResaltadoCommand.Execute(null);

#if WINDOWS
        // Las flechas ↑/↓ para moverse entre sugerencias necesitan el control nativo
        // de Windows (MAUI no expone un evento de tecla multiplataforma para Entry).
        EntryBusquedaHuesped.HandlerChanged += (s, e) =>
        {
            if (EntryBusquedaHuesped.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBox textBox)
            {
                textBox.PreviewKeyDown += (sender, args) =>
                {
                    if (args.Key == Windows.System.VirtualKey.Down)
                    {
                        _viewModel.MoverResaltadoCommand.Execute("Abajo");
                        args.Handled = true;
                    }
                    else if (args.Key == Windows.System.VirtualKey.Up)
                    {
                        _viewModel.MoverResaltadoCommand.Execute("Arriba");
                        args.Handled = true;
                    }
                };
            }
        };
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.CargarAsync();
    }
}
