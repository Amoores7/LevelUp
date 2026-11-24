namespace LevelUp.Models
{
    /// <summary>
    /// Estado del jugador. Se guarda en /usuarios/{uid}/progreso y se escribe
    /// siempre de forma condicional (ETag) porque depende del valor anterior.
    /// </summary>
    public class Progreso
    {
        public int Experiencia { get; set; }

        public int Nivel { get; set; } = 1;

        public int Puntos { get; set; }

        /// <summary>
        /// misionId → (clave → registro). La clave es la fecha (yyyy-MM-dd) en las
        /// misiones diarias o "unica" en las misiones únicas.
        /// </summary>
        public Dictionary<string, Dictionary<string, Completado>> Completados { get; set; } = new();

        /// <summary>logroId → fecha de desbloqueo (yyyy-MM-dd).</summary>
        public Dictionary<string, string> Logros { get; set; } = new();
    }

    /// <summary>Marca de que una misión se completó (y cuánta XP dio).</summary>
    public class Completado
    {
        public string Fecha { get; set; } = "";

        public int Experiencia { get; set; }
    }
}
