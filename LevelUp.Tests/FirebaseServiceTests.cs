using System.Net;
using System.Net.Http;
using LevelUp.Models;
using LevelUp.Services;
using LevelUp.Tests.Fakes;

namespace LevelUp.Tests
{
    /// <summary>
    /// Acceso a la API REST de Realtime Database: métodos HTTP, rutas .json con el token,
    /// escritura condicional, renovación del token y traducción de errores.
    /// </summary>
    public class FirebaseServiceTests
    {
        private const string UrlBase = "https://levelup-test.firebasedatabase.app";

        private readonly FakeHttpHandler servidor = new();
        private readonly FakeProveedorToken tokens = new();
        private readonly FirebaseService firebase;

        public FirebaseServiceTests()
        {
            firebase = new FirebaseService(new HttpClient(servidor), UrlBase + "/", tokens);
        }

        [Fact]
        public void ConstruirUrl_TerminaEnJsonYLlevaElToken()
        {
            string url = firebase.ConstruirUrl("/usuarios/u1/misiones/", "abc+123");

            Assert.Equal(UrlBase + "/usuarios/u1/misiones.json?auth=abc%2B123", url);
        }

        [Fact]
        public async Task Get_NodoQueNoExiste_DevuelveNull()
        {
            servidor.Responder = _ => FakeHttpHandler.Json(HttpStatusCode.OK, "null");

            var mision = await firebase.GetAsync<Mision>("usuarios/u1/misiones/no-existe");

            Assert.Null(mision);
        }

        [Fact]
        public async Task GetColeccion_LeeLasClavesYLosDatosEnCamelCase()
        {
            servidor.Responder = _ => FakeHttpHandler.Json(HttpStatusCode.OK, """
                {
                  "-M1": { "titulo": "Leer 20 minutos", "experiencia": 20, "tipo": "Diaria", "fechaLimite": "2026-11-30" },
                  "-M2": { "titulo": "Correr 5 km", "experiencia": 120, "tipo": "Única" }
                }
                """);

            var misiones = await firebase.GetColeccionAsync<Mision>("usuarios/u1/misiones");

            Assert.Equal(2, misiones.Count);
            Assert.Equal("Leer 20 minutos", misiones["-M1"].Titulo);
            Assert.Equal(new DateOnly(2026, 11, 30), misiones["-M1"].FechaLimite);
            Assert.Null(misiones["-M2"].FechaLimite);
            Assert.Equal(HttpMethod.Get, servidor.Peticiones[0].Metodo);
        }

        [Fact]
        public async Task GetColeccion_Vacia_DevuelveDiccionarioVacio()
        {
            var actividades = await firebase.GetColeccionAsync<Actividad>("usuarios/u1/actividades");

            Assert.Empty(actividades);
        }

        [Fact]
        public async Task Post_DevuelveLaClaveGeneradaPorFirebase()
        {
            servidor.Responder = _ => FakeHttpHandler.Json(HttpStatusCode.OK, """{ "name": "-NuevaClave" }""");
            var mision = new Mision { Titulo = "Meditar 10 minutos", Categoria = "Bienestar", Experiencia = 10 };

            string clave = await firebase.PostAsync("usuarios/u1/misiones", mision);

            Assert.Equal("-NuevaClave", clave);
            var peticion = Assert.Single(servidor.Peticiones);
            Assert.Equal(HttpMethod.Post, peticion.Metodo);
            Assert.Equal(UrlBase + "/usuarios/u1/misiones.json?auth=token-1", peticion.Url);
            Assert.Contains("\"titulo\":\"Meditar 10 minutos\"", peticion.Cuerpo);
            Assert.DoesNotContain("\"id\"", peticion.Cuerpo); // la clave no se guarda dentro del nodo
            Assert.DoesNotContain("fechaLimite", peticion.Cuerpo); // los nulos no se envían
        }

        [Fact]
        public async Task Patch_EnviaSoloLosCamposIndicados()
        {
            await firebase.PatchAsync("usuarios/u1/perfil", new { nombre = "Carlos" });

            var peticion = Assert.Single(servidor.Peticiones);
            Assert.Equal(HttpMethod.Patch, peticion.Metodo);
            Assert.Equal("""{"nombre":"Carlos"}""", peticion.Cuerpo);
        }

        [Fact]
        public async Task Delete_UsaElMetodoDelete()
        {
            await firebase.DeleteAsync("usuarios/u1/actividades/-A1");

            var peticion = Assert.Single(servidor.Peticiones);
            Assert.Equal(HttpMethod.Delete, peticion.Metodo);
            Assert.StartsWith(UrlBase + "/usuarios/u1/actividades/-A1.json", peticion.Url);
        }

