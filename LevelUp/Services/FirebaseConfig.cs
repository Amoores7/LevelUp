using System.IO;
using System.Text.Json;

namespace LevelUp.Services
{
    /// <summary>
    /// Configuración pública de Firebase, leída de firebase.config.json (junto al .exe).
    /// Solo contiene la API key web y la URL de la base de datos: nunca contraseñas
    /// ni claves privadas de cuentas de servicio.
    /// </summary>
    public class FirebaseConfig
    {
        public const string NombreArchivo = "firebase.config.json";

        public string ApiKey { get; set; } = "";

        public string DatabaseUrl { get; set; } = "";

        public static FirebaseConfig Cargar()
        {
            string ruta = Path.Combine(AppContext.BaseDirectory, NombreArchivo);

            if (!File.Exists(ruta))
                throw new FirebaseException(
                    $"No se encuentra {NombreArchivo}. Copia firebase.config.example.json, renómbralo y pon tu API key.",
                    TipoErrorFirebase.Configuracion);

            var config = JsonSerializer.Deserialize<FirebaseConfig>(
                File.ReadAllText(ruta),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (config == null || string.IsNullOrWhiteSpace(config.ApiKey) || config.ApiKey.StartsWith("PON_AQUI")
                || string.IsNullOrWhiteSpace(config.DatabaseUrl))
                throw new FirebaseException(
                    $"{NombreArchivo} está incompleto: revisa apiKey y databaseUrl.",
                    TipoErrorFirebase.Configuracion);

            config.DatabaseUrl = config.DatabaseUrl.TrimEnd('/');
            return config;
        }
    }
}
