using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.Features.Credentials.Common
{
    /// <summary>Reglas puras de la entrega de credenciales de la API SUNAT al ERP (fase C6b).</summary>
    public static class CredentialDeliveryRules
    {
        /// <summary>Cuánto espera la consola a que el ERP recoja unas credenciales ya generadas; pasado el plazo se borran.</summary>
        public static readonly TimeSpan ReadyValidity = TimeSpan.FromDays(7);

        /// <summary>Máximo de entregas que recoge el ERP en una consulta.</summary>
        public const int MaxPerPull = 20;

        /// <summary>Máximo de ids que confirma el ERP en una llamada.</summary>
        public const int MaxPerAck = 50;

        public const int RucLength = 11;

        public const string NotConfiguredMessage =
            "La API SUNAT no tiene dirección pública https (ApiSunat:PublicUrl): no se pueden entregar credenciales al ERP.";

        public const string ConfirmMessage =
            "Confirme que entiende que las credenciales actuales de la empresa dejan de funcionar al generar las nuevas.";

        public const string ReasonFormat = "Entrega de credenciales al ERP (instancia {0}) desde la consola de plataforma";

        /// <summary>Motivo que exige la API al regenerar (máximo 400 caracteres); lleva el id de la instancia y nada secreto.</summary>
        public static string ReasonFor(int instanceId) => string.Format(ReasonFormat, instanceId);

        /// <summary>Estados en los que la entrega sigue abierta: solo puede haber una por emisor.</summary>
        public static bool IsOpen(ApiCredentialDeliveryStatus status)
            => status is ApiCredentialDeliveryStatus.Pending or ApiCredentialDeliveryStatus.Sending
                or ApiCredentialDeliveryStatus.Ready or ApiCredentialDeliveryStatus.Failed;

        /// <summary>El RUC de la entrega: solo los 11 dígitos (así lo compara el ERP con el de su empresa).</summary>
        public static bool IsValidRuc(string? ruc) => ruc is { Length: RucLength } && ruc.All(char.IsAsciiDigit);

        public static string StatusName(ApiCredentialDeliveryStatus status) => status switch
        {
            ApiCredentialDeliveryStatus.Pending => "Pendiente",
            ApiCredentialDeliveryStatus.Sending => "En curso",
            ApiCredentialDeliveryStatus.Ready => "Lista (esperando al ERP)",
            ApiCredentialDeliveryStatus.Delivered => "Entregada",
            ApiCredentialDeliveryStatus.Failed => "Fallida (se reintenta)",
            ApiCredentialDeliveryStatus.Abandoned => "Abandonada",
            ApiCredentialDeliveryStatus.Cancelled => "Cancelada",
            ApiCredentialDeliveryStatus.Expired => "Vencida",
            _ => status.ToString()
        };
    }
}
