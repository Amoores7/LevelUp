using System.Text.Json.Serialization;

namespace LevelUp.Models
{
    /// <summary>
    /// Un objetivo personal convertido en misión. Se guarda en /usuarios/{uid}/misiones/{id}.
    /// </summary>
    public class Mision
    {
        public const string TipoDiaria = "Diaria";
        public const string TipoUnica = "Única";

        public const string EstadoPendiente = "Pendiente";
        public const string EstadoEnCurso = "En curso";
        public const string EstadoCompletada = "Completada";

        public static readonly string[] Tipos = { TipoDiaria, TipoUnica };
        public static readonly string[] Estados = { EstadoPendiente, EstadoEnCurso, EstadoCompletada };
        public static readonly string[] Categorias = { "Salud", "Deporte", "Estudio", "Trabajo", "Hogar", "Bienestar", "Ocio", "Otros" };

        /// <summary>Clave generada por Firebase al crearla (no se guarda dentro del nodo).</summary>
        [JsonIgnore]
        public string Id { get; set; } = "";

        public string Titulo { get; set; } = "";

        public string Descripcion { get; set; } = "";

        public string Categoria { get; set; } = "";

        /// <summary>XP que concede al completarla.</summary>
        public int Experiencia { get; set; }

        public string Tipo { get; set; } = TipoDiaria;

        public string Estado { get; set; } = EstadoPendiente;

        public DateOnly? FechaLimite { get; set; }

        public string Creada { get; set; } = "";

        // ---- Propiedades solo para mostrar en la interfaz (no se guardan) ----

        /// <summary>Se rellena al cargar: true si una diaria ya se hizo hoy o una única ya se hizo.</summary>
        [JsonIgnore]
        public bool CompletadaAhora { get; set; }

        [JsonIgnore]
        public string EstadoTexto =>
            Tipo == TipoDiaria && CompletadaAhora ? "Hecha hoy" : Estado;

        [JsonIgnore]
        public string FechaLimiteTexto => FechaLimite?.ToString("dd/MM/yyyy") ?? "—";

        public override string ToString() => Titulo;
    }
}
