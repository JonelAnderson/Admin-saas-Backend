namespace DS.Platform.Application.Interfaces.Services.ApiSunat
{
    /// <summary>
    /// Resultado de una llamada a la API SUNAT. <see cref="Retryable"/> dice si repetirla más tarde puede arreglarla (la API caída,
    /// un tiempo de espera, 429 o 5xx) o no (la empresa no existe, la clave se rechazó, la petición es inválida).
    /// </summary>
    public sealed record ApiSunatCallResult<T>(bool Success, int StatusCode, string? Error, T? Data, bool Retryable)
    {
        public static ApiSunatCallResult<T> Ok(T data, int statusCode = 200) => new(true, statusCode, null, data, false);

        public static ApiSunatCallResult<T> Fail(int statusCode, string error, bool retryable) => new(false, statusCode, error, default, retryable);
    }

    /// <summary>Empresa de la API SUNAT tal como la devuelve <c>/api/admin/tenants</c> (nunca trae secretos).</summary>
    public sealed record ApiTenantInfo(
        long Id,
        string Ruc,
        string BusinessName,
        string? TradeName,
        string? Environment,
        string? Plan,
        bool IsActive,
        bool BillingSuspended,
        int DocumentsThisMonth,
        int MaxDocumentsMonth);

    public sealed record ApiTenantPage(IReadOnlyList<ApiTenantInfo> Items, long Total);

    /// <summary>Certificado digital de una empresa (<c>/api/admin/certificates</c>): estado OK / POR_VENCER / VENCIDO / ROTO / SIN_CERTIFICADO.</summary>
    public sealed record ApiCertificateInfo(long TenantId, string Ruc, string BusinessName, string? Environment, string Status, DateOnly? ExpiresOn, long? DaysLeft);

    /// <summary>Resumen global de la API (<c>/api/admin/dashboard</c>) para el tablero de la consola.</summary>
    public sealed record ApiDashboardInfo(
        long TenantsActive, long DocumentsThisMonth, long RejectedThisMonth, long PendingNow, long FailedJobs,
        long CertificatesExpiring, long CertificatesBroken);

    /// <summary>
    /// Invitación al portal de clientes que devuelve la API (fase C6): el id del usuario y el token de activación **en claro, una sola
    /// vez**. Es una clase y no un record para que ni <c>ToString()</c> ni un log del resultado muestren el token.
    /// </summary>
    public sealed class ApiPortalInvitation(long userId, string token, DateTime expiresAtUtc)
    {
        public long UserId { get; } = userId;
        public string Token { get; } = token;
        public DateTime ExpiresAtUtc { get; } = expiresAtUtc;

        public override string ToString() => $"ApiPortalInvitation(UserId={UserId}, ExpiresAtUtc={ExpiresAtUtc:O})";
    }

    /// <summary>
    /// Credenciales nuevas de una empresa emisora (<c>POST api/admin/tenants/{id}/credenciales/regenerar</c>, fase C6b): la API key y el
    /// secret **en claro, una sola vez**; las anteriores ya no sirven. Es una clase y no un record para que ni <c>ToString()</c> ni un
    /// log del resultado muestren el secreto.
    /// </summary>
    public sealed class ApiTenantCredentials(string apiKey, string apiSecret)
    {
        public string ApiKey { get; } = apiKey;
        public string ApiSecret { get; } = apiSecret;

        public override string ToString() => "ApiTenantCredentials(***)";
    }

    /// <summary>
    /// Cliente HTTP de <c>/api/admin/**</c> de la API SUNAT (diseño §5.3). Se usa solo por esta interfaz: los handlers nunca saben
    /// de HTTP. Las acciones que cambian estado no se llaman desde un caso de uso: pasan por la bandeja de acciones remotas.
    /// Sin red ni clave configurada, los métodos devuelven un error reintentable (nunca lanzan).
    /// </summary>
    public interface IApiSunatAdminClient
    {
        Task<ApiSunatCallResult<ApiTenantInfo>> GetTenantAsync(long tenantId, CancellationToken cancellationToken);

        /// <summary>Busca empresas por RUC, razón social o nombre comercial (una página; <paramref name="page"/> desde 1).</summary>
        Task<ApiSunatCallResult<ApiTenantPage>> SearchTenantsAsync(string? text, int page, int pageSize, CancellationToken cancellationToken);

        /// <summary>Certificados con ese estado que vencen en los próximos <paramref name="days"/> días (recorre las páginas, con tope).</summary>
        Task<ApiSunatCallResult<IReadOnlyList<ApiCertificateInfo>>> GetCertificatesAsync(string status, int days, CancellationToken cancellationToken);

        Task<ApiSunatCallResult<ApiDashboardInfo>> GetDashboardAsync(CancellationToken cancellationToken);

        /// <summary>Cuántos comprobantes rechazados u observados por SUNAT hubo ese día (Lima).</summary>
        Task<ApiSunatCallResult<long>> CountRejectionsAsync(DateOnly day, CancellationToken cancellationToken);

        /// <summary>
        /// Suspende por falta de pago (la empresa sigue activa: consulta y descarga sí, emisión nueva no, 402) o la reactiva.
        /// Es idempotente en la API. <paramref name="actor"/> viaja como <c>X-Admin-Actor</c>.
        /// </summary>
        Task<ApiSunatCallResult<ApiTenantInfo>> SetBillingSuspensionAsync(
            long tenantId, bool suspended, string reason, string actor, CancellationToken cancellationToken);

        /// <summary>
        /// Aplica el plan (<c>free</c> / <c>pro</c> / <c>business</c>) y el cupo mensual de comprobantes (null = el del plan) de la empresa.
        /// Es idempotente en la API. Devuelve la empresa ya actualizada.
        /// </summary>
        Task<ApiSunatCallResult<ApiTenantInfo>> SetPlanAsync(
            long tenantId, string plan, int? maxDocumentsMonth, string reason, string actor, CancellationToken cancellationToken);

        /// <summary>
        /// Crea (o reactiva) al usuario del portal de clientes con ese correo y le da acceso a esas empresas (fase C6). Devuelve su id y
        /// el token de activación, que solo viaja en esta respuesta. <paramref name="resetMfa"/> borra también su MFA.
        /// </summary>
        Task<ApiSunatCallResult<ApiPortalInvitation>> CreatePortalInvitationAsync(
            string email, string name, IReadOnlyCollection<long> tenantIds, bool resetMfa, string actor, CancellationToken cancellationToken);

        /// <summary>Habilita o deshabilita al usuario del portal (idempotente; deshabilitar cierra sus sesiones).</summary>
        Task<ApiSunatCallResult<bool>> SetPortalUserActiveAsync(long userId, bool active, string actor, CancellationToken cancellationToken);

        /// <summary>
        /// Regenera la API key y el secret del emisor (fase C6b). **Invalida las anteriores.** Devuelve las nuevas en claro, una sola
        /// vez: quien llama las guarda cifradas en el acto. <paramref name="reason"/> es el motivo que exige la API.
        /// </summary>
        Task<ApiSunatCallResult<ApiTenantCredentials>> RegenerateTenantCredentialsAsync(
            long tenantId, string reason, string actor, CancellationToken cancellationToken);
    }
}
