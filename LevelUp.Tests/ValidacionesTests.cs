using LevelUp.Models;
using LevelUp.Services;

namespace LevelUp.Tests
{
    /// <summary>Comprobaciones de los formularios antes de guardar nada en Firebase.</summary>
    public class ValidacionesTests
    {
        private static readonly DateOnly Hoy = new(2026, 11, 24);

        // ------------------------------------------------------------------
        // Login
        // ------------------------------------------------------------------

        [Fact]
        public void ValidarLogin_DatosCorrectos_NoDaErrores()
        {
            Assert.Empty(Validaciones.ValidarLogin("prueba1@levelup.com", "secreta"));
        }

        [Fact]
        public void ValidarLogin_CamposVacios_PideCorreoYContrasena()
        {
            var errores = Validaciones.ValidarLogin("", "");
            Assert.Equal(2, errores.Count);
        }

        [Theory]
        [InlineData("correo-sin-arroba")]
        [InlineData("correo@sindominio")]
        [InlineData("con espacio@levelup.com")]
        public void ValidarLogin_CorreoMalEscrito_DaError(string correo)
        {
            var errores = Validaciones.ValidarLogin(correo, "secreta");
            Assert.Contains(errores, e => e.Contains("formato"));
        }

        // ------------------------------------------------------------------
        // Misiones
        // ------------------------------------------------------------------

        /// <summary>Valida una misión cambiando solo los campos indicados.</summary>
        private static List<string> ValidarMision(string titulo = "Leer 20 minutos", string descripcion = "",
            string? categoria = "Estudio", string textoXp = "20", string? tipo = Mision.TipoDiaria,
            string? estado = Mision.EstadoPendiente, DateOnly? fechaLimite = null, string? estadoAnterior = null)
        {
            return Validaciones.ValidarMision(titulo, descripcion, categoria, textoXp, tipo, estado,
                fechaLimite, estadoAnterior, Hoy, out _);
        }

