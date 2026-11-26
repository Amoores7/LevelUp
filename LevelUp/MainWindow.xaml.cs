using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LevelUp.Models;
using LevelUp.Services;
using LevelUp.Views;

namespace LevelUp
{
    /// <summary>
    /// Ventana principal. Este archivo tiene lo común: carga de datos, navegación,
    /// mensajes de estado y la vista Inicio. Cada vista tiene su propio archivo parcial:
    /// MainWindow.Misiones.cs, MainWindow.Actividad.cs, MainWindow.Logros.cs y MainWindow.Perfil.cs.
    /// </summary>
    public partial class MainWindow : Window
    {
        private enum TipoEstado { Cargando, Correcto, Error, Info }

        private readonly Sesion sesion;

        // Datos cargados desde Firebase
        private List<Mision> misiones = new();
        private List<Actividad> actividades = new();
        private List<Logro> catalogoLogros = new();
        private Progreso progreso = new();

        /// <summary>true mientras hay una petición en curso (se deshabilitan los botones).</summary>
        private bool ocupado;

        private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Now);

        public MainWindow(Sesion sesion)
        {
            InitializeComponent();
            this.sesion = sesion;

            PrepararListasDesplegables();
            MostrarDatosUsuario();
            MostrarVista(vistaInicio, "Inicio", btnInicio);

            Loaded += async (_, _) => await EjecutarAsync("Cargando tus datos…", RecargarDatosAsync, "Datos cargados.");
        }

        private void PrepararListasDesplegables()
        {
            cmbFiltroEstado.ItemsSource = new[] { "Todas", "Pendiente", "En curso", "Completada", "Diarias", "Únicas" };
            cmbFiltroEstado.SelectedIndex = 0;

            cmbCategoriaMision.ItemsSource = Mision.Categorias;
            cmbTipoMision.ItemsSource = Mision.Tipos;
            cmbEstadoMision.ItemsSource = Mision.Estados;

            cmbMisionActividad.DisplayMemberPath = nameof(Mision.Titulo);

            cmbFiltroLogros.ItemsSource = new[] { "Todos", "Desbloqueados", "Bloqueados" };
            cmbFiltroLogros.SelectedIndex = 0;

            LimpiarFormularioMision();
            LimpiarFormularioActividad();
        }

        // ------------------------------------------------------------------
        // Carga de datos
        // ------------------------------------------------------------------

        /// <summary>Lee de Firebase todo lo que muestra la ventana y refresca las vistas.</summary>
        private async Task RecargarDatosAsync()
        {
            var tareaProgreso = sesion.Datos.ObtenerProgresoAsync();
            var tareaCatalogo = sesion.Datos.ObtenerCatalogoLogrosAsync();
            var tareaMisiones = sesion.Datos.ObtenerMisionesAsync();
            var tareaActividades = sesion.Datos.ObtenerActividadesAsync();
            await Task.WhenAll(tareaProgreso, tareaCatalogo, tareaMisiones, tareaActividades);

            progreso = tareaProgreso.Result;
            catalogoLogros = tareaCatalogo.Result;
            misiones = tareaMisiones.Result;
            actividades = tareaActividades.Result;

            // Datos derivados que solo sirven para mostrar
            foreach (var mision in misiones)
                mision.CompletadaAhora = ReglasProgreso.EstaCompletada(progreso, mision, Hoy);

            var titulos = misiones.ToDictionary(m => m.Id, m => m.Titulo);
            foreach (var actividad in actividades)
                actividad.Mision = titulos.TryGetValue(actividad.MisionId, out var t) ? t : "(misión eliminada)";

            foreach (var logro in catalogoLogros)
            {
                logro.Desbloqueado = progreso.Logros.TryGetValue(logro.Id, out var fecha);
                logro.FechaDesbloqueo = fecha;
            }

            RefrescarVistas();
        }

