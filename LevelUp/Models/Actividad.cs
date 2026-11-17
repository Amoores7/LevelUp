
namespace LevelUp.Models
{
    public class Actividad
    {
        public string Id { get; set; }

        public string MisionId { get; set; }

        public string Descripcion { get; set; }

        public string Tipo { get; set; }

        public DateTime Fecha { get; set; }

        public int Duracion { get; set; }

        public int Cantidad { get; set; }
    }
}
