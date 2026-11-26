using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using LevelUp.Models;
using LevelUp.Services;
using LevelUp.Tests.Fakes;

namespace LevelUp.Tests
{
    /// <summary>
    /// Operaciones que modifican varios nodos o dependen del estado anterior:
    /// borrado multipath y completar misión con escritura condicional.
    /// </summary>
    public class DatosServiceTests
    {
        private const string Uid = "u1";
        private static readonly DateOnly Hoy = new(2026, 11, 24);

        private readonly FakeHttpHandler servidor = new();
        private readonly DatosService datos;

        public DatosServiceTests()
        {
            var firebase = new FirebaseService(new HttpClient(servidor), "https://levelup-test.firebasedatabase.app",
                new FakeProveedorToken());
            datos = new DatosService(firebase, Uid);
        }

        private static bool EsRuta(PeticionRegistrada p, string ruta) => p.Url.Contains($"/{ruta}.json?");

        // ------------------------------------------------------------------
        // Rutas
        // ------------------------------------------------------------------

        [Fact]
        public async Task ObtenerMisiones_LeeSoloLasDelUsuarioYGuardaLaClaveComoId()
        {
            servidor.Responder = _ => FakeHttpHandler.Json(HttpStatusCode.OK, """
                { "-M1": { "titulo": "Leer 20 minutos", "experiencia": 20 } }
                """);

            var misiones = await datos.ObtenerMisionesAsync();

            Assert.True(EsRuta(servidor.Peticiones[0], "usuarios/u1/misiones"));
            Assert.Equal("-M1", Assert.Single(misiones).Id);
        }

        [Fact]
        public async Task CrearActividad_HacePostYGuardaLaClaveDevuelta()
        {
            servidor.Responder = _ => FakeHttpHandler.Json(HttpStatusCode.OK, """{ "name": "-A9" }""");
            var actividad = new Actividad { MisionId = "-M1", Descripcion = "Capítulo 5", Fecha = Hoy, Minutos = 30 };

            await datos.CrearActividadAsync(actividad);

            Assert.Equal("-A9", actividad.Id);
            var peticion = Assert.Single(servidor.Peticiones);
            Assert.Equal(HttpMethod.Post, peticion.Metodo);
            Assert.True(EsRuta(peticion, "usuarios/u1/actividades"));
            Assert.Contains("\"fecha\":\"2026-11-24\"", peticion.Cuerpo);
            Assert.Contains("\"misionId\":\"-M1\"", peticion.Cuerpo);
        }

        // ------------------------------------------------------------------
        // Eliminar misión (actualización multipath)
        // ------------------------------------------------------------------

        [Fact]
        public async Task EliminarMision_BorraLaMisionYSusActividadesEnUnaSolaPeticion()
        {
            servidor.Responder = p => p.Metodo == HttpMethod.Get
                ? FakeHttpHandler.Json(HttpStatusCode.OK, """
                    {
                      "-A1": { "misionId": "-M1", "descripcion": "Capítulo 1", "fecha": "2026-11-20", "minutos": 25 },
                      "-A2": { "misionId": "-M1", "descripcion": "Capítulo 2", "fecha": "2026-11-21", "minutos": 20 },
                      "-A3": { "misionId": "-M2", "descripcion": "Otra misión", "fecha": "2026-11-21", "minutos": 60 }
                    }
                    """)
                : FakeHttpHandler.Json(HttpStatusCode.OK, "{}");

            int borradas = await datos.EliminarMisionAsync("-M1");

            Assert.Equal(2, borradas);

            var patch = Assert.Single(servidor.Peticiones, p => p.Metodo == HttpMethod.Patch);
            Assert.True(EsRuta(patch, "usuarios/u1"));
            Assert.DoesNotContain(servidor.Peticiones, p => p.Metodo == HttpMethod.Delete);

            var cambios = JsonNode.Parse(patch.Cuerpo!)!.AsObject();
            Assert.Equal(3, cambios.Count);
            Assert.True(cambios.ContainsKey("misiones/-M1") && cambios["misiones/-M1"] == null);
            Assert.True(cambios.ContainsKey("actividades/-A1") && cambios["actividades/-A1"] == null);
            Assert.True(cambios.ContainsKey("actividades/-A2") && cambios["actividades/-A2"] == null);
            Assert.False(cambios.ContainsKey("actividades/-A3")); // es de otra misión
        }

        // ------------------------------------------------------------------
        // Completar misión (escritura condicional)
        // ------------------------------------------------------------------

        private const string MisionDiaria = """
            { "titulo": "Leer 20 minutos", "experiencia": 20, "tipo": "Diaria", "estado": "En curso", "categoria": "Estudio" }
            """;

        private const string MisionUnica = """
            { "titulo": "Ordenar el escritorio", "experiencia": 40, "tipo": "Única", "estado": "Pendiente", "categoria": "Hogar" }
            """;

        private static string ProgresoJson(Progreso p) => JsonSerializer.Serialize(p, FirebaseService.OpcionesJson);

