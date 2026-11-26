using System.Text.Json.Serialization;

namespace LevelUp.Models
{
    /// <summary>
    /// Datos editables de la cuenta. Se guardan en /usuarios/{uid}/perfil.
    /// </summary>
    public class Usuario
    {
        /// <summary>uid de Firebase Authentication (es la clave del nodo, no se guarda dentro).</summary>
        [JsonIgnore]
        public string Id { get; set; } = "";

        public string Nombre { get; set; } = "";

        public string Email { get; set; } = "";
    }
}
