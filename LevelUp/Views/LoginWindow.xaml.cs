using System.Windows;
using LevelUp.Services;

namespace LevelUp.Views
{
    /// <summary>
    /// Ventana de acceso: inicia sesión con Firebase Authentication y abre la ventana principal.
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        /// <summary>Se usa al caducar la sesión para explicar por qué se vuelve aquí.</summary>
        public LoginWindow(string mensaje) : this()
        {
            txtError.Text = mensaje;
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            txtError.Text = "";

            var errores = Validaciones.ValidarLogin(txtCorreo.Text, txtPassword.Password);
            if (errores.Count > 0)
            {
                txtError.Text = string.Join("\n", errores);
                return;
            }

            btnLogin.IsEnabled = false;
            btnLogin.Content = "Conectando…";

            try
            {
                var sesion = await Sesion.IniciarAsync(txtCorreo.Text, txtPassword.Password);

                var principal = new MainWindow(sesion);
                Application.Current.MainWindow = principal;
                principal.Show();
                Close();
            }
            catch (FirebaseException ex)
            {
                txtError.Text = ex.Message;
            }
            catch (Exception ex)
            {
                txtError.Text = "Error inesperado: " + ex.Message;
            }
            finally
            {
                // La contraseña no se guarda en ningún sitio.
                txtPassword.Clear();
                btnLogin.IsEnabled = true;
                btnLogin.Content = "Iniciar sesión";
            }
        }
    }
}