        [Fact]
        public async Task GetConETag_PideYDevuelveElETag()
        {
            servidor.Responder = _ => FakeHttpHandler.Json(HttpStatusCode.OK,
                """{ "experiencia": 230, "nivel": 3, "puntos": 335 }""", etag: "etag-abc");

            var (progreso, etag) = await firebase.GetConETagAsync<Progreso>("usuarios/u1/progreso");

            Assert.Equal("etag-abc", etag);
            Assert.Equal(230, progreso!.Experiencia);
            Assert.Equal("true", servidor.Peticiones[0].PideETag);
        }

        [Fact]
        public async Task PutCondicional_SinCambiosDeOtroCliente_EscribeYDevuelveTrue()
        {
            bool escrito = await firebase.PutCondicionalAsync("usuarios/u1/progreso", new Progreso(), "etag-abc");

            Assert.True(escrito);
            var peticion = Assert.Single(servidor.Peticiones);
            Assert.Equal(HttpMethod.Put, peticion.Metodo);
            Assert.Equal("etag-abc", peticion.IfMatch);
        }

        [Fact]
        public async Task PutCondicional_SiOtroClienteCambioElNodo_DevuelveFalse()
        {
            servidor.Responder = _ => FakeHttpHandler.Json(HttpStatusCode.PreconditionFailed, "{}", etag: "etag-nuevo");

            bool escrito = await firebase.PutCondicionalAsync("usuarios/u1/progreso", new Progreso(), "etag-viejo");

            Assert.False(escrito);
        }

        [Fact]
        public async Task TokenCaducado_SeRenuevaYSeRepiteLaPeticionUnaVez()
        {
            servidor.Responder = p => p.Url.Contains("auth=" + FakeProveedorToken.TokenInicial)
                ? FakeHttpHandler.Json(HttpStatusCode.Unauthorized, """{ "error": "Auth token is expired" }""")
                : FakeHttpHandler.Json(HttpStatusCode.OK, """{ "nombre": "Carlos", "email": "prueba1@levelup.com" }""");

            var perfil = await firebase.GetAsync<Usuario>("usuarios/u1/perfil");

            Assert.Equal("Carlos", perfil!.Nombre);
            Assert.Equal(1, tokens.Renovaciones);
            Assert.Equal(2, servidor.Peticiones.Count);
            Assert.Contains("auth=" + FakeProveedorToken.TokenRenovado, servidor.Peticiones[1].Url);
        }

        [Fact]
        public async Task AccesoDenegadoPorLasReglas_LanzaAccesoDenegado()
        {
            // Lo que responde Firebase al intentar leer los datos de otro usuario.
            servidor.Responder = _ => FakeHttpHandler.Json(HttpStatusCode.Unauthorized, """{ "error": "Permission denied" }""");

            var error = await Assert.ThrowsAsync<FirebaseException>(() =>
                firebase.GetAsync<Progreso>("usuarios/otro-uid/progreso"));

            Assert.Equal(TipoErrorFirebase.AccesoDenegado, error.Tipo);
            Assert.Equal(0, tokens.Renovaciones); // no es un token caducado: no se reintenta
        }

        [Fact]
        public async Task SinConexion_LanzaErrorDeConexion()
        {
            servidor.ErrorDeConexion = new HttpRequestException("Sin red");

            var error = await Assert.ThrowsAsync<FirebaseException>(() =>
                firebase.GetAsync<Progreso>("usuarios/u1/progreso"));

            Assert.Equal(TipoErrorFirebase.Conexion, error.Tipo);
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized, "Auth token is expired", TipoErrorFirebase.SesionCaducada)]
        [InlineData(HttpStatusCode.Unauthorized, "Permission denied", TipoErrorFirebase.AccesoDenegado)]
        [InlineData(HttpStatusCode.BadRequest, "Permission denied", TipoErrorFirebase.AccesoDenegado)]
        [InlineData(HttpStatusCode.NotFound, "", TipoErrorFirebase.Configuracion)]
        [InlineData(HttpStatusCode.InternalServerError, "", TipoErrorFirebase.Otro)]
        public void TraducirError_ClasificaLaRespuestaDeFirebase(HttpStatusCode codigo, string cuerpo, TipoErrorFirebase esperado)
        {
            Assert.Equal(esperado, FirebaseService.TraducirError(codigo, cuerpo).Tipo);
        }
    }
}