        [Fact]
        public async Task CompletarMision_GuardaElProgresoConElETagLeido()
        {
            servidor.Responder = p => p.Metodo == HttpMethod.Get && EsRuta(p, "usuarios/u1/misiones/-M1")
                ? FakeHttpHandler.Json(HttpStatusCode.OK, MisionDiaria)
                : p.Metodo == HttpMethod.Get
                    ? FakeHttpHandler.Json(HttpStatusCode.OK, ProgresoJson(new Progreso()), etag: "etag-1")
                    : FakeHttpHandler.Json(HttpStatusCode.OK, "{}");

            var resultado = await datos.CompletarMisionAsync(new Mision { Id = "-M1" }, new List<Logro>(), Hoy);

            Assert.True(resultado.Completada);
            Assert.Equal(20, resultado.Progreso.Experiencia);

            var put = Assert.Single(servidor.Peticiones, p => p.Metodo == HttpMethod.Put);
            Assert.True(EsRuta(put, "usuarios/u1/progreso"));
            Assert.Equal("etag-1", put.IfMatch);
        }

        [Fact]
        public async Task CompletarMision_UsaLaExperienciaGuardadaEnFirebaseYNoLaDelFormulario()
        {
            servidor.Responder = p => p.Metodo == HttpMethod.Get && EsRuta(p, "usuarios/u1/misiones/-M1")
                ? FakeHttpHandler.Json(HttpStatusCode.OK, MisionDiaria)
                : p.Metodo == HttpMethod.Get
                    ? FakeHttpHandler.Json(HttpStatusCode.OK, ProgresoJson(new Progreso()), etag: "etag-1")
                    : FakeHttpHandler.Json(HttpStatusCode.OK, "{}");

            var trucada = new Mision { Id = "-M1", Experiencia = 500, Tipo = Mision.TipoDiaria };
            var resultado = await datos.CompletarMisionAsync(trucada, new List<Logro>(), Hoy);

            Assert.Equal(20, resultado.ExperienciaGanada);
        }

        [Fact]
        public async Task CompletarMision_SiOtroClienteCambiaElProgreso_VuelveALeerYNoPierdeDatos()
        {
            // Mientras tanto, otra sesión completó otra misión y dejó 50 XP.
            var progresoDelOtroCliente = new Progreso { Experiencia = 50, Nivel = 1, Puntos = 50 };
            int lecturasProgreso = 0;
            int escrituras = 0;

            servidor.Responder = p =>
            {
                if (p.Metodo == HttpMethod.Get && EsRuta(p, "usuarios/u1/misiones/-M1"))
                    return FakeHttpHandler.Json(HttpStatusCode.OK, MisionDiaria);

                if (p.Metodo == HttpMethod.Get)
                {
                    lecturasProgreso++;
                    return lecturasProgreso == 1
                        ? FakeHttpHandler.Json(HttpStatusCode.OK, ProgresoJson(new Progreso()), etag: "etag-1")
                        : FakeHttpHandler.Json(HttpStatusCode.OK, ProgresoJson(progresoDelOtroCliente), etag: "etag-2");
                }

                escrituras++;
                return p.IfMatch == "etag-1"
                    ? FakeHttpHandler.Json(HttpStatusCode.PreconditionFailed, "{}") // ETag antiguo: se rechaza
                    : FakeHttpHandler.Json(HttpStatusCode.OK, "{}");
            };

            var resultado = await datos.CompletarMisionAsync(new Mision { Id = "-M1" }, new List<Logro>(), Hoy);

            Assert.Equal(2, lecturasProgreso);
            Assert.Equal(2, escrituras);
            Assert.Equal(50 + 20, resultado.Progreso.Experiencia); // se suma sobre el valor nuevo
        }

        [Fact]
        public async Task CompletarMision_YaCompletadaHoy_NoEscribeNada()
        {
            var yaHecha = ReglasProgreso.Completar(new Progreso(),
                new Mision { Id = "-M1", Experiencia = 20, Tipo = Mision.TipoDiaria }, new List<Logro>(), Hoy).Progreso;

            servidor.Responder = p => EsRuta(p, "usuarios/u1/misiones/-M1")
                ? FakeHttpHandler.Json(HttpStatusCode.OK, MisionDiaria)
                : FakeHttpHandler.Json(HttpStatusCode.OK, ProgresoJson(yaHecha), etag: "etag-1");

            var resultado = await datos.CompletarMisionAsync(new Mision { Id = "-M1" }, new List<Logro>(), Hoy);

            Assert.False(resultado.Completada);
            Assert.All(servidor.Peticiones, p => Assert.Equal(HttpMethod.Get, p.Metodo));
        }

        [Fact]
        public async Task CompletarMision_Unica_MarcaLaMisionComoCompletada()
        {
            servidor.Responder = p => p.Metodo == HttpMethod.Get && EsRuta(p, "usuarios/u1/misiones/-M4")
                ? FakeHttpHandler.Json(HttpStatusCode.OK, MisionUnica)
                : p.Metodo == HttpMethod.Get
                    ? FakeHttpHandler.Json(HttpStatusCode.OK, ProgresoJson(new Progreso()), etag: "etag-1")
                    : FakeHttpHandler.Json(HttpStatusCode.OK, "{}");

            await datos.CompletarMisionAsync(new Mision { Id = "-M4" }, new List<Logro>(), Hoy);

            var patch = Assert.Single(servidor.Peticiones, p => p.Metodo == HttpMethod.Patch);
            Assert.True(EsRuta(patch, "usuarios/u1/misiones/-M4"));
            Assert.Equal("""{"estado":"Completada"}""", patch.Cuerpo);
        }

        [Fact]
        public async Task CompletarMision_QueYaNoExiste_LanzaError()
        {
            servidor.Responder = _ => FakeHttpHandler.Json(HttpStatusCode.OK, "null");

            await Assert.ThrowsAsync<FirebaseException>(() =>
                datos.CompletarMisionAsync(new Mision { Id = "-Borrada" }, new List<Logro>(), Hoy));
        }
    }
}
