using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls.Shapes;
using JKalixto_System.Application.Services;
using JKalixto_System.Presentation.ViewModels;

namespace JKalixto_System.Presentation.Pages;

public partial class CalendarioPage : ContentPage
{
    private readonly CalendarioViewModel _viewModel;

    /// <summary>Columna fija de la izquierda: número de habitación + tipo abreviado.</summary>
    private const double AnchoColumnaHabitaciones = 104;

    /// <summary>
    /// Ancho de cada columna-día y alto de cada fila-habitación de la grilla principal.
    /// Dejaron de ser "const" para poder hacer zoom horizontal/vertical (ver
    /// OnAumentarAnchoDia/OnDisminuirAnchoDia/OnAumentarAltoFila/OnDisminuirAltoFila):
    /// los botones de zoom cambian estos valores dentro de un rango y vuelven a
    /// dibujar la grilla. AltoFilaMin=40 es a propósito el mínimo con el que la
    /// etiqueta de habitación (número + tipo, dos líneas) entra completa sin que la
    /// segunda línea quede tapada — con AltoFila=34 (el valor viejo) el bloque de
    /// texto necesitaba más alto del que había, y por eso se veía cortado.
    /// </summary>
    private double _anchoColumnaDia = 56;
    private const double AnchoColumnaDiaMin = 34;
    private const double AnchoColumnaDiaMax = 120;
    private const double AnchoColumnaDiaPaso = 12;

    private double _altoFila = 46;
    private const double AltoFilaMin = 40;
    private const double AltoFilaMax = 84;
    private const double AltoFilaPaso = 10;

    /// <summary>Fila fija de arriba: día de semana + número (el de hoy, en un círculo).</summary>
    private const double AltoFilaEncabezado = 42;

    /// <summary>Ancho/alto de cada celda del mini-calendario del panel lateral.</summary>
    private const double AltoFilaMiniCalendario = 26;

    private static readonly string[] DiasCorto = { "Dom", "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb" };
    private static readonly string[] InicialesDiasSemana = { "D", "L", "M", "X", "J", "V", "S" };

