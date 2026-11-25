using LevelUp.Services;

namespace LevelUp.Tests.Fakes
{
    /// <summary>Proveedor de tokens de prueba: no llama a Firebase Authentication.</summary>
    public class FakeProveedorToken : IProveedorToken
    {
        public const string TokenInicial = "token-1";
        public const string TokenRenovado = "token-2";

        /// <summary>Veces que se ha pedido un token nuevo.</summary>
        public int Renovaciones { get; private set; }

        public Task<string> ObtenerTokenAsync(bool forzarRenovacion = false)
        {
            if (forzarRenovacion)
                Renovaciones++;
            return Task.FromResult(Renovaciones > 0 ? TokenRenovado : TokenInicial);
        }
    }
}
