using System.Net;
using System.Net.Http;
using LevelUp.Services;
using LevelUp.Tests.Fakes;

namespace LevelUp.Tests
{
    /// <summary>
    /// Inicio de sesión y renovación del ID token con Firebase Authentication
    /// (las respuestas de Firebase están simuladas).
    /// </summary>
    public class AuthServiceTests
    {
        private const string RespuestaLoginCorrecto = """
            {
              "idToken": "id-token-1",
              "refreshToken": "refresh-1",
              "expiresIn": "3600",
              "localId": "uid-prueba-1",
              "email": "prueba1@levelup.com"
            }
            """;

        private const string RespuestaRenovacion = """
            {
              "id_token": "id-token-2",
              "refresh_token": "refresh-2",
              "expires_in": "3600"
            }
            """;

        private static (AuthService Auth, FakeHttpHandler Servidor) Crear()
        {
            var servidor = new FakeHttpHandler();
            return (new AuthService(new HttpClient(servidor), "api-key-prueba"), servidor);
        }

        [Fact]
        public async Task IniciarSesion_CredencialesCorrectas_GuardaUidCorreoYToken()
        {
            var (auth, servidor) = Crear();
            servidor.Responder = _ => FakeHttpHandler.Json(HttpStatusCode.OK, RespuestaLoginCorrecto);

            await auth.IniciarSesionAsync("prueba1@levelup.com", "secreta");

            Assert.True(auth.SesionIniciada);
            Assert.Equal("uid-prueba-1", auth.Uid);
            Assert.Equal("prueba1@levelup.com", auth.Email);
            Assert.Equal("id-token-1", await auth.ObtenerTokenAsync());

            var peticion = Assert.Single(servidor.Peticiones);
            Assert.Equal(HttpMethod.Post, peticion.Metodo);
            Assert.Contains("accounts:signInWithPassword?key=api-key-prueba", peticion.Url);
        }

        [Fact]
        public async Task IniciarSesion_ContrasenaIncorrecta_LanzaErrorDeCredenciales()
        {
            var (auth, servidor) = Crear();
            servidor.Responder = _ => FakeHttpHandler.Json(HttpStatusCode.BadRequest,
                """{ "error": { "message": "INVALID_LOGIN_CREDENTIALS" } }""");

            var error = await Assert.ThrowsAsync<FirebaseException>(() =>
                auth.IniciarSesionAsync("prueba1@levelup.com", "mala"));

            Assert.Equal(TipoErrorFirebase.Credenciales, error.Tipo);
            Assert.Equal("Correo o contraseña incorrectos.", error.Message);
            Assert.False(auth.SesionIniciada);
        }

        [Fact]
        public async Task IniciarSesion_SinConexion_LanzaErrorDeConexion()
        {
            var (auth, servidor) = Crear();
            servidor.ErrorDeConexion = new HttpRequestException("Sin red");

            var error = await Assert.ThrowsAsync<FirebaseException>(() =>
                auth.IniciarSesionAsync("prueba1@levelup.com", "secreta"));

            Assert.Equal(TipoErrorFirebase.Conexion, error.Tipo);
        }

        [Fact]
        public async Task ObtenerToken_ForzandoRenovacion_PideUnTokenNuevoConElRefreshToken()
        {
            var (auth, servidor) = Crear();
            servidor.Responder = p => FakeHttpHandler.Json(HttpStatusCode.OK,
                p.Url.Contains("securetoken") ? RespuestaRenovacion : RespuestaLoginCorrecto);
            await auth.IniciarSesionAsync("prueba1@levelup.com", "secreta");

            string token = await auth.ObtenerTokenAsync(forzarRenovacion: true);

            Assert.Equal("id-token-2", token);
            var renovacion = servidor.Peticiones.Last();
            Assert.Contains("securetoken.googleapis.com", renovacion.Url);
            Assert.Contains("grant_type=refresh_token", renovacion.Cuerpo);
            Assert.Contains("refresh_token=refresh-1", renovacion.Cuerpo);
        }

        [Fact]
        public async Task ObtenerToken_SiNoSePuedeRenovar_CierraLaSesionYPideVolverAEntrar()
        {
            var (auth, servidor) = Crear();
            servidor.Responder = p => p.Url.Contains("securetoken")
                ? FakeHttpHandler.Json(HttpStatusCode.BadRequest, """{ "error": { "message": "TOKEN_EXPIRED" } }""")
                : FakeHttpHandler.Json(HttpStatusCode.OK, RespuestaLoginCorrecto);
            await auth.IniciarSesionAsync("prueba1@levelup.com", "secreta");

            var error = await Assert.ThrowsAsync<FirebaseException>(() =>
                auth.ObtenerTokenAsync(forzarRenovacion: true));

            Assert.Equal(TipoErrorFirebase.SesionCaducada, error.Tipo);
            Assert.False(auth.SesionIniciada);
            Assert.Equal("", auth.Uid);
        }

        [Fact]
        public async Task ObtenerToken_SinSesionIniciada_LanzaSesionCaducada()
        {
            var (auth, _) = Crear();

            var error = await Assert.ThrowsAsync<FirebaseException>(() => auth.ObtenerTokenAsync());

            Assert.Equal(TipoErrorFirebase.SesionCaducada, error.Tipo);
        }

        [Fact]
        public async Task CerrarSesion_BorraLosDatosDeLaSesion()
        {
            var (auth, servidor) = Crear();
            servidor.Responder = _ => FakeHttpHandler.Json(HttpStatusCode.OK, RespuestaLoginCorrecto);
            await auth.IniciarSesionAsync("prueba1@levelup.com", "secreta");

            auth.CerrarSesion();

            Assert.False(auth.SesionIniciada);
            Assert.Equal("", auth.Uid);
            Assert.Equal("", auth.Email);
        }

        [Theory]
        [InlineData("""{ "error": { "message": "INVALID_LOGIN_CREDENTIALS" } }""", "Correo o contraseña incorrectos.")]
        [InlineData("""{ "error": { "message": "EMAIL_NOT_FOUND" } }""", "Correo o contraseña incorrectos.")]
        [InlineData("""{ "error": { "message": "USER_DISABLED" } }""", "Esta cuenta está deshabilitada.")]
        [InlineData("""{ "error": { "message": "TOO_MANY_ATTEMPTS_TRY_LATER : Access disabled" } }""", "Demasiados intentos fallidos. Espera unos minutos.")]
        [InlineData("esto no es JSON", "No se pudo iniciar sesión.")]
        public void TraducirErrorLogin_DevuelveUnMensajeComprensible(string respuesta, string mensajeEsperado)
        {
            Assert.Equal(mensajeEsperado, AuthService.TraducirErrorLogin(respuesta));
        }
    }
}
