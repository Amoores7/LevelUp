using LevelUp.Models;
using LevelUp.Services;

namespace LevelUp.Tests
{
    /// <summary>
    /// Reglas del juego: experiencia, niveles, logros y, sobre todo,
    /// que nunca se conceden puntos dos veces por la misma acción.
    /// </summary>
    public class ReglasProgresoTests
    {
        private static readonly DateOnly Hoy = new(2026, 11, 24);

        private static Mision Diaria(string id = "m1", int xp = 20) =>
            new() { Id = id, Titulo = "Leer 20 minutos", Experiencia = xp, Tipo = Mision.TipoDiaria };

        private static Mision Unica(string id = "m2", int xp = 150) =>
            new() { Id = id, Titulo = "Terminar curso de C#", Experiencia = xp, Tipo = Mision.TipoUnica };

        private static Logro CrearLogro(string id, string tipo, int valor, int puntos) =>
            new() { Id = id, Titulo = id, Puntos = puntos, Condicion = new CondicionLogro { Tipo = tipo, Valor = valor } };

        /// <summary>Parte del catálogo real de /catalogo/logros.</summary>
        private static readonly List<Logro> Catalogo = new()
        {
            CrearLogro("lg01", Logro.CondicionCompletadas, 1, 10),
            CrearLogro("lg05", Logro.CondicionDiarias, 3, 25),
            CrearLogro("lg07", Logro.CondicionUnicas, 1, 30),
            CrearLogro("lg09", Logro.CondicionNivel, 2, 20)
        };

        // ------------------------------------------------------------------
        // Niveles
        // ------------------------------------------------------------------

        [Theory]
        [InlineData(0, 1)]
        [InlineData(99, 1)]
        [InlineData(100, 2)]
        [InlineData(230, 3)]
        [InlineData(-50, 1)]
        public void CalcularNivel_DevuelveElNivelSegunLaExperiencia(int experiencia, int nivelEsperado)
        {
            Assert.Equal(nivelEsperado, ReglasProgreso.CalcularNivel(experiencia));
        }

        [Fact]
        public void XpDentroDelNivel_DevuelveLaExperienciaDelNivelActual()
        {
            Assert.Equal(30, ReglasProgreso.XpDentroDelNivel(230));
        }

        // ------------------------------------------------------------------
        // Completar misiones
        // ------------------------------------------------------------------

        [Fact]
        public void ClaveCompletado_EsLaFechaEnLasDiariasYUnicaEnLasUnicas()
        {
            Assert.Equal("2026-11-24", ReglasProgreso.ClaveCompletado(Diaria(), Hoy));
            Assert.Equal(ReglasProgreso.ClaveUnica, ReglasProgreso.ClaveCompletado(Unica(), Hoy));
        }

        [Fact]
        public void Completar_PrimeraVez_SumaExperienciaYPuntos()
        {
            var resultado = ReglasProgreso.Completar(new Progreso(), Diaria(xp: 20), new List<Logro>(), Hoy);

            Assert.True(resultado.Completada);
            Assert.Equal(20, resultado.ExperienciaGanada);
            Assert.Equal(20, resultado.Progreso.Experiencia);
            Assert.Equal(20, resultado.Progreso.Puntos);
            Assert.True(ReglasProgreso.EstaCompletada(resultado.Progreso, Diaria(), Hoy));
        }

        [Fact]
        public void Completar_LaMismaDiariaDosVecesElMismoDia_NoSumaPuntosOtraVez()
        {
            var mision = Diaria(xp: 20);
            var primera = ReglasProgreso.Completar(new Progreso(), mision, Catalogo, Hoy);

            var segunda = ReglasProgreso.Completar(primera.Progreso, mision, Catalogo, Hoy);

            Assert.False(segunda.Completada);
            Assert.Equal(0, segunda.ExperienciaGanada);
            Assert.Equal(primera.Progreso.Experiencia, segunda.Progreso.Experiencia);
            Assert.Equal(primera.Progreso.Puntos, segunda.Progreso.Puntos);
        }

        [Fact]
        public void Completar_LaMismaDiariaAlDiaSiguiente_VuelveASumar()
        {
            var mision = Diaria(xp: 20);
            var hoy = ReglasProgreso.Completar(new Progreso(), mision, new List<Logro>(), Hoy);

            var manana = ReglasProgreso.Completar(hoy.Progreso, mision, new List<Logro>(), Hoy.AddDays(1));

            Assert.True(manana.Completada);
            Assert.Equal(40, manana.Progreso.Experiencia);
            Assert.Equal(2, ReglasProgreso.TotalDiarias(manana.Progreso));
        }

        [Fact]
        public void Completar_UnaUnicaDosVeces_SoloSumaLaPrimera()
        {
            var mision = Unica(xp: 150);
            var primera = ReglasProgreso.Completar(new Progreso(), mision, new List<Logro>(), Hoy);

            // Aunque se intente otro día, una misión única solo cuenta una vez.
            var segunda = ReglasProgreso.Completar(primera.Progreso, mision, new List<Logro>(), Hoy.AddDays(3));

            Assert.False(segunda.Completada);
            Assert.Equal(150, segunda.Progreso.Experiencia);
            Assert.Equal(1, ReglasProgreso.TotalUnicas(segunda.Progreso));
        }

