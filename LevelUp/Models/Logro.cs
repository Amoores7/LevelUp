using System.Text.Json.Serialization;

namespace LevelUp.Models
{
    /// <summary>
    /// Insignia del catálogo (/catalogo/logros/{id}). El catálogo es común y de solo lectura;
    /// lo que desbloquea cada usuario se guarda en /usuarios/{uid}/progreso/logros.
    /// </summary>
    public class Logro
    {
        public const string CondicionCompletadas = "completadas";
        public const string CondicionDiarias = "diarias";
        public const string CondicionUnicas = "unicas";
        public const string CondicionNivel = "nivel";
        public const string CondicionExperiencia = "experiencia";

        [JsonIgnore]
        public string Id { get; set; } = "";

        public string Titulo { get; set; } = "";

        public string Descripcion { get; set; } = "";

        public string Icono { get; set; } = "";

        /// <summary>Puntos de recompensa al desbloquearlo.</summary>
        public int Puntos { get; set; }

        public CondicionLogro Condicion { get; set; } = new();

        // ---- Estado para el usuario actual (no se guarda en el catálogo) ----

        [JsonIgnore]
        public bool Desbloqueado { get; set; }

        [JsonIgnore]
        public string? FechaDesbloqueo { get; set; }

        [JsonIgnore]
        public string EstadoTexto => Desbloqueado
            ? $"Desbloqueado el {FechaDesbloqueo}"
            : "Bloqueado";

        [JsonIgnore]
        public double Opacidad => Desbloqueado ? 1.0 : 0.45;
    }

    public class CondicionLogro
    {
        public string Tipo { get; set; } = "";

        public int Valor { get; set; }
    }
}
