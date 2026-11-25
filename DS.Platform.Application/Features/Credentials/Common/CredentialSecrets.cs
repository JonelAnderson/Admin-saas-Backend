using DS.Platform.Domain.Entities.Ops;

namespace DS.Platform.Application.Features.Credentials.Common
{
    /// <summary>El único lugar que borra el secreto de una entrega de credenciales: al confirmar el ERP, al vencer y al cancelar.</summary>
    public static class CredentialSecrets
    {
        public static void Wipe(ApiCredentialDelivery delivery)
        {
            delivery.ApiKeyProtected = null;
            delivery.ApiSecretProtected = null;
        }
    }
}
