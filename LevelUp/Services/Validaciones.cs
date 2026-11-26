using System.Text.RegularExpressions;
using LevelUp.Models;

namespace LevelUp.Services
{
    /// <summary>
    /// Comprobaciones de los formularios antes de enviar nada a Firebase.
    /// Cada método devuelve la lista de errores (vacía si todo es correcto).
    /// </summary>
    public static class Validaciones
    {
        public const int MaxTitulo = 60;
        public const int MaxDescripcionMision = 300;
        public const int MinXp = 1;
        public const int MaxXp = 500;
        public const int MaxDescripcionActividad = 100;
        public const int MaxMinutos = 1440;
        public const int MaxNombre = 50;

        public static List<string> ValidarLogin(string correo, string password)
        {
            var errores = new List<string>();
            if (string.IsNullOrWhiteSpace(correo))
                errores.Add("Introduce tu correo electrónico.");
            else if (!Regex.IsMatch(correo.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                errores.Add("El correo electrónico no tiene un formato válido.");
            if (string.IsNullOrEmpty(password))
                errores.Add("Introduce tu contraseña.");
            return errores;
        }

        /// <param name="estadoAnterior">Estado guardado de la misión (null si es nueva).</param>
        public static List<string> ValidarMision(string titulo, string descripcion, string? categoria,
            string textoXp, string? tipo, string? estado, DateOnly? fechaLimite,
            string? estadoAnterior, DateOnly hoy, out int experiencia)
        {
            var errores = new List<string>();
            experiencia = 0;
            bool esNueva = estadoAnterior == null;

            if (string.IsNullOrWhiteSpace(titulo))
                errores.Add("El título es obligatorio.");
            else if (titulo.Trim().Length > MaxTitulo)
                errores.Add($"El título no puede superar {MaxTitulo} caracteres.");

            if ((descripcion ?? "").Trim().Length > MaxDescripcionMision)
                errores.Add($"La descripción no puede superar {MaxDescripcionMision} caracteres.");

            if (string.IsNullOrWhiteSpace(categoria) || !Mision.Categorias.Contains(categoria))
                errores.Add("Elige una categoría.");

            if (!int.TryParse(textoXp?.Trim(), out experiencia))
                errores.Add("La experiencia debe ser un número entero.");
            else if (experiencia < MinXp || experiencia > MaxXp)
                errores.Add($"La experiencia debe estar entre {MinXp} y {MaxXp}.");

            if (string.IsNullOrWhiteSpace(tipo) || !Mision.Tipos.Contains(tipo))
                errores.Add("Elige el tipo de misión (Diaria o Única).");

            if (string.IsNullOrWhiteSpace(estado) || !Mision.Estados.Contains(estado))
                errores.Add("Elige un estado.");
            else if (estado == Mision.EstadoCompletada && estadoAnterior != Mision.EstadoCompletada)
                errores.Add("Para completar una misión usa el botón «Completar misión»: así se suma la experiencia.");
            else if (estado == Mision.EstadoCompletada && tipo == Mision.TipoDiaria)
                errores.Add("Una misión diaria no puede quedar en estado Completada: se completa cada día.");

            if (esNueva && fechaLimite.HasValue && fechaLimite.Value < hoy)
                errores.Add("La fecha límite no puede ser anterior a hoy.");

            return errores;
        }

        public static List<string> ValidarActividad(string descripcion, Mision? mision,
            IEnumerable<string> idsMisionesExistentes, DateOnly? fecha, string textoMinutos,
            DateOnly hoy, out int minutos)
        {
            var errores = new List<string>();
            minutos = 0;

            if (string.IsNullOrWhiteSpace(descripcion))
                errores.Add("La descripción es obligatoria.");
            else if (descripcion.Trim().Length > MaxDescripcionActividad)
                errores.Add($"La descripción no puede superar {MaxDescripcionActividad} caracteres.");

            if (mision == null)
                errores.Add("Elige la misión relacionada.");
            else if (!idsMisionesExistentes.Contains(mision.Id))
                errores.Add("La misión relacionada ya no existe. Pulsa Actualizar.");

            if (!fecha.HasValue)
                errores.Add("Elige la fecha.");
            else if (fecha.Value > hoy)
                errores.Add("No puedes registrar actividad en una fecha futura.");

            if (!int.TryParse(textoMinutos?.Trim(), out minutos))
                errores.Add("El tiempo dedicado debe ser un número entero de minutos.");
            else if (minutos < 1 || minutos > MaxMinutos)
                errores.Add($"El tiempo dedicado debe estar entre 1 y {MaxMinutos} minutos.");

            return errores;
        }

        public static List<string> ValidarNombre(string nombre)
        {
            var errores = new List<string>();
            if (string.IsNullOrWhiteSpace(nombre))
                errores.Add("El nombre es obligatorio.");
            else if (nombre.Trim().Length > MaxNombre)
                errores.Add($"El nombre no puede superar {MaxNombre} caracteres.");
            return errores;
        }
    }
}
