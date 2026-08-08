using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using JKalixto_System.Application.Services;
using JKalixto_System.Presentation.Pages;

namespace JKalixto_System.Presentation.ViewModels;

public class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly ISessionService _sessionService;
    private readonly IAuditoriaService _auditoriaService;

    private string _username = string.Empty;
    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    private string _password = string.Empty;
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    private string _mensajeError = string.Empty;
    public string MensajeError
    {
        get => _mensajeError;
        set => SetProperty(ref _mensajeError, value);
    }

    private bool _hayError;
    public bool HayError
    {
        get => _hayError;
        set => SetProperty(ref _hayError, value);
    }

    public ICommand IniciarSesionCommand { get; }

    public LoginViewModel(IAuthService authService, ISessionService sessionService, IAuditoriaService auditoriaService)
    {
        _authService = authService;
        _sessionService = sessionService;
        _auditoriaService = auditoriaService;
        Title = "Iniciar Sesión";
        IniciarSesionCommand = new Command(async () => await IniciarSesionAsync());
    }

    private async Task IniciarSesionAsync()
    {
        if (IsBusy)
        {
            return;
        }

        HayError = false;
        MensajeError = string.Empty;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            MensajeError = "Por favor ingrese usuario y contraseña.";
            HayError = true;
            return;
        }

        try
        {
            IsBusy = true;

            var resultado = await _authService.IniciarSesionAsync(Username.Trim(), Password);

            if (!resultado.Exito || resultado.Usuario is null)
            {
                MensajeError = resultado.Mensaje;
                HayError = true;
                return;
            }

            _sessionService.UsuarioActual = resultado.Usuario;
            Password = string.Empty;

            await _auditoriaService.RegistrarAsync(
                "LOGIN",
                $"{resultado.Usuario.NombreCompleto} inició sesión.",
                resultado.Usuario.Id, "Usuario", resultado.Usuario.Id);

            await Shell.Current.GoToAsync($"//{nameof(DashboardPage)}");
        }
        catch (Exception ex)
        {
            MensajeError = "Ocurrió un error al iniciar sesión. Intente nuevamente.";
            HayError = true;
            System.Diagnostics.Debug.WriteLine($"Error de login: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
