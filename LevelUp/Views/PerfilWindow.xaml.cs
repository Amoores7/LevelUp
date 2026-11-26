using System.Windows;
using System.Windows.Input;
using LevelUp.Models;
using LevelUp.Services;

namespace LevelUp.Views
{
    /// <summary>
    /// Ventana de perfil: muestra los datos de la cuenta y el progreso del jugador
    /// y permite cambiar el nombre. El correo es el de Firebase Authentication y no se modifica.
    /// </summary>
    public partial class PerfilWindow : Window
    {
        private readonly Sesion sesion;
        private readonly Progreso progreso;

        /// <summary>true si se ha guardado un nombre nuevo (la ventana principal lo vuelve a mostrar).</summary>
        public bool NombreCambiado { get; private set; }

        /// <summary>Si la sesión caducó mientras la ventana estaba abierta, el mensaje para el login.</summary>
        public string? MensajeSesionCaducada { get; private set; }

        public PerfilWindow(Sesion sesion, Progreso progreso)
        {
            InitializeComponent();
            this.sesion = sesion;
            this.progreso = progreso;
            MostrarPerfil();
        }

        private void MostrarPerfil()
        {
            // Resumen (columna izquierda)
            txtNombrePerfilVista.Text = sesion.Perfil.Nombre;
            txtEmailPerfil.Text = sesion.Perfil.Email;
            txtNivelPerfil.Text = progreso.Nivel.ToString();

            // Datos personales y progreso (columna derecha)
            txtNombrePerfil.Text = sesion.Perfil.Nombre;
            txtCorreoPerfil.Text = sesion.Perfil.Email; // solo lectura: es el correo de la cuenta
            txtExperienciaPerfil.Text = $"{progreso.Experiencia} XP";
            txtPuntosPerfil.Text = progreso.Puntos.ToString();
        }

        private async void btnGuardarPerfil_Click(object sender, RoutedEventArgs e)
        {
            string nombre = txtNombrePerfil.Text.Trim();

            var errores = Validaciones.ValidarNombre(nombre);
            if (errores.Count > 0)
            {
                MessageBox.Show(this, "• " + string.Join("\n• ", errores), "Datos no válidos",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (nombre == sesion.Perfil.Nombre)
            {
                MessageBox.Show(this, "No hay cambios que guardar.", "Perfil",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Mientras se guarda no se puede volver a pulsar.
            btnGuardarPerfil.IsEnabled = false;
            btnGuardarPerfil.Content = "Guardando…";
            Cursor = Cursors.Wait;

            try
            {
                await sesion.Datos.ModificarNombreAsync(nombre);           // PATCH /usuarios/{uid}/perfil
                sesion.Perfil = await sesion.Datos.ObtenerPerfilAsync();   // se vuelve a leer lo guardado
                NombreCambiado = true;
                MostrarPerfil();
                MessageBox.Show(this, "Perfil guardado.", "Perfil",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (FirebaseException ex) when (ex.Tipo == TipoErrorFirebase.SesionCaducada)
            {
                MensajeSesionCaducada = ex.Message;
                Close();
            }
            catch (FirebaseException ex)
            {
                string titulo = ex.Tipo == TipoErrorFirebase.Conexion ? "Error de conexión" : "No se pudo guardar";
                MessageBox.Show(this, ex.Message, titulo, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                btnGuardarPerfil.IsEnabled = true;
                btnGuardarPerfil.Content = "Guardar cambios";
                Cursor = null;
            }
        }
    }
}
