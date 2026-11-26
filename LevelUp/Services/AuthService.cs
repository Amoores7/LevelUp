using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LevelUp.Services
{
    /// <summary>Quien sabe dar un ID token válido para las peticiones a la base de datos.</summary>
    public interface IProveedorToken
    {
        /// <param name="forzarRenovacion">true para pedir un token nuevo aunque el actual no haya caducado.</param>
        Task<string> ObtenerTokenAsync(bool forzarRenovacion = false);
    }

    /// <summary>
    /// Inicio de sesión con correo y contraseña usando la API REST de Firebase Authentication.
    /// Guarda en memoria el ID token y el refresh token; la contraseña nunca se guarda.
    /// </summary>
    public class AuthService : IProveedorToken
    {
        private const string UrlLogin = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=";
        private const string UrlRenovar = "https://securetoken.googleapis.com/v1/token?key=";

        private readonly HttpClient http;
        private readonly string apiKey;

        private string? idToken;
        private string? refreshToken;
        private DateTime caducaUtc;

        public string Uid { get; private set; } = "";
        public string Email { get; private set; } = "";
        public bool SesionIniciada => idToken != null;

        public AuthService(HttpClient http, string apiKey)
        {
            this.http = http;
            this.apiKey = apiKey;
        }

        public async Task IniciarSesionAsync(string email, string password)
        {
            var cuerpo = new { email, password, returnSecureToken = true };

            HttpResponseMessage respuesta;
            try
            {
                respuesta = await http.PostAsJsonAsync(UrlLogin + apiKey, cuerpo);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                throw new FirebaseException("No se pudo conectar con Firebase. Revisa tu conexión a Internet.",
                    TipoErrorFirebase.Conexion, ex);
            }

            string texto = await respuesta.Content.ReadAsStringAsync();

            if (!respuesta.IsSuccessStatusCode)
                throw new FirebaseException(TraducirErrorLogin(texto), TipoErrorFirebase.Credenciales);

            var datos = JsonSerializer.Deserialize<RespuestaLogin>(texto)!;
            idToken = datos.IdToken;
            refreshToken = datos.RefreshToken;
            caducaUtc = DateTime.UtcNow.AddSeconds(int.Parse(datos.ExpiresIn));
            Uid = datos.LocalId;
            Email = datos.Email;
        }

        public async Task<string> ObtenerTokenAsync(bool forzarRenovacion = false)
        {
            if (idToken == null || refreshToken == null)
                throw new FirebaseException("No hay ninguna sesión iniciada.", TipoErrorFirebase.SesionCaducada);

            // Se renueva un minuto antes de que caduque (los tokens duran 1 hora).
            if (forzarRenovacion || DateTime.UtcNow >= caducaUtc.AddMinutes(-1))
                await RenovarTokenAsync();

            return idToken!;
        }

        /// <summary>Pide un ID token nuevo con el refresh token. Si falla, la sesión ha caducado.</summary>
        private async Task RenovarTokenAsync()
        {
            var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken!
            });

            HttpResponseMessage respuesta;
            try
            {
                respuesta = await http.PostAsync(UrlRenovar + apiKey, formulario);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                throw new FirebaseException("No se pudo conectar con Firebase para renovar la sesión.",
                    TipoErrorFirebase.Conexion, ex);
            }

            if (!respuesta.IsSuccessStatusCode)
            {
                CerrarSesion();
                throw new FirebaseException("Tu sesión ha caducado. Vuelve a iniciar sesión.",
                    TipoErrorFirebase.SesionCaducada);
            }

            var datos = (await respuesta.Content.ReadFromJsonAsync<RespuestaRenovar>())!;
            idToken = datos.IdToken;
            refreshToken = datos.RefreshToken;
            caducaUtc = DateTime.UtcNow.AddSeconds(int.Parse(datos.ExpiresIn));
        }

        public void CerrarSesion()
        {
            idToken = null;
            refreshToken = null;
            Uid = "";
            Email = "";
        }

        /// <summary>Convierte los códigos de error de Firebase Auth en mensajes comprensibles.</summary>
        public static string TraducirErrorLogin(string json)
        {
            string codigo = "";
            try
            {
                using var doc = JsonDocument.Parse(json);
                codigo = doc.RootElement.GetProperty("error").GetProperty("message").GetString() ?? "";
            }
            catch (Exception)
            {
                // Respuesta no JSON: se usa el mensaje genérico.
            }

            return codigo switch
            {
                "INVALID_LOGIN_CREDENTIALS" or "INVALID_PASSWORD" or "EMAIL_NOT_FOUND"
                    => "Correo o contraseña incorrectos.",
                "INVALID_EMAIL" => "El correo electrónico no tiene un formato válido.",
                "USER_DISABLED" => "Esta cuenta está deshabilitada.",
                var c when c.StartsWith("TOO_MANY_ATTEMPTS_TRY_LATER")
                    => "Demasiados intentos fallidos. Espera unos minutos.",
                var c when c.Contains("API key not valid") || c.Contains("API_KEY_INVALID")
                    => "La API key de firebase.config.json no es válida.",
                _ => "No se pudo iniciar sesión" + (codigo == "" ? "." : $" ({codigo}).")
            };
        }

        private class RespuestaLogin
        {
            [JsonPropertyName("idToken")] public string IdToken { get; set; } = "";
            [JsonPropertyName("refreshToken")] public string RefreshToken { get; set; } = "";
            [JsonPropertyName("expiresIn")] public string ExpiresIn { get; set; } = "3600";
            [JsonPropertyName("localId")] public string LocalId { get; set; } = "";
            [JsonPropertyName("email")] public string Email { get; set; } = "";
        }

        private class RespuestaRenovar
        {
            [JsonPropertyName("id_token")] public string IdToken { get; set; } = "";
            [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = "";
            [JsonPropertyName("expires_in")] public string ExpiresIn { get; set; } = "3600";
        }
    }
}