        [Fact]
        public void Completar_AlSuperar100Xp_SubeDeNivel()
        {
            var progreso = new Progreso { Experiencia = 90, Nivel = 1, Puntos = 90 };

            var resultado = ReglasProgreso.Completar(progreso, Diaria(xp: 20), new List<Logro>(), Hoy);

            Assert.True(resultado.SubioDeNivel);
            Assert.Equal(2, resultado.Progreso.Nivel);
            Assert.Equal(110, resultado.Progreso.Experiencia);
        }

        [Fact]
        public void Completar_SinLlegarA100Xp_NoSubeDeNivel()
        {
            var resultado = ReglasProgreso.Completar(new Progreso(), Diaria(xp: 20), new List<Logro>(), Hoy);

            Assert.False(resultado.SubioDeNivel);
            Assert.Equal(1, resultado.Progreso.Nivel);
        }

        [Fact]
        public void Completar_LaPrimeraMision_DesbloqueaElLogroYSumaSusPuntos()
        {
            var resultado = ReglasProgreso.Completar(new Progreso(), Diaria(xp: 20), Catalogo, Hoy);

            var logro = Assert.Single(resultado.LogrosNuevos);
            Assert.Equal("lg01", logro.Id);
            Assert.Equal("2026-11-24", resultado.Progreso.Logros["lg01"]);
            Assert.Equal(20 + 10, resultado.Progreso.Puntos); // XP de la misión + puntos del logro
            Assert.Equal(20, resultado.Progreso.Experiencia);   // los logros no dan experiencia
        }

        [Fact]
        public void Completar_UnaUnicaQueHaceSubirDeNivel_DesbloqueaVariosLogrosALaVez()
        {
            var resultado = ReglasProgreso.Completar(new Progreso(), Unica(xp: 150), Catalogo, Hoy);

            var ids = resultado.LogrosNuevos.Select(l => l.Id).OrderBy(id => id).ToList();
            Assert.Equal(new[] { "lg01", "lg07", "lg09" }, ids);
            Assert.Equal(150 + 10 + 30 + 20, resultado.Progreso.Puntos);
        }

        [Fact]
        public void Completar_LogroYaDesbloqueado_NoSeConcedeOtraVez()
        {
            var primera = ReglasProgreso.Completar(new Progreso(), Diaria("m1"), Catalogo, Hoy);

            var segunda = ReglasProgreso.Completar(primera.Progreso, Diaria("m3"), Catalogo, Hoy);

            Assert.DoesNotContain(segunda.LogrosNuevos, l => l.Id == "lg01");
            Assert.Equal(primera.Progreso.Puntos + 20, segunda.Progreso.Puntos);
        }

        [Fact]
        public void Completar_TresDiarias_DesbloqueaElLogroDeRutina()
        {
            var progreso = new Progreso();
            for (int dia = 0; dia < 3; dia++)
                progreso = ReglasProgreso.Completar(progreso, Diaria(xp: 10), Catalogo, Hoy.AddDays(dia)).Progreso;

            Assert.True(progreso.Logros.ContainsKey("lg05"));
            Assert.Equal(3, ReglasProgreso.TotalDiarias(progreso));
        }

        [Fact]
        public void Completar_NoModificaElProgresoRecibido()
        {
            var original = new Progreso { Experiencia = 50, Nivel = 1, Puntos = 50 };

            ReglasProgreso.Completar(original, Diaria(xp: 20), Catalogo, Hoy);

            Assert.Equal(50, original.Experiencia);
            Assert.Equal(50, original.Puntos);
            Assert.Empty(original.Completados);
            Assert.Empty(original.Logros);
        }

        [Fact]
        public void Completar_MisionSinIdentificador_LanzaExcepcion()
        {
            var mision = Diaria(id: "");
            Assert.Throws<ArgumentException>(() =>
                ReglasProgreso.Completar(new Progreso(), mision, Catalogo, Hoy));
        }

        [Fact]
        public void Completar_MisionSinExperiencia_LanzaExcepcion()
        {
            var mision = Diaria(xp: 0);
            Assert.Throws<ArgumentException>(() =>
                ReglasProgreso.Completar(new Progreso(), mision, Catalogo, Hoy));
        }

        // ------------------------------------------------------------------
        // Totales (los mismos datos que el usuario de prueba 1 de Firebase)
        // ------------------------------------------------------------------

        [Fact]
        public void Totales_CuentanDiariasYUnicasPorSeparado()
        {
            var progreso = new Progreso
            {
                Completados = new()
                {
                    ["m1"] = new()
                    {
                        ["2026-11-20"] = new Completado { Fecha = "2026-11-20", Experiencia = 20 },
                        ["2026-11-21"] = new Completado { Fecha = "2026-11-21", Experiencia = 20 },
                        ["2026-11-22"] = new Completado { Fecha = "2026-11-22", Experiencia = 20 },
                        ["2026-11-24"] = new Completado { Fecha = "2026-11-24", Experiencia = 20 }
                    },
                    ["m3"] = new()
                    {
                        [ReglasProgreso.ClaveUnica] = new Completado { Fecha = "2026-11-18", Experiencia = 150 }
                    }
                }
            };

            Assert.Equal(5, ReglasProgreso.TotalCompletadas(progreso));
            Assert.Equal(4, ReglasProgreso.TotalDiarias(progreso));
            Assert.Equal(1, ReglasProgreso.TotalUnicas(progreso));
        }
    }
}
