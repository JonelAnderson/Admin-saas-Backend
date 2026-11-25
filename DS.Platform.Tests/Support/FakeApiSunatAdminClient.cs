using DS.Platform.Application.Interfaces.Services.ApiSunat;

namespace DS.Platform.Tests.Support
{
    /// <summary>
    /// API SUNAT de mentira: guarda las empresas en memoria, cuenta las llamadas que cambian estado y puede fallar a pedido
    /// (reintentable o no). Imita la regla de la API: la suspensión manual (empresa inactiva) no la pisa la consola.
    /// </summary>
    public sealed class FakeApiSunatAdminClient : IApiSunatAdminClient
    {
        public Dictionary<long, ApiTenantInfo> Tenants { get; } = new();

        /// <summary>(empresa, suspendida, motivo, actor) de cada llamada de cambio de estado.</summary>
        public List<(long TenantId, bool Suspended, string Reason, string Actor)> StateCalls { get; } = new();

        /// <summary>Si no es null, los cambios de estado fallan con este resultado.</summary>
        public ApiSunatCallResult<ApiTenantInfo>? FailStateWith { get; set; }

        /// <summary>(empresa, plan, cupo, actor) de cada llamada de cambio de plan.</summary>
        public List<(long TenantId, string Plan, int? MaxDocuments, string Actor)> PlanCalls { get; } = new();

        /// <summary>Si no es null, los cambios de plan fallan con este resultado.</summary>
        public ApiSunatCallResult<ApiTenantInfo>? FailPlanWith { get; set; }

        /// <summary>Certificados que devuelve la API (se filtran por estado y días como la real).</summary>
        public List<ApiCertificateInfo> Certificates { get; } = new();

        public ApiDashboardInfo Dashboard { get; set; } = new(2, 10, 1, 0, 0, 0, 0);

        public long RejectionsToday { get; set; }

        /// <summary>Si es true, consultar empresas falla (API caída).</summary>
        public bool Down { get; set; }

        /// <summary>(correo, nombre, empresas, restablecer MFA, actor) de cada invitación al portal.</summary>
        public List<(string Email, string Name, long[] TenantIds, bool ResetMfa, string Actor)> InvitationCalls { get; } = new();

        /// <summary>Si no es null, las invitaciones fallan con este resultado.</summary>
        public ApiSunatCallResult<ApiPortalInvitation>? FailInvitationWith { get; set; }

        /// <summary>Token que entrega la API en la siguiente invitación (cada una lleva el suyo).</summary>
        public string NextToken { get; set; } = "tok-secreto-1";

        /// <summary>(usuario, activo, actor) de cada cambio de estado de un usuario del portal.</summary>
        public List<(long UserId, bool Active, string Actor)> PortalStateCalls { get; } = new();

        public ApiSunatCallResult<bool>? FailPortalStateWith { get; set; }

        private long _nextPortalUserId = 500;
        private readonly Dictionary<string, long> _portalUsers = new();

        public Task<ApiSunatCallResult<ApiPortalInvitation>> CreatePortalInvitationAsync(
            string email, string name, IReadOnlyCollection<long> tenantIds, bool resetMfa, string actor, CancellationToken cancellationToken)
        {
            InvitationCalls.Add((email, name, tenantIds.ToArray(), resetMfa, actor));
            if (FailInvitationWith is not null)
            {
                return Task.FromResult(FailInvitationWith);
            }
            // Como la API: el mismo correo es el mismo usuario.
            if (!_portalUsers.TryGetValue(email, out var userId))
            {
                userId = _nextPortalUserId++;
                _portalUsers[email] = userId;
            }
            return Task.FromResult(ApiSunatCallResult<ApiPortalInvitation>.Ok(
                new ApiPortalInvitation(userId, NextToken, DateTime.UtcNow.AddHours(72))));
        }

        public Task<ApiSunatCallResult<bool>> SetPortalUserActiveAsync(long userId, bool active, string actor, CancellationToken cancellationToken)
        {
            PortalStateCalls.Add((userId, active, actor));
            return Task.FromResult(FailPortalStateWith ?? ApiSunatCallResult<bool>.Ok(true));
        }

        /// <summary>(empresa, motivo, actor) de cada regeneración de credenciales (nunca guarda los valores generados).</summary>
        public List<(long TenantId, string Reason, string Actor)> RegenerateCalls { get; } = new();

        /// <summary>Si no es null, regenerar credenciales falla con este resultado.</summary>
        public ApiSunatCallResult<ApiTenantCredentials>? FailRegenerateWith { get; set; }

        private int _credentialSerial;

        /// <summary>Las últimas credenciales que entregó la API (para comprobar que lo guardado coincide); cada regeneración trae otras.</summary>
        public (string ApiKey, string ApiSecret)? LastCredentials { get; private set; }

        public Task<ApiSunatCallResult<ApiTenantCredentials>> RegenerateTenantCredentialsAsync(
            long tenantId, string reason, string actor, CancellationToken cancellationToken)
        {
            RegenerateCalls.Add((tenantId, reason, actor));
            if (FailRegenerateWith is not null)
            {
                return Task.FromResult(FailRegenerateWith);
            }
            if (Tenants.Count > 0 && !Tenants.ContainsKey(tenantId))
            {
                return Task.FromResult(ApiSunatCallResult<ApiTenantCredentials>.Fail(404, "No existe la empresa.", false));
            }
            _credentialSerial++;
            LastCredentials = ($"key-{tenantId}-{_credentialSerial}", $"secret-{tenantId}-{_credentialSerial}-ultra-secreto");
            return Task.FromResult(ApiSunatCallResult<ApiTenantCredentials>.Ok(new ApiTenantCredentials(LastCredentials.Value.ApiKey, LastCredentials.Value.ApiSecret)));
        }

