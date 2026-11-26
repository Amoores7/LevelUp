using System.Net;
using System.Net.Http;
using System.Text;

namespace LevelUp.Tests.Fakes
{
    /// <summary>Petición que ha recibido el servidor simulado.</summary>
    public record PeticionRegistrada(HttpMethod Metodo, string Url, string? Cuerpo, string? IfMatch, string? PideETag);

    /// <summary>
    /// Sustituye a Internet en las pruebas: guarda cada petición que hace la aplicación
    /// y responde lo que indique la función <see cref="Responder"/>. Así se prueba el
    /// acceso a Firebase sin conexión y sin tocar la base de datos real.
    /// </summary>
    public class FakeHttpHandler : HttpMessageHandler
    {
        public List<PeticionRegistrada> Peticiones { get; } = new();

        /// <summary>Recibe la petición registrada y devuelve la respuesta simulada.</summary>
        public Func<PeticionRegistrada, HttpResponseMessage> Responder { get; set; } = _ => Json(HttpStatusCode.OK, "null");

        /// <summary>Si se pone, se lanza al enviar (simula que no hay conexión).</summary>
        public Exception? ErrorDeConexion { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (ErrorDeConexion != null)
                throw ErrorDeConexion;

            string? cuerpo = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            // Se leen sin validar porque Firebase usa ETags sin comillas.
            string? ifMatch = request.Headers.NonValidated.TryGetValues("if-match", out var v1) ? v1.ToString() : null;
            string? pideETag = request.Headers.NonValidated.TryGetValues("X-Firebase-ETag", out var v2) ? v2.ToString() : null;

            var peticion = new PeticionRegistrada(request.Method, request.RequestUri!.ToString(), cuerpo, ifMatch, pideETag);
            Peticiones.Add(peticion);
            return Responder(peticion);
        }

        public static HttpResponseMessage Json(HttpStatusCode codigo, string json, string? etag = null)
        {
            var respuesta = new HttpResponseMessage(codigo)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            if (etag != null)
                respuesta.Headers.TryAddWithoutValidation("ETag", etag);
            return respuesta;
        }
    }
}