    public CalendarioPage(CalendarioViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _viewModel.CalendarioActualizado += OnCalendarioActualizado;

#if WINDOWS
        // "HandlerChanged" avisa exactamente cuando la vista nativa (el ScrollViewer
        // de verdad, no el ScrollView de MAUI) ya existe — ahí recién se puede
        // enganchar el evento de la rueda del mouse. Ver ConfigurarRuedaDelMouse.
        ScrollHorizontalPrincipal.HandlerChanged += (_, _) => ConfigurarRuedaDelMouse();
#endif
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

    /// <summary>Único punto de acceso a los colores del tema en este archivo — evita
    /// escribir "Microsoft.Maui.Controls.Application.Current" muchas veces, y usa el
    /// nombre completo (no "Application.Current" a secas) por la misma razón que en
    /// Servicios.cs: "Application" es ambiguo en este proyecto porque también existe
    /// el namespace JKalixto_System.Application (Clean Architecture).</summary>
    private static Color ObtenerColorDelTema(string clave)
    {
        return (Color)Microsoft.Maui.Controls.Application.Current!.Resources[clave];
    }

    /// <summary>
    /// La grilla principal scrollea con DOS ScrollView anidados (uno vertical afuera,
    /// uno horizontal adentro) en vez de un solo ScrollView "Orientation=Both": en
    /// Windows, un único ScrollView de ambas direcciones no siempre responde bien al
    /// gesto de scroll horizontal (rueda del mouse + Shift, arrastre de la barra,
    /// touchpad) — separarlos en dos hace que cada dirección sea 100% confiable, al
    /// costo de necesitar un handler de "Scrolled" por cada uno.
    ///
    /// El encabezado de días y las etiquetas de habitación son estáticos por sí
    /// mismos, así que se "arrastran" con TranslationX/Y para que se sientan
    /// congelados (fijos) mientras el usuario scrollea el resto — el mismo efecto
    /// visual que un panel congelado de Excel o Google Calendar, sin depender de
    /// ningún paquete externo.
    /// </summary>
    private void OnScrollVerticalPrincipal(object? sender, ScrolledEventArgs e)
    {
        GrillaEtiquetasHabitaciones.TranslationY = -e.ScrollY;
    }

    private void OnScrollHorizontalPrincipal(object? sender, ScrolledEventArgs e)
    {
        GrillaEncabezadoDias.TranslationX = -e.ScrollX;
    }

#if WINDOWS
    private bool _ruedaDelMouseConfigurada;

    /// <summary>
    /// MAUI no expone en forma multiplataforma si Ctrl/Alt estaban apretados durante
    /// un gesto de scroll, así que esto engancha el evento nativo de Windows
    /// (PointerWheelChanged) directo sobre el ScrollViewer de verdad que hay debajo
    /// del ScrollView horizontal — es el que recibe el evento primero (está más
    /// "adentro" que el vertical), así que interceptarlo acá alcanza para las dos
    /// combinaciones sin tocar el ScrollView vertical:
    ///   Ctrl + rueda  → zoom (ancho de columnas + alto de filas a la vez)
    ///   Alt + rueda   → scroll horizontal (en vez del vertical de siempre)
    ///   Rueda sola    → se deja pasar sin marcar Handled, así sigue scrolleando
    ///                    vertical como siempre (sube al ScrollView de afuera).
    /// </summary>
    private void ConfigurarRuedaDelMouse()
    {
        if (_ruedaDelMouseConfigurada)
        {
            return;
        }

        if (ScrollHorizontalPrincipal.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.ScrollViewer scrollViewerNativo)
        {
            scrollViewerNativo.PointerWheelChanged += OnRuedaDelMouseSobreGrilla;
            _ruedaDelMouseConfigurada = true;
        }
    }

    private void OnRuedaDelMouseSobreGrilla(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var conCtrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var conAlt = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (!conCtrl && !conAlt)
        {
            return; // rueda sola: se deja pasar, sigue scrolleando vertical como siempre
        }

        var scrollViewerNativo = (Microsoft.UI.Xaml.Controls.ScrollViewer)sender;
        var delta = e.GetCurrentPoint(scrollViewerNativo).Properties.MouseWheelDelta;
        e.Handled = true;

        if (conCtrl)
        {
            // "pasos" en vez de un paso fijo: en mouses/touchpads de precisión cada
            // "tick" reporta un delta más chico que 120, así el zoom se siente
            // fluido en vez de saltar de golpe.
            var pasos = delta / 120.0;
            CambiarAnchoColumnaDia(pasos * AnchoColumnaDiaPaso);
            CambiarAltoFila(pasos * AltoFilaPaso);
        }
        else
        {
            scrollViewerNativo.ChangeView(scrollViewerNativo.HorizontalOffset - delta, null, null);
        }
    }
#endif

    /// <summary>Zoom horizontal: ancho de cada columna-día. Afecta cuánto texto entra
    /// legible en el nombre del huésped dentro de cada tramo del calendario.</summary>
    private void OnDisminuirAnchoDia(object? sender, EventArgs e) => CambiarAnchoColumnaDia(-AnchoColumnaDiaPaso);
    private void OnAumentarAnchoDia(object? sender, EventArgs e) => CambiarAnchoColumnaDia(AnchoColumnaDiaPaso);

    /// <summary>Zoom vertical: alto de cada fila-habitación. Nunca baja de AltoFilaMin
    /// (40) porque con menos que eso la etiqueta de habitación (número + tipo, dos
    /// líneas) no entra completa y se corta — el bug que reportaron.</summary>
    private void OnDisminuirAltoFila(object? sender, EventArgs e) => CambiarAltoFila(-AltoFilaPaso);
    private void OnAumentarAltoFila(object? sender, EventArgs e) => CambiarAltoFila(AltoFilaPaso);

    private void CambiarAnchoColumnaDia(double delta)
    {
        var nuevoAncho = Math.Clamp(_anchoColumnaDia + delta, AnchoColumnaDiaMin, AnchoColumnaDiaMax);
        if (nuevoAncho == _anchoColumnaDia)
        {
            return;
        }

        _anchoColumnaDia = nuevoAncho;
        RedibujarSiHayDatos();
    }

    private void CambiarAltoFila(double delta)
    {
        var nuevoAlto = Math.Clamp(_altoFila + delta, AltoFilaMin, AltoFilaMax);
        if (nuevoAlto == _altoFila)
        {
            return;
        }

        _altoFila = nuevoAlto;
        RedibujarSiHayDatos();
    }

    private void RedibujarSiHayDatos()
    {
        var calendario = _viewModel.Calendario;
        if (calendario is not null)
        {
            DibujarGrilla(calendario);
        }
    }

    private void OnCalendarioActualizado()
    {
        var calendario = _viewModel.Calendario;
        if (calendario is not null)
        {
            DibujarGrilla(calendario);
        }
    }

    /// <summary>
    /// Arma las 3 grillas: encabezado de días (fijo verticalmente), etiquetas de
    /// habitación (fijas horizontalmente) y la grilla principal con las celdas de
    /// color. HABITACIONES en filas y DÍAS DEL MES en columnas — así se lee igual
    /// que un calendario de reservas real: cada fila muestra de un vistazo la
    /// ocupación del mes completo de una habitación, y cada columna muestra la
    /// disponibilidad de todo el hotel en un día puntual.
    /// </summary>
    private void DibujarGrilla(CalendarioMensualDto calendario)
    {
        var colorTextoSecundario = ObtenerColorDelTema("ColorTextoSecundario");
        var colorTextoPrimario = ObtenerColorDelTema("ColorTextoPrimario");
        var colorAcento = ObtenerColorDelTema("ColorAcento");
        var colorFondo = ObtenerColorDelTema("ColorFondo");
        var colorSuperficie = ObtenerColorDelTema("ColorSuperficie");
        var colorBorde = ObtenerColorDelTema("ColorBorde");

        var hoy = DateTime.Now.Date;

        DibujarEncabezadoDias(calendario, hoy, colorTextoSecundario, colorTextoPrimario, colorAcento, colorFondo);
        DibujarEtiquetasHabitaciones(calendario, colorTextoPrimario, colorTextoSecundario, colorSuperficie);
        DibujarCeldas(calendario, hoy, colorBorde, colorTextoPrimario);
        DibujarMiniCalendario(calendario, hoy, colorTextoSecundario, colorTextoPrimario, colorAcento, colorFondo);
    }

    /// <summary>Calendario chico del panel lateral: mismo mes que la grilla grande.
    /// Tocar un día scrollea la grilla principal hasta esa columna (ver
    /// IrAlDiaEnGrillaPrincipalAsync) — sirve para saltar rápido sin tener que
    /// arrastrar el scroll horizontal a mano por 30 columnas.</summary>
    private void DibujarMiniCalendario(
        CalendarioMensualDto calendario, DateTime hoy,
        Color colorTextoSecundario, Color colorTextoPrimario, Color colorAcento, Color colorFondo)
    {
        GrillaMiniCalendario.Children.Clear();
        GrillaMiniCalendario.RowDefinitions.Clear();
        GrillaMiniCalendario.ColumnDefinitions.Clear();

        for (var c = 0; c < 7; c++)
        {
            GrillaMiniCalendario.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        GrillaMiniCalendario.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
        for (var c = 0; c < 7; c++)
        {
            var encabezado = new Label
            {
                Text = InicialesDiasSemana[c],
                FontSize = 14,
                HorizontalTextAlignment = TextAlignment.Center,
                TextColor = colorTextoSecundario
            };
            Grid.SetRow(encabezado, 0);
            Grid.SetColumn(encabezado, c);
            GrillaMiniCalendario.Children.Add(encabezado);
        }

        var primerDiaDelMes = new DateTime(calendario.Anio, calendario.Mes, 1);
        var columnaInicio = (int)primerDiaDelMes.DayOfWeek; // 0 = domingo
        var filasNecesarias = (int)Math.Ceiling((columnaInicio + calendario.Dias.Count) / 7.0);

        for (var f = 0; f < filasNecesarias; f++)
        {
            GrillaMiniCalendario.RowDefinitions.Add(new RowDefinition { Height = new GridLength(AltoFilaMiniCalendario) });
        }

        foreach (var dia in calendario.Dias)
        {
            var indiceCelda = columnaInicio + (dia - 1);
            var fila = 1 + (indiceCelda / 7);
            var columna = indiceCelda % 7;
            var fecha = new DateTime(calendario.Anio, calendario.Mes, dia);
            var esHoy = fecha == hoy;

            View celdaDia;
            if (esHoy)
            {
                celdaDia = new Border
                {
                    BackgroundColor = colorAcento,
                    WidthRequest = 22,
                    HeightRequest = 22,
                    Padding = 0,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    StrokeShape = new RoundRectangle { CornerRadius = 11 },
                    Content = new Label
                    {
                        Text = dia.ToString(),
                        FontSize = 16,
                        FontAttributes = FontAttributes.Bold,
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center,
                        TextColor = colorFondo
                    }
                };
            }
            else
            {
                celdaDia = new Label
                {
                    Text = dia.ToString(),
                    FontSize = 18,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center,
                    TextColor = colorTextoPrimario
                };
            }

            var diaCapturado = dia;
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (s, e) => await IrAlDiaEnGrillaPrincipalAsync(diaCapturado);
            celdaDia.GestureRecognizers.Add(tap);

            Grid.SetRow(celdaDia, fila);
            Grid.SetColumn(celdaDia, columna);
            GrillaMiniCalendario.Children.Add(celdaDia);
        }
    }

    /// <summary>Lleva la grilla principal (scroll horizontal) hasta la columna del día
    /// elegido en el mini-calendario, sin tocar el scroll vertical actual.</summary>
    private async Task IrAlDiaEnGrillaPrincipalAsync(int dia)
    {
        var x = (dia - 1) * _anchoColumnaDia;
        await ScrollHorizontalPrincipal.ScrollToAsync(x, 0, true);
    }

    private void DibujarEncabezadoDias(
        CalendarioMensualDto calendario, DateTime hoy,
        Color colorTextoSecundario, Color colorTextoPrimario, Color colorAcento, Color colorFondo)
    {
        GrillaEncabezadoDias.Children.Clear();
        GrillaEncabezadoDias.RowDefinitions.Clear();
        GrillaEncabezadoDias.ColumnDefinitions.Clear();

        GrillaEncabezadoDias.RowDefinitions.Add(new RowDefinition { Height = new GridLength(AltoFilaEncabezado) });
        for (var i = 0; i < calendario.Dias.Count; i++)
        {
            GrillaEncabezadoDias.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_anchoColumnaDia) });
        }