        public static ApiTenantInfo Tenant(long id, string ruc, string name, bool active = true, bool billingSuspended = false)
            => new(id, ruc, name, null, "beta", "pro", active, billingSuspended, 12, 1000);

        public FakeApiSunatAdminClient With(ApiTenantInfo tenant)
        {
            Tenants[tenant.Id] = tenant;
            return this;
        }

        public Task<ApiSunatCallResult<ApiTenantInfo>> GetTenantAsync(long tenantId, CancellationToken cancellationToken)
        {
            if (Down)
            {
                return Task.FromResult(ApiSunatCallResult<ApiTenantInfo>.Fail(503, "No se pudo conectar con la API SUNAT.", true));
            }
            return Task.FromResult(Tenants.TryGetValue(tenantId, out var t)
                ? ApiSunatCallResult<ApiTenantInfo>.Ok(t)
                : ApiSunatCallResult<ApiTenantInfo>.Fail(404, "No existe la empresa.", false));
        }

        public Task<ApiSunatCallResult<ApiTenantPage>> SearchTenantsAsync(string? text, int page, int pageSize, CancellationToken cancellationToken)
        {
            if (Down)
            {
                return Task.FromResult(ApiSunatCallResult<ApiTenantPage>.Fail(503, "No se pudo conectar con la API SUNAT.", true));
            }
            var rows = Tenants.Values
                .Where(t => string.IsNullOrWhiteSpace(text) || t.Ruc.Contains(text) || t.BusinessName.Contains(text, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => t.Id).ToList();
            return Task.FromResult(ApiSunatCallResult<ApiTenantPage>.Ok(new ApiTenantPage(rows.Take(pageSize).ToList(), rows.Count)));
        }

        public Task<ApiSunatCallResult<IReadOnlyList<ApiCertificateInfo>>> GetCertificatesAsync(string status, int days, CancellationToken cancellationToken)
        {
            if (Down)
            {
                return Task.FromResult(ApiSunatCallResult<IReadOnlyList<ApiCertificateInfo>>.Fail(503, "No se pudo conectar con la API SUNAT.", true));
            }
            IReadOnlyList<ApiCertificateInfo> rows = Certificates.Where(c => c.Status == status).ToList();
            return Task.FromResult(ApiSunatCallResult<IReadOnlyList<ApiCertificateInfo>>.Ok(rows));
        }

        public Task<ApiSunatCallResult<ApiDashboardInfo>> GetDashboardAsync(CancellationToken cancellationToken)
            => Task.FromResult(Down
                ? ApiSunatCallResult<ApiDashboardInfo>.Fail(503, "No se pudo conectar con la API SUNAT.", true)
                : ApiSunatCallResult<ApiDashboardInfo>.Ok(Dashboard));

        public Task<ApiSunatCallResult<long>> CountRejectionsAsync(DateOnly day, CancellationToken cancellationToken)
            => Task.FromResult(Down
                ? ApiSunatCallResult<long>.Fail(503, "No se pudo conectar con la API SUNAT.", true)
                : ApiSunatCallResult<long>.Ok(RejectionsToday));

        public Task<ApiSunatCallResult<ApiTenantInfo>> SetPlanAsync(
            long tenantId, string plan, int? maxDocumentsMonth, string reason, string actor, CancellationToken cancellationToken)
        {
            PlanCalls.Add((tenantId, plan, maxDocumentsMonth, actor));
            if (FailPlanWith is not null)
            {
                return Task.FromResult(FailPlanWith);
            }
            if (!Tenants.TryGetValue(tenantId, out var t))
            {
                return Task.FromResult(ApiSunatCallResult<ApiTenantInfo>.Fail(404, "No existe la empresa.", false));
            }
            // Sin cupo pedido, la API aplica el del plan (aquí: 1000 en pro, 5000 en business, 100 en free).
            var quota = maxDocumentsMonth ?? (plan == "business" ? 5000 : plan == "pro" ? 1000 : 100);
            var updated = t with { Plan = plan, MaxDocumentsMonth = quota };
            Tenants[tenantId] = updated;
            return Task.FromResult(ApiSunatCallResult<ApiTenantInfo>.Ok(updated));
        }

        public Task<ApiSunatCallResult<ApiTenantInfo>> SetBillingSuspensionAsync(
            long tenantId, bool suspended, string reason, string actor, CancellationToken cancellationToken)
        {
            StateCalls.Add((tenantId, suspended, reason, actor));
            if (FailStateWith is not null)
            {
                return Task.FromResult(FailStateWith);
            }
            if (!Tenants.TryGetValue(tenantId, out var t))
            {
                return Task.FromResult(ApiSunatCallResult<ApiTenantInfo>.Fail(404, "No existe la empresa.", false));
            }
            // Suspender no pisa una suspensión manual; reactivar solo quita la de pago.
            var updated = suspended ? (t.IsActive ? t with { BillingSuspended = true } : t) : t with { BillingSuspended = false };
            Tenants[tenantId] = updated;
            return Task.FromResult(ApiSunatCallResult<ApiTenantInfo>.Ok(updated));
        }
    }
}
