using System.Globalization;
using System.Text.Json;

namespace DS.Platform.Application.Features.Instances.Common
{
    /// <summary>
    /// JSON de la licencia que se firma. Es texto: la firma cubre exactamente estos bytes y el ERP los verifica antes de leerlos,
    /// así no hay que acordar una forma canónica. Lleva el <c>nonce</c> que mandó la instancia (la respuesta de otra consulta no
    /// sirve de reemplazo) y <c>issuedAt</c>. Las fechas de negocio son de Lima (yyyy-MM-dd) y <c>issuedAt</c> es UTC.
    /// </summary>
    public static class LicensePayload
    {
        public const int Version = 1;

        private static string? Ymd(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static string Build(LicenseState state, string nonce, DateTime nowUtc)
        {
            var payload = new
            {
                v = Version,
                status = state.Status.ToString(),
                coveredUntil = Ymd(state.CoveredUntil),
                suspendOn = Ymd(state.SuspendOn),
                trialEndsOn = Ymd(state.TrialEndsOn),
                plan = new
                {
                    code = state.PlanCode,
                    maxCompanies = state.MaxCompanies,
                    maxUsers = state.MaxUsers,
                    maxBranches = state.MaxBranches,
                    ai = state.Ai
                },
                message = state.Message,
                nonce,
                issuedAt = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
            };
            return JsonSerializer.Serialize(payload, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }
    }
}
