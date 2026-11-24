using LevelUp.Models;

namespace LevelUp.Services
{
    /// <summary>Resultado de intentar completar una misión.</summary>
    public class ResultadoCompletar
    {
        /// <summary>false si la misión ya estaba completada (hoy, si es diaria) y no se ha sumado nada.</summary>
        public bool Completada { get; init; }

        /// <summary>Progreso resultante (el mismo de entrada si no se completó).</summary>
        public Progreso Progreso { get; init; } = new();

        public int ExperienciaGanada { get; init; }

        public bool SubioDeNivel { get; init; }

        public List<Logro> LogrosNuevos { get; init; } = new();
    }

    /// <summary>
    /// Reglas del juego (sin acceso a Firebase, para poder probarlas):
    /// cálculo de nivel, cuándo una misión cuenta como completada y qué logros se desbloquean.
    /// </summary>
    public static class ReglasProgreso
    {
        public const int XpPorNivel = 100;
        public const string ClaveUnica = "unica";

        /// <summary>Nivel 1 con 0-99 XP, nivel 2 con 100-199 XP…</summary>
        public static int CalcularNivel(int experiencia) => Math.Max(0, experiencia) / XpPorNivel + 1;

        /// <summary>XP acumulada dentro del nivel actual (para la barra de progreso).</summary>
        public static int XpDentroDelNivel(int experiencia) => Math.Max(0, experiencia) % XpPorNivel;

        /// <summary>
        /// Clave con la que se registra el completado: la fecha si es diaria (una vez por día)
        /// o "unica" si es única (una sola vez).
        /// </summary>
        public static string ClaveCompletado(Mision mision, DateOnly hoy) =>
            mision.Tipo == Mision.TipoDiaria ? hoy.ToString("yyyy-MM-dd") : ClaveUnica;

        public static bool EstaCompletada(Progreso progreso, Mision mision, DateOnly hoy) =>
            progreso.Completados.TryGetValue(mision.Id, out var registros)
            && registros.ContainsKey(ClaveCompletado(mision, hoy));

        public static int TotalCompletadas(Progreso p) => p.Completados.Values.Sum(r => r.Count);

        public static int TotalDiarias(Progreso p) =>
            p.Completados.Values.Sum(r => r.Keys.Count(k => k != ClaveUnica));

        public static int TotalUnicas(Progreso p) =>
            p.Completados.Values.Count(r => r.ContainsKey(ClaveUnica));

        /// <summary>Valor actual del jugador para el tipo de condición de un logro.</summary>
        public static int ValorCondicion(Progreso p, string tipoCondicion) => tipoCondicion switch
        {
            Logro.CondicionCompletadas => TotalCompletadas(p),
            Logro.CondicionDiarias => TotalDiarias(p),
            Logro.CondicionUnicas => TotalUnicas(p),
            Logro.CondicionNivel => p.Nivel,
            Logro.CondicionExperiencia => p.Experiencia,
            _ => 0
        };

        /// <summary>Logros del catálogo que el progreso cumple y que aún no estaban desbloqueados.</summary>
        public static List<Logro> LogrosPorDesbloquear(Progreso p, IEnumerable<Logro> catalogo) =>
            catalogo
                .Where(l => !p.Logros.ContainsKey(l.Id)
                            && ValorCondicion(p, l.Condicion.Tipo) >= l.Condicion.Valor)
                .ToList();

        /// <summary>
        /// Calcula el progreso tras completar la misión. Si ya estaba completada no suma nada
        /// (así nunca se conceden puntos dos veces por la misma acción).
        /// No modifica el objeto recibido: devuelve una copia.
        /// </summary>
        public static ResultadoCompletar Completar(Progreso actual, Mision mision,
            IEnumerable<Logro> catalogo, DateOnly hoy)
        {
            if (string.IsNullOrEmpty(mision.Id))
                throw new ArgumentException("La misión no tiene identificador.", nameof(mision));
            if (mision.Experiencia <= 0)
                throw new ArgumentException("La misión debe dar al menos 1 XP.", nameof(mision));

            if (EstaCompletada(actual, mision, hoy))
                return new ResultadoCompletar { Completada = false, Progreso = actual };

            var nuevo = Copiar(actual);
            string fecha = hoy.ToString("yyyy-MM-dd");

            if (!nuevo.Completados.TryGetValue(mision.Id, out var registros))
            {
                registros = new Dictionary<string, Completado>();
                nuevo.Completados[mision.Id] = registros;
            }
            registros[ClaveCompletado(mision, hoy)] = new Completado { Fecha = fecha, Experiencia = mision.Experiencia };

            int nivelAnterior = actual.Nivel;
            nuevo.Experiencia += mision.Experiencia;
            nuevo.Puntos += mision.Experiencia;
            nuevo.Nivel = CalcularNivel(nuevo.Experiencia);

            // Los logros dan puntos, no experiencia, así que se evalúan una sola vez.
            var logrosNuevos = LogrosPorDesbloquear(nuevo, catalogo);
            foreach (var logro in logrosNuevos)
            {
                nuevo.Logros[logro.Id] = fecha;
                nuevo.Puntos += logro.Puntos;
            }

            return new ResultadoCompletar
            {
                Completada = true,
                Progreso = nuevo,
                ExperienciaGanada = mision.Experiencia,
                SubioDeNivel = nuevo.Nivel > nivelAnterior,
                LogrosNuevos = logrosNuevos
            };
        }

        public static Progreso Copiar(Progreso p) => new()
        {
            Experiencia = p.Experiencia,
            Nivel = p.Nivel,
            Puntos = p.Puntos,
            Completados = p.Completados.ToDictionary(
                par => par.Key,
                par => par.Value.ToDictionary(r => r.Key,
                    r => new Completado { Fecha = r.Value.Fecha, Experiencia = r.Value.Experiencia })),
            Logros = new Dictionary<string, string>(p.Logros)
        };
    }
}
