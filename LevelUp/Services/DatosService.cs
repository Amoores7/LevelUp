using System.Text.Json;
using System.Text.Json.Nodes;
using LevelUp.Models;

namespace LevelUp.Services
{
    /// <summary>
    /// Operaciones de LevelUp sobre la base de datos del usuario que ha iniciado sesión.
    /// Todas las rutas cuelgan de /usuarios/{uid}, que es lo único que permiten las reglas.
    /// </summary>
    public class DatosService
    {
        private const int MaxReintentosConflicto = 3;

        private readonly FirebaseService firebase;
        private readonly string uid;

        public DatosService(FirebaseService firebase, string uid)
        {
            this.firebase = firebase;
            this.uid = uid;
        }

        private string Ruta(string subruta = "") =>
            subruta == "" ? $"usuarios/{uid}" : $"usuarios/{uid}/{subruta}";

        // ------------------------------------------------------------------
        // Perfil y progreso
        // ------------------------------------------------------------------

        /// <summary>
        /// La primera vez que entra una cuenta crea su perfil y su progreso inicial.
        /// Devuelve el perfil.
        /// </summary>
        public async Task<Usuario> PrepararCuentaAsync(string email)
        {
            var perfil = await firebase.GetAsync<Usuario>(Ruta("perfil"));
            if (perfil == null)
            {
                perfil = new Usuario { Nombre = email.Split('@')[0], Email = email };
                await firebase.PutAsync(Ruta("perfil"), perfil);
            }

            var progreso = await firebase.GetAsync<Progreso>(Ruta("progreso"));
            if (progreso == null)
                await firebase.PutAsync(Ruta("progreso"), new Progreso());

            perfil.Id = uid;
            return perfil;
        }

        public async Task<Progreso> ObtenerProgresoAsync() =>
            await firebase.GetAsync<Progreso>(Ruta("progreso")) ?? new Progreso();

        // ------------------------------------------------------------------
        // Catálogo de logros (común y de solo lectura)
        // ------------------------------------------------------------------

        public async Task<List<Logro>> ObtenerCatalogoLogrosAsync()
        {
            var datos = await firebase.GetColeccionAsync<Logro>("catalogo/logros");
            return datos.Select(par => { par.Value.Id = par.Key; return par.Value; })
                        .OrderBy(l => l.Id)
                        .ToList();
        }

        // ------------------------------------------------------------------
        // Misiones
        // ------------------------------------------------------------------

        public async Task<List<Mision>> ObtenerMisionesAsync()
        {
            var datos = await firebase.GetColeccionAsync<Mision>(Ruta("misiones"));
            return datos.Select(par => { par.Value.Id = par.Key; return par.Value; })
                        .OrderBy(m => m.Titulo)
                        .ToList();
        }

        /// <summary>POST: Firebase genera la clave, que se guarda en mision.Id.</summary>
        public async Task<string> CrearMisionAsync(Mision mision)
        {
            mision.Id = await firebase.PostAsync(Ruta("misiones"), mision);
            return mision.Id;
        }

        /// <summary>PATCH de la misión (si se quitó la fecha límite, se borra enviándola a null).</summary>
        public Task ModificarMisionAsync(Mision mision)
        {
            var cambios = JsonSerializer.SerializeToNode(mision, FirebaseService.OpcionesJson)!.AsObject();
            if (mision.FechaLimite == null)
                cambios["fechaLimite"] = null;
            return firebase.PatchAsync(Ruta($"misiones/{mision.Id}"), cambios);
        }

        /// <summary>
        /// Elimina la misión y todas sus actividades con UNA actualización multipath,
        /// para que no queden actividades apuntando a una misión inexistente.
        /// </summary>
        public async Task<int> EliminarMisionAsync(string misionId)
        {
            var actividades = await firebase.GetColeccionAsync<Actividad>(Ruta("actividades"));
            var cambios = new JsonObject { [$"misiones/{misionId}"] = null };

            int borradas = 0;
            foreach (var par in actividades.Where(a => a.Value.MisionId == misionId))
            {
                cambios[$"actividades/{par.Key}"] = null;
                borradas++;
            }

            await firebase.PatchAsync(Ruta(), cambios);
            return borradas;
        }

        /// <summary>
        /// Completa una misión sin conceder puntos dos veces:
        /// lee el progreso con su ETag, comprueba que no esté ya completada, calcula el nuevo
        /// progreso y lo guarda con escritura condicional. Si otro cliente cambió el progreso
        /// entretanto (412), vuelve a leer y lo intenta otra vez.
        /// </summary>
        public async Task<ResultadoCompletar> CompletarMisionAsync(Mision mision, IReadOnlyList<Logro> catalogo,
            DateOnly hoy)
        {
            // Se usan los datos de la misión guardados en Firebase, no los del formulario.
            var guardada = await firebase.GetAsync<Mision>(Ruta($"misiones/{mision.Id}"))
                ?? throw new FirebaseException("La misión ya no existe. Pulsa Actualizar.", TipoErrorFirebase.Otro);
            guardada.Id = mision.Id;

            for (int intento = 1; intento <= MaxReintentosConflicto; intento++)
            {
                var (progreso, etag) = await firebase.GetConETagAsync<Progreso>(Ruta("progreso"));
                var resultado = ReglasProgreso.Completar(progreso ?? new Progreso(), guardada, catalogo, hoy);

                if (!resultado.Completada)
                    return resultado; // ya estaba completada: no se suma nada

                if (await firebase.PutCondicionalAsync(Ruta("progreso"), resultado.Progreso, etag))
                {
                    if (guardada.Tipo == Mision.TipoUnica && guardada.Estado != Mision.EstadoCompletada)
                        await firebase.PatchAsync(Ruta($"misiones/{mision.Id}"),
                            new { estado = Mision.EstadoCompletada });
                    return resultado;
                }
                // 412: el progreso cambió desde que lo leímos → se repite con los datos nuevos.
            }

            throw new FirebaseException(
                "Tu progreso se está modificando desde otro sitio. Inténtalo de nuevo.", TipoErrorFirebase.Otro);
        }

        // ------------------------------------------------------------------
        // Actividades
        // ------------------------------------------------------------------

        public async Task<List<Actividad>> ObtenerActividadesAsync()
        {
            var datos = await firebase.GetColeccionAsync<Actividad>(Ruta("actividades"));
            return datos.Select(par => { par.Value.Id = par.Key; return par.Value; })
                        .OrderByDescending(a => a.Fecha)
                        .ToList();
        }

        public async Task<string> CrearActividadAsync(Actividad actividad)
        {
            actividad.Id = await firebase.PostAsync(Ruta("actividades"), actividad);
            return actividad.Id;
        }

        public Task ModificarActividadAsync(Actividad actividad) =>
            firebase.PatchAsync(Ruta($"actividades/{actividad.Id}"), actividad);

        public Task EliminarActividadAsync(string actividadId) =>
            firebase.DeleteAsync(Ruta($"actividades/{actividadId}"));
    }
}
