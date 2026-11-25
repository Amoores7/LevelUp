namespace LevelUp.Services
{
    public enum TipoErrorFirebase
    {
        /// <summary>No hay conexión o Firebase no responde.</summary>
        Conexion,
        /// <summary>Correo o contraseña incorrectos, cuenta deshabilitada…</summary>
        Credenciales,
        /// <summary>El token ha caducado y no se ha podido renovar: hay que volver a iniciar sesión.</summary>
        SesionCaducada,
        /// <summary>Las reglas de seguridad han rechazado la operación (Permission denied).</summary>
        AccesoDenegado,
        /// <summary>Falta o es incorrecto firebase.config.json.</summary>
        Configuracion,
        /// <summary>Cualquier otra respuesta inesperada.</summary>
        Otro
    }

    /// <summary>Error de Firebase con un mensaje pensado para mostrarlo al usuario.</summary>
    public class FirebaseException : Exception
    {
        public TipoErrorFirebase Tipo { get; }

        public FirebaseException(string mensaje, TipoErrorFirebase tipo, Exception? interna = null)
            : base(mensaje, interna)
        {
            Tipo = tipo;
        }
    }
}
