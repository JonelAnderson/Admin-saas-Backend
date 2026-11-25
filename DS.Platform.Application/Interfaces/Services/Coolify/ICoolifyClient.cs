namespace DS.Platform.Application.Interfaces.Services.Coolify
{
    /// <summary>
    /// Resultado de una llamada a Coolify. <see cref="Retryable"/> dice si repetirla más tarde puede arreglarla (Coolify caído, tiempo
    /// de espera, 429, 5xx, token todavía sin configurar) o no (proyecto inexistente, petición inválida, dominio en conflicto).
    /// </summary>
    public sealed record CoolifyCallResult<T>(bool Success, int StatusCode, string? Error, T? Data, bool Retryable)
    {
        public static CoolifyCallResult<T> Ok(T data, int statusCode = 200) => new(true, statusCode, null, data, false);

        public static CoolifyCallResult<T> Fail(int statusCode, string error, bool retryable) => new(false, statusCode, error, default, retryable);
    }

    /// <summary>Servicio (stack Docker Compose) que se crea en Coolify. <see cref="ComposeYaml"/> viaja en Base64 (lo hace el cliente).</summary>
    public sealed record CoolifyServiceSpec(string Name, string Description, string ComposeYaml, string ServiceName, string PublicUrl, int ServicePort);

    /// <summary>
    /// Cliente de la API de Coolify (<c>/api/v1</c>) para montar y actualizar el stack del ERP de cada cliente (fase C5). Se usa solo por
    /// esta interfaz y solo desde el despachador de despliegues: ningún caso de uso la llama directamente. Nunca lanza: sin red o sin
    /// token devuelve un error reintentable.
    /// </summary>
    public interface ICoolifyClient
    {
        /// <summary>Crea el servicio sin arrancarlo y devuelve su uuid.</summary>
        Task<CoolifyCallResult<string>> CreateServiceAsync(CoolifyServiceSpec spec, CancellationToken cancellationToken);

        /// <summary>
        /// Crea o actualiza una variable del servicio (idempotente: actualiza y, si no existe, la crea). Los valores nunca se registran en
        /// el log.
        /// </summary>
        Task<CoolifyCallResult<bool>> SetEnvAsync(string serviceUuid, string key, string value, CancellationToken cancellationToken);

        /// <summary>Arranca el servicio recién creado.</summary>
        Task<CoolifyCallResult<bool>> StartServiceAsync(string serviceUuid, CancellationToken cancellationToken);

        /// <summary>Reinicia el servicio descargando las imágenes más recientes (así toma la versión nueva).</summary>
        Task<CoolifyCallResult<bool>> RestartServiceAsync(string serviceUuid, bool pullLatest, CancellationToken cancellationToken);
    }
}
