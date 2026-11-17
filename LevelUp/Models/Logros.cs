
namespace LevelUp.Models
{
    public class Logro
    {
        public string Id { get; set; }

        public string Titulo { get; set; }

        public string Descripcion { get; set; }

        public string Imagen { get; set; }

        public int Experiencia { get; set; }

        public bool Desbloqueado { get; set; }

        public DateTime? FechaDesbloqueo { get; set; }
    }
}
