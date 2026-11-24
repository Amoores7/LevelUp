using System.Text.Json.Serialization;

namespace LevelUp.Models
{
    /// <summary>
    /// Tiempo dedicado a una misión en un día concreto.
    /// Se guarda en /usuarios/{uid}/actividades/{id}.
    /// </summary>
    public class Actividad
    {
        [JsonIgnore]
        public string Id { get; set; } = "";

        /// <summary>Referencia por identificador a /usuarios/{uid}/misiones/{misionId}.</summary>
        public string MisionId { get; set; } = "";

        public string Descripcion { get; set; } = "";

        public DateOnly Fecha { get; set; }

        public int Minutos { get; set; }

        // ---- Propiedades solo para mostrar en la interfaz (no se guardan) ----

        /// <summary>Título de la misión relacionada; se rellena al cargar.</summary>
        [JsonIgnore]
        public string Mision { get; set; } = "";

        [JsonIgnore]
        public string Tiempo => Minutos >= 60
            ? $"{Minutos / 60} h {Minutos % 60:00} min"
            : $"{Minutos} min";

        [JsonIgnore]
        public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    }
}