        for (var col = 0; col < calendario.Dias.Count; col++)
        {
            var dia = calendario.Dias[col];
            var fecha = new DateTime(calendario.Anio, calendario.Mes, dia);
            var esHoy = fecha == hoy;
            var esFinDeSemana = fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            var contenedor = new VerticalStackLayout
            {
                Spacing = 2,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            contenedor.Children.Add(new Label
            {
                Text = DiasCorto[(int)fecha.DayOfWeek].ToUpperInvariant(),
                FontSize = 14,
                HorizontalTextAlignment = TextAlignment.Center,
                TextColor = esFinDeSemana ? colorAcento : colorTextoSecundario
            });

            if (esHoy)
            {
                contenedor.Children.Add(new Border
                {
                    BackgroundColor = colorAcento,
                    WidthRequest = 23,
                    HeightRequest = 23,
                    Padding = 0,
                    HorizontalOptions = LayoutOptions.Center,
                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                    Content = new Label
                    {
                        Text = dia.ToString("00"),
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center,
                        TextColor = colorFondo
                    }
                });
            }
            else
            {
                contenedor.Children.Add(new Label
                {
                    Text = dia.ToString("00"),
                    FontSize = 19,
                    HorizontalTextAlignment = TextAlignment.Center,
                    TextColor = colorTextoPrimario
                });
            }

            Grid.SetColumn(contenedor, col);
            GrillaEncabezadoDias.Children.Add(contenedor);
        }
    }

