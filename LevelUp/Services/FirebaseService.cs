using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LevelUp.Services
{
    /// <summary>
    /// Acceso a la API REST de Realtime Database con HttpClient y JSON.
    /// GET consulta, POST crea con clave generada, PUT reemplaza, PATCH modifica
    /// (también actualizaciones multipath) y DELETE elimina. Todas las rutas terminan en .json
    /// y llevan el ID token en ?auth=.
    /// </summary>
    public class FirebaseService
    {
        /// <summary>Opciones JSON comunes: propiedades en camelCase y sin escribir nulos.</summary>
        public static readonly JsonSerializerOptions OpcionesJson = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient http;
        private readonly string urlBase;
        private readonly IProveedorToken tokens;

        public FirebaseService(HttpClient http, string urlBase, IProveedorToken tokens)
        {
            this.http = http;
            this.urlBase = urlBase.TrimEnd('/');
            this.tokens = tokens;
        }

        // ------------------------------------------------------------------
        // Operaciones
        // ------------------------------------------------------------------

        /// <summary>GET. Devuelve null si el nodo no existe.</summary>
        public async Task<T?> GetAsync<T>(string ruta)
        {
            using var respuesta = await EnviarAsync(HttpMethod.Get, ruta, null);
            return await LeerAsync<T>(respuesta);
        }

        /// <summary>GET de una colección: devuelve un diccionario clave → elemento (vacío si no hay nada).</summary>
        public async Task<Dictionary<string, T>> GetColeccionAsync<T>(string ruta)
        {
            return await GetAsync<Dictionary<string, T>>(ruta) ?? new Dictionary<string, T>();
        }

        /// <summary>GET pidiendo también el ETag del nodo, para luego escribir de forma condicional.</summary>
        public async Task<(T? Valor, string ETag)> GetConETagAsync<T>(string ruta)
        {
            using var respuesta = await EnviarAsync(HttpMethod.Get, ruta, null,
                ("X-Firebase-ETag", "true"));

            // Firebase manda el ETag sin comillas, así que se lee sin validar el formato.
            string etag = respuesta.Headers.NonValidated.TryGetValues("ETag", out var valores)
                ? valores.ToString()
                : "";

            return (await LeerAsync<T>(respuesta), etag);
        }

        /// <summary>POST: crea un elemento con clave generada por Firebase y devuelve esa clave.</summary>
        public async Task<string> PostAsync<T>(string ruta, T dato)
        {
            using var respuesta = await EnviarAsync(HttpMethod.Post, ruta, Serializar(dato));
            var resultado = await LeerAsync<RespuestaPost>(respuesta);
            return resultado?.Name ?? throw new FirebaseException(
                "Firebase no devolvió la clave del registro creado.", TipoErrorFirebase.Otro);
        }

        /// <summary>PUT: escribe (o reemplaza) el nodo completo.</summary>
        public async Task PutAsync<T>(string ruta, T dato)
        {
            using var respuesta = await EnviarAsync(HttpMethod.Put, ruta, Serializar(dato));
        }

        /// <summary>
        /// PUT condicional: solo escribe si el nodo no ha cambiado desde que se leyó (mismo ETag).
        /// Devuelve false si otro cliente lo modificó antes (HTTP 412).
        /// </summary>
        public async Task<bool> PutCondicionalAsync<T>(string ruta, T dato, string etag)
        {
            using var respuesta = await EnviarAsync(HttpMethod.Put, ruta, Serializar(dato),
                ("if-match", etag));
            return respuesta.StatusCode != HttpStatusCode.PreconditionFailed;
        }

        /// <summary>
        /// PATCH: modifica solo los campos indicados. Si las claves son rutas
        /// ("misiones/abc": null, "actividades/x": null) es una actualización multipath atómica.
        /// </summary>
        public async Task PatchAsync(string ruta, object cambios)
        {
            using var respuesta = await EnviarAsync(HttpMethod.Patch, ruta, Serializar(cambios));
        }

        /// <summary>DELETE: elimina el nodo.</summary>
        public async Task DeleteAsync(string ruta)
        {
            using var respuesta = await EnviarAsync(HttpMethod.Delete, ruta, null);
        }

        // ------------------------------------------------------------------
        // Funcionamiento interno
        // ------------------------------------------------------------------

        public string ConstruirUrl(string ruta, string token)
        {
            return $"{urlBase}/{ruta.Trim('/')}.json?auth={Uri.EscapeDataString(token)}";
        }

        private static string Serializar<T>(T dato) => JsonSerializer.Serialize(dato, OpcionesJson);

        /// <summary>
        /// Envía la petición. Si Firebase responde que el token ha caducado, lo renueva
        /// y repite la petición una vez. Traduce los errores a FirebaseException.
        /// </summary>
        private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, string? json,
            params (string Nombre, string Valor)[] cabeceras)
        {
            for (int intento = 1; ; intento++)
            {
                string token = await tokens.ObtenerTokenAsync(forzarRenovacion: intento > 1);

                var peticion = new HttpRequestMessage(metodo, ConstruirUrl(ruta, token));
                if (json != null)
                    peticion.Content = new StringContent(json, Encoding.UTF8, "application/json");
                foreach (var (nombre, valor) in cabeceras)
                    peticion.Headers.TryAddWithoutValidation(nombre, valor);

                HttpResponseMessage respuesta;
                try
                {
                    respuesta = await http.SendAsync(peticion);
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                {
                    throw new FirebaseException("No se pudo conectar con Firebase. Revisa tu conexión a Internet.",
                        TipoErrorFirebase.Conexion, ex);
                }

                if (respuesta.IsSuccessStatusCode || respuesta.StatusCode == HttpStatusCode.PreconditionFailed)
                    return respuesta;

                string error = await respuesta.Content.ReadAsStringAsync();
                bool tokenCaducado = error.Contains("expired", StringComparison.OrdinalIgnoreCase);

                if (respuesta.StatusCode == HttpStatusCode.Unauthorized && tokenCaducado && intento == 1)
                {
                    respuesta.Dispose();
                    continue; // se renueva el token y se repite
                }

                respuesta.Dispose();
                throw TraducirError(respuesta.StatusCode, error);
            }
        }

        public static FirebaseException TraducirError(HttpStatusCode codigo, string cuerpo)
        {
            if (cuerpo.Contains("expired", StringComparison.OrdinalIgnoreCase))
                return new FirebaseException("Tu sesión ha caducado. Vuelve a iniciar sesión.",
                    TipoErrorFirebase.SesionCaducada);

            if (codigo == HttpStatusCode.Unauthorized || codigo == HttpStatusCode.Forbidden
                || cuerpo.Contains("Permission denied", StringComparison.OrdinalIgnoreCase))
                return new FirebaseException(
                    "Firebase ha denegado la operación: no tienes permiso o los datos no cumplen las reglas.",
                    TipoErrorFirebase.AccesoDenegado);

            if (codigo == HttpStatusCode.NotFound)
                return new FirebaseException("No se encuentra la base de datos. Revisa databaseUrl.",
                    TipoErrorFirebase.Configuracion);

            return new FirebaseException($"Error de Firebase ({(int)codigo}).", TipoErrorFirebase.Otro);
        }

        private static async Task<T?> LeerAsync<T>(HttpResponseMessage respuesta)
        {
            string texto = await respuesta.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(texto) || texto == "null")
                return default;
            return JsonSerializer.Deserialize<T>(texto, OpcionesJson);
        }

        private class RespuestaPost
        {
            public string Name { get; set; } = "";
        }
    }
}
