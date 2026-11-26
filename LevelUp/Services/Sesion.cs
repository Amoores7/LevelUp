using System.Net.Http;
using LevelUp.Models;

namespace LevelUp.Services
{
    /// <summary>
    /// Agrupa todo lo que necesita la ventana principal tras iniciar sesión:
    /// autenticación (token), acceso a los datos del usuario y su perfil.
    /// </summary>
    public class Sesion
    {
        /// <summary>Un único HttpClient para toda la aplicación.</summary>
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

        public AuthService Auth { get; }
        public DatosService Datos { get; }
        public Usuario Perfil { get; set; }

        private Sesion(AuthService auth, DatosService datos, Usuario perfil)
        {
            Auth = auth;
            Datos = datos;
            Perfil = perfil;
        }

        public static async Task<Sesion> IniciarAsync(string email, string password)
        {
            var config = FirebaseConfig.Cargar();

            var auth = new AuthService(Http, config.ApiKey);
            await auth.IniciarSesionAsync(email.Trim(), password);

            var firebase = new FirebaseService(Http, config.DatabaseUrl, auth);
            var datos = new DatosService(firebase, auth.Uid);
            var perfil = await datos.PrepararCuentaAsync(auth.Email);

            return new Sesion(auth, datos, perfil);
        }

        public void Cerrar() => Auth.CerrarSesion();
    }
}