    private void DibujarEtiquetasHabitaciones(
        CalendarioMensualDto calendario, Color colorTextoPrimario, Color colorTextoSecundario, Color colorSuperficie)
    {
        GrillaEtiquetasHabitaciones.Children.Clear();
        GrillaEtiquetasHabitaciones.RowDefinitions.Clear();
        GrillaEtiquetasHabitaciones.ColumnDefinitions.Clear();

        GrillaEtiquetasHabitaciones.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(AnchoColumnaHabitaciones) });
        for (var i = 0; i < calendario.Columnas.Count; i++)
        {
            GrillaEtiquetasHabitaciones.RowDefinitions.Add(new RowDefinition { Height = new GridLength(_altoFila) });
        }

        for (var fila = 0; fila < calendario.Columnas.Count; fila++)
        {
            var columnaHabitacion = calendario.Columnas[fila];

            // Franjas alternadas (zebra) para que el ojo no pierda la fila al mirar
            // 30 columnas de ancho — el mismo truco que usan las hojas de cálculo.
            var fondoFila = fila % 2 == 0 ? colorSuperficie : Colors.Transparent;

            var celda = new Grid
            {
                BackgroundColor = fondoFila,
                Padding = new Thickness(6, 0),
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                }
            };

            var contenido = new VerticalStackLayout
            {
                Spacing = 0,
                VerticalOptions = LayoutOptions.Center
            };
            contenido.Children.Add(new Label
            {
                Text = columnaHabitacion.Numero.ToString(),
                FontSize = 21,
                FontAttributes = FontAttributes.Bold,
                TextColor = colorTextoPrimario
            });
            contenido.Children.Add(new Label
            {
                Text = columnaHabitacion.EtiquetaTipo,
                FontSize = 14,
                TextColor = colorTextoSecundario,
                LineBreakMode = LineBreakMode.NoWrap
            });
            celda.Children.Add(contenido);

            Grid.SetRow(celda, fila);
            GrillaEtiquetasHabitaciones.Children.Add(celda);
        }
    }

    private void DibujarCeldas(CalendarioMensualDto calendario, DateTime hoy, Color colorBorde, Color colorTextoPrimario)
    {
        GrillaCalendario.Children.Clear();
        GrillaCalendario.RowDefinitions.Clear();
        GrillaCalendario.ColumnDefinitions.Clear();

        for (var i = 0; i < calendario.Columnas.Count; i++)
        {
            GrillaCalendario.RowDefinitions.Add(new RowDefinition { Height = new GridLength(_altoFila) });
        }
        for (var i = 0; i < calendario.Dias.Count; i++)
        {
            GrillaCalendario.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_anchoColumnaDia) });
        }

        for (var fila = 0; fila < calendario.Columnas.Count; fila++)
        {
            DibujarFilaComoTramos(calendario, calendario.Columnas[fila], fila, colorBorde);
        }
    }

    /// <summary>
    /// En vez de crear una vista nativa por cada día (36 habitaciones × ~30 días =
    /// ~1000 Border/Label/gestos, todos armados de una sola vez porque el Grid de MAUI
    /// no "recicla" celdas fuera de pantalla como sí lo hace un CollectionView), se
    /// agrupan los días CONSECUTIVOS de una misma habitación que comparten estado y
    /// cliente en un solo "tramo" con ColumnSpan — igual que un evento de varios días
    /// en Google Calendar. En un mes típico, la mayoría de una fila está Disponible de
    /// punta a punta, así que esto pasa de ~30 vistas por fila a un puñado, sin perder
    /// información: el tramo ocupado/reservado sigue mostrando el nombre del cliente,
    /// y tocarlo abre el mismo detalle o la misma reserva rápida que antes (usando el
    /// primer día del tramo tocado).
    /// </summary>
    private void DibujarFilaComoTramos(CalendarioMensualDto calendario, ColumnaHabitacionCalendarioDto columnaHabitacion, int fila, Color colorBorde)
    {
        var celdas = columnaHabitacion.Celdas;
        var col = 0;
        while (col < celdas.Count)
        {
            var inicio = col;
            var estado = celdas[col].Estado;
            var cliente = celdas[col].NombreCliente;

            var fin = col;
            while (fin + 1 < celdas.Count && celdas[fin + 1].Estado == estado && celdas[fin + 1].NombreCliente == cliente)
            {
                fin++;
            }

            AgregarTramo(calendario, columnaHabitacion, fila, inicio, fin - inicio + 1, estado, cliente, colorBorde);
            col = fin + 1;
        }
    }

    private void AgregarTramo(
        CalendarioMensualDto calendario, ColumnaHabitacionCalendarioDto columnaHabitacion, int fila,
        int colInicio, int longitud, EstadoCeldaCalendario estado, string? nombreCliente, Color colorBorde)
    {
        var colorEstado = ColorParaEstadoCelda(estado);
        var esDisponible = estado == EstadoCeldaCalendario.Disponible;

        // Border en vez de BoxView: así el tramo puede mostrar el nombre del
        // huésped/cliente adentro (como los "chips" de evento de un calendario real),
        // no solo un bloque de color sin información.
        var caja = new Border
        {
            // Disponible no tiene nada que mostrar (sin cliente), así que pintar todo
            // el bloque sería demasiado peso visual — se deja transparente y en vez de
            // eso se dibuja solo una línea delgada abajo (ver más adelante).
            BackgroundColor = esDisponible ? Colors.Transparent : colorEstado,
            Stroke = Colors.Transparent,
            StrokeThickness = 1.5,
            Padding = new Thickness(4, 0),
            StrokeShape = new RoundRectangle { CornerRadius = 4 }
        };

        if (esDisponible)
        {
            caja.Content = new BoxView
            {
                BackgroundColor = colorEstado,
                HeightRequest = 4,
                CornerRadius = 2,
                Margin = new Thickness(2, 0),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.End
            };
        }
        else if (!string.IsNullOrWhiteSpace(nombreCliente))
        {
            var primerNombre = nombreCliente
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? nombreCliente;
            caja.Content = new Label
            {
                Text = primerNombre,
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                LineBreakMode = LineBreakMode.TailTruncation,
                HorizontalTextAlignment = longitud > 1 ? TextAlignment.Start : TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Colors.White
            };
        }

        // Se capturan estos valores en variables locales propias de esta vuelta del
        // loop (no la variable del loop en sí), para que cada tramo recuerde SU
        // PROPIA habitación/día-de-inicio al tocarlo más tarde.
        var habitacionIdCapturada = columnaHabitacion.HabitacionId;
        var numeroHabitacionCapturado = columnaHabitacion.Numero;
        var tipoHabitacionCapturado = columnaHabitacion.EtiquetaTipo;
        var diaCapturado = calendario.Dias[colInicio];
        var estadoCapturado = estado;
        var nombreClienteCapturado = nombreCliente;

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) => await ManejarTapCeldaAsync(
            habitacionIdCapturada, numeroHabitacionCapturado, tipoHabitacionCapturado, diaCapturado,
            calendario.Mes, calendario.Anio, estadoCapturado, nombreClienteCapturado);
        caja.GestureRecognizers.Add(tap);

        // Resalta el borde del tramo al pasar el mouse por encima (Windows/Mac
        // Catalyst) — un detalle de app de escritorio que ayuda a "apuntar" bien en
        // una grilla tan densa.
        var puntero = new PointerGestureRecognizer();
        puntero.PointerEntered += (s, e) => { caja.Stroke = colorBorde; };
        puntero.PointerExited += (s, e) => { caja.Stroke = Colors.Transparent; };
        caja.GestureRecognizers.Add(puntero);

        Grid.SetRow(caja, fila);
        Grid.SetColumn(caja, colInicio);
        Grid.SetColumnSpan(caja, longitud);
        GrillaCalendario.Children.Add(caja);
    }

    private static Color ColorParaEstadoCelda(EstadoCeldaCalendario estado) => estado switch
    {
        EstadoCeldaCalendario.Ocupada => ObtenerColorDelTema("ColorOcupada"),
        EstadoCeldaCalendario.Reservada => ObtenerColorDelTema("ColorReservada"),
        EstadoCeldaCalendario.Mantenimiento => ObtenerColorDelTema("ColorMantenimiento"),
        _ => ObtenerColorDelTema("ColorDisponible")
    };

    /// <summary>Una celda Disponible abre directo el formulario de Nueva Reserva (con la
    /// habitación y la fecha ya elegidas) para poder reservar rápido sin salir del
    /// Calendario. Las demás celdas (Ocupada/Reservada/Mantenimiento) solo muestran su
    /// detalle informativo, como antes.</summary>
    private async Task ManejarTapCeldaAsync(
        int habitacionId, int numeroHabitacion, string tipoHabitacion, int dia, int mes, int anio,
        EstadoCeldaCalendario estado, string? nombreCliente)
    {
        if (estado == EstadoCeldaCalendario.Disponible)
        {
            await AbrirReservaRapidaAsync(habitacionId, dia, mes, anio);
            return;
        }

        await MostrarDetalleCeldaAsync(numeroHabitacion, tipoHabitacion, dia, mes, anio, estado, nombreCliente);
    }

    private static async Task AbrirReservaRapidaAsync(int habitacionId, int dia, int mes, int anio)
    {
        var fechaInicio = new DateTime(anio, mes, dia);
        var fechaFin = fechaInicio.AddDays(1);

        await Shell.Current.GoToAsync(
            $"{nameof(ReservaNuevaPage)}?habitacionId={habitacionId}&fechaInicio={fechaInicio:yyyy-MM-dd}&fechaFin={fechaFin:yyyy-MM-dd}");
    }

    private async Task MostrarDetalleCeldaAsync(
        int numeroHabitacion, string tipoHabitacion, int dia, int mes, int anio,
        EstadoCeldaCalendario estado, string? nombreCliente)
    {
        var meses = new[] { "", "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio",
                             "agosto", "septiembre", "octubre", "noviembre", "diciembre" };
        var fecha = new DateTime(anio, mes, dia);
        var fechaTexto = $"{DiasCorto[(int)fecha.DayOfWeek]} {dia:00} de {meses[mes]} del {anio}";

        var etiquetaEstado = estado switch
        {
            EstadoCeldaCalendario.Ocupada => "Ocupada",
            EstadoCeldaCalendario.Reservada => "Reservada",
            EstadoCeldaCalendario.Mantenimiento => "Mantenimiento",
            _ => "Disponible"
        };

        var mensaje = $"{fechaTexto}\nEstado: {etiquetaEstado}";
        if (!string.IsNullOrWhiteSpace(nombreCliente))
        {
            mensaje += $"\nCliente: {nombreCliente}";
        }

        await DisplayAlertAsync($"Habitación {numeroHabitacion} — {tipoHabitacion}", mensaje, "Cerrar");
    }
}