        [Fact]
        public void ValidarMision_DatosCorrectos_NoDaErroresYDevuelveLaExperiencia()
        {
            var errores = Validaciones.ValidarMision("Leer 20 minutos", "", "Estudio", " 20 ", Mision.TipoDiaria,
                Mision.EstadoPendiente, null, null, Hoy, out int experiencia);

            Assert.Empty(errores);
            Assert.Equal(20, experiencia);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidarMision_SinTitulo_DaError(string titulo)
        {
            Assert.Contains(ValidarMision(titulo: titulo), e => e.Contains("título es obligatorio"));
        }

        [Fact]
        public void ValidarMision_TituloDemasiadoLargo_DaError()
        {
            string titulo = new('a', Validaciones.MaxTitulo + 1);
            Assert.Contains(ValidarMision(titulo: titulo), e => e.Contains("título no puede superar"));
        }

        [Fact]
        public void ValidarMision_ExperienciaQueNoEsUnNumero_DaError()
        {
            Assert.Contains(ValidarMision(textoXp: "veinte"), e => e.Contains("número entero"));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-5")]
        [InlineData("501")]
        public void ValidarMision_ExperienciaFueraDeRango_DaError(string textoXp)
        {
            Assert.Contains(ValidarMision(textoXp: textoXp), e => e.Contains("entre 1 y 500"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("Inventada")]
        public void ValidarMision_CategoriaNoValida_DaError(string? categoria)
        {
            Assert.Contains(ValidarMision(categoria: categoria), e => e.Contains("categoría"));
        }

        [Fact]
        public void ValidarMision_TipoNoValido_DaError()
        {
            Assert.Contains(ValidarMision(tipo: "Semanal"), e => e.Contains("tipo de misión"));
        }

        [Fact]
        public void ValidarMision_MarcarComoCompletadaDesdeElFormulario_DaError()
        {
            // Para completar hay que usar el botón: si no, no se sumaría la experiencia.
            var errores = ValidarMision(tipo: Mision.TipoUnica, estado: Mision.EstadoCompletada,
                estadoAnterior: Mision.EstadoPendiente);
            Assert.Contains(errores, e => e.Contains("Completar misión"));
        }

        [Fact]
        public void ValidarMision_UnicaYaCompletada_SePuedeModificarSinCambiarElEstado()
        {
            var errores = ValidarMision(tipo: Mision.TipoUnica, estado: Mision.EstadoCompletada,
                estadoAnterior: Mision.EstadoCompletada);
            Assert.Empty(errores);
        }

        [Fact]
        public void ValidarMision_DiariaEnEstadoCompletada_DaError()
        {
            var errores = ValidarMision(tipo: Mision.TipoDiaria, estado: Mision.EstadoCompletada,
                estadoAnterior: Mision.EstadoCompletada);
            Assert.Contains(errores, e => e.Contains("diaria"));
        }

        [Fact]
        public void ValidarMision_NuevaConFechaLimitePasada_DaError()
        {
            var errores = ValidarMision(fechaLimite: Hoy.AddDays(-1));
            Assert.Contains(errores, e => e.Contains("fecha límite"));
        }

        [Fact]
        public void ValidarMision_ExistenteConFechaLimitePasada_SePuedeGuardar()
        {
            // Una misión creada hace tiempo puede tener ya la fecha vencida.
            var errores = ValidarMision(fechaLimite: Hoy.AddDays(-1), estadoAnterior: Mision.EstadoEnCurso);
            Assert.Empty(errores);
        }

        // ------------------------------------------------------------------
        // Actividades
        // ------------------------------------------------------------------

        private static readonly Mision MisionExistente = new() { Id = "m1", Titulo = "Leer 20 minutos" };
        private static readonly string[] IdsExistentes = { "m1", "m2" };

        private static List<string> ValidarActividad(string descripcion = "Capítulo 3", Mision? mision = null,
            DateOnly? fecha = null, string textoMinutos = "20", bool sinMision = false, bool sinFecha = false)
        {
            return Validaciones.ValidarActividad(descripcion, sinMision ? null : mision ?? MisionExistente,
                IdsExistentes, sinFecha ? null : fecha ?? Hoy, textoMinutos, Hoy, out _);
        }

        [Fact]
        public void ValidarActividad_DatosCorrectos_NoDaErroresYDevuelveLosMinutos()
        {
            var errores = Validaciones.ValidarActividad("Capítulo 3", MisionExistente, IdsExistentes,
                Hoy, "45", Hoy, out int minutos);

            Assert.Empty(errores);
            Assert.Equal(45, minutos);
        }

        [Fact]
        public void ValidarActividad_SinDescripcion_DaError()
        {
            Assert.Contains(ValidarActividad(descripcion: " "), e => e.Contains("descripción es obligatoria"));
        }

        [Fact]
        public void ValidarActividad_SinMision_DaError()
        {
            Assert.Contains(ValidarActividad(sinMision: true), e => e.Contains("misión relacionada"));
        }

        [Fact]
        public void ValidarActividad_MisionQueYaNoExiste_DaError()
        {
            var borrada = new Mision { Id = "m-borrada", Titulo = "Misión eliminada" };
            Assert.Contains(ValidarActividad(mision: borrada), e => e.Contains("ya no existe"));
        }

        [Fact]
        public void ValidarActividad_SinFecha_DaError()
        {
            Assert.Contains(ValidarActividad(sinFecha: true), e => e.Contains("fecha"));
        }

        [Fact]
        public void ValidarActividad_FechaFutura_DaError()
        {
            Assert.Contains(ValidarActividad(fecha: Hoy.AddDays(1)), e => e.Contains("fecha futura"));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("1441")]
        public void ValidarActividad_MinutosFueraDeRango_DaError(string textoMinutos)
        {
            Assert.Contains(ValidarActividad(textoMinutos: textoMinutos), e => e.Contains("entre 1 y 1440"));
        }

        [Fact]
        public void ValidarActividad_MinutosQueNoSonUnNumero_DaError()
        {
            Assert.Contains(ValidarActividad(textoMinutos: "media hora"), e => e.Contains("número entero"));
        }

        [Theory]
        [InlineData("1")]
        [InlineData("1440")]
        public void ValidarActividad_MinutosEnElLimite_SonValidos(string textoMinutos)
        {
            Assert.Empty(ValidarActividad(textoMinutos: textoMinutos));
        }
    }
}