        private void RefrescarVistas()
        {
            MostrarDatosUsuario();
            MostrarInicio();
            MostrarMisiones();
            MostrarActividades();
            MostrarLogros();
        }

        /// <summary>
        /// Ejecuta una operación contra Firebase: bloquea los botones, muestra "cargando",
        /// y al terminar muestra el resultado o el error. Si la sesión caducó, vuelve al login.
        /// </summary>
        private async Task<bool> EjecutarAsync(string mensajeCarga, Func<Task> operacion, string? mensajeCorrecto = null)
        {
            if (ocupado)
                return false;

            PonerOcupado(true);
            MostrarEstado(mensajeCarga, TipoEstado.Cargando);

            try
            {
                await operacion();
                if (mensajeCorrecto != null)
                    MostrarEstado(mensajeCorrecto, TipoEstado.Correcto);
                return true;
            }
            catch (FirebaseException ex) when (ex.Tipo == TipoErrorFirebase.SesionCaducada)
            {
                VolverAlLogin(ex.Message);
                return false;
            }
            catch (FirebaseException ex)
            {
                string titulo = ex.Tipo == TipoErrorFirebase.Conexion ? "Error de conexión" : "No se pudo completar";
                MostrarEstado($"{titulo}: {ex.Message}", TipoEstado.Error);
                MessageBox.Show(this, ex.Message, titulo, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            catch (Exception ex)
            {
                MostrarEstado("Error inesperado: " + ex.Message, TipoEstado.Error);
                MessageBox.Show(this, ex.Message, "Error inesperado", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
                PonerOcupado(false);
            }
        }

        private void PonerOcupado(bool valor)
        {
            ocupado = valor;
            Cursor = valor ? Cursors.Wait : null;

            var botonesQueEnvian = new[]
            {
                btnGuardarMision, btnModificarMision, btnEliminarMision, btnCompletarMision, btnActualizarMisiones,
                btnGuardarActividad, btnModificarActividad, btnEliminarActividad, btnActualizarActividades,
                btnActualizarLogros
            };
            foreach (var boton in botonesQueEnvian)
                boton.IsEnabled = !valor;

            if (!valor)
            {
                ActualizarBotonesMision();
                ActualizarBotonesActividad();
            }
        }

        private void MostrarEstado(string mensaje, TipoEstado tipo)
        {
            (string icono, string color) = tipo switch
            {
                TipoEstado.Cargando => ("⏳", "#918797"),
                TipoEstado.Correcto => ("✔", "#3E8E5A"),
                TipoEstado.Error => ("✖", "#C34E79"),
                _ => ("ℹ", "#7954A1")
            };
            txtEstado.Text = $"{icono}  {mensaje}   ·   {DateTime.Now:HH:mm:ss}";
            txtEstado.Foreground = (Brush)new BrushConverter().ConvertFromString(color)!;
        }

        /// <summary>Muestra los errores de validación juntos y en la barra de estado.</summary>
        private bool MostrarErroresValidacion(List<string> errores)
        {
            if (errores.Count == 0)
                return false;

            MostrarEstado("Revisa los datos del formulario.", TipoEstado.Error);
            MessageBox.Show(this, "• " + string.Join("\n• ", errores), "Datos no válidos",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return true;
        }

        private void VolverAlLogin(string mensaje)
        {
            sesion.Cerrar();
            var login = new LoginWindow(mensaje);
            Application.Current.MainWindow = login;
            login.Show();
            Close();
        }

        // ------------------------------------------------------------------
        // Navegación
        // ------------------------------------------------------------------

        private void MostrarVista(Grid vista, string titulo, Button botonMenu)
        {
            foreach (var v in new[] { vistaInicio, vistaMisiones, vistaLogros, vistaActividad })
                v.Visibility = v == vista ? Visibility.Visible : Visibility.Collapsed;

            foreach (var b in new[] { btnInicio, btnMisiones, btnLogros, btnActividad })
            {
                b.ClearValue(BackgroundProperty);
                b.ClearValue(ForegroundProperty);
                b.FontWeight = FontWeights.Normal;
            }
            botonMenu.Background = new SolidColorBrush(Color.FromRgb(0xE5, 0xD9, 0xEF));
            botonMenu.Foreground = (Brush)FindResource("MoradoOscuro");
            botonMenu.FontWeight = FontWeights.SemiBold;

            txtTituloPagina.Text = titulo;
        }

        private void btnInicio_Click(object sender, RoutedEventArgs e) => MostrarVista(vistaInicio, "Inicio", btnInicio);

        private void btnMisiones_Click(object sender, RoutedEventArgs e) => MostrarVista(vistaMisiones, "Misiones", btnMisiones);

        private void btnLogros_Click(object sender, RoutedEventArgs e) => MostrarVista(vistaLogros, "Logros", btnLogros);

        private void btnActividad_Click(object sender, RoutedEventArgs e) => MostrarVista(vistaActividad, "Actividad", btnActividad);

        private async void btnActualizar_Click(object sender, RoutedEventArgs e)
        {
            string? misionId = (listaMisiones.SelectedItem as Mision)?.Id;
            string? actividadId = (listaActividades.SelectedItem as Actividad)?.Id;

            if (await EjecutarAsync("Actualizando datos…", RecargarDatosAsync, "Datos actualizados."))
            {
                SeleccionarMision(misionId);
                SeleccionarActividad(actividadId);
            }
        }

        /// <summary>Abre la ventana de perfil.</summary>
        private void btnPerfil_Click(object sender, RoutedEventArgs e)
        {
            var perfil = new PerfilWindow { Owner = this };
            perfil.ShowDialog();
        }

        private void btnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(this, "¿Quieres cerrar la sesión?", "Cerrar sesión",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                VolverAlLogin("Has cerrado la sesión.");
        }

        // ------------------------------------------------------------------
        // Menú lateral, barra superior e Inicio
        // ------------------------------------------------------------------

        private void MostrarDatosUsuario()
        {
            txtNombreMenu.Text = sesion.Perfil.Nombre;
            txtEmailMenu.Text = sesion.Perfil.Email;
            txtNivelSuperior.Text = $"Nivel {progreso.Nivel}  ·  {progreso.Puntos} puntos";
        }

        private void MostrarInicio()
        {
            txtNombreInicio.Text = $"¡Hola, {sesion.Perfil.Nombre}!";
            txtEmailInicio.Text = sesion.Perfil.Email;
            txtNivel.Text = progreso.Nivel.ToString();

            int xpNivel = ReglasProgreso.XpDentroDelNivel(progreso.Experiencia);
            barraExperiencia.Maximum = ReglasProgreso.XpPorNivel;
            barraExperiencia.Value = xpNivel;
            txtExperiencia.Text = $"{xpNivel} / {ReglasProgreso.XpPorNivel} XP para el nivel {progreso.Nivel + 1}";

            txtTotalXP.Text = progreso.Experiencia.ToString();
            txtTotalMisiones.Text = ReglasProgreso.TotalCompletadas(progreso).ToString();
            txtTotalLogros.Text = $"{progreso.Logros.Count} / {catalogoLogros.Count}";

            var retosHoy = misiones.Where(m => m.Tipo == Mision.TipoDiaria)
                                   .OrderBy(m => m.CompletadaAhora)
                                   .ThenBy(m => m.Titulo)
                                   .ToList();
            listaRetosHoy.ItemsSource = retosHoy;
            txtRetosHoyVacio.Visibility = retosHoy.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            var proximas = misiones.Where(m => m.Tipo == Mision.TipoUnica && !m.CompletadaAhora && m.FechaLimite.HasValue)
                                   .OrderBy(m => m.FechaLimite)
                                   .Take(5)
                                   .ToList();
            listaProximas.ItemsSource = proximas;
            txtProximasVacio.Visibility = proximas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
