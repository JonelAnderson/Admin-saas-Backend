namespace DS.Platform.Application.Interfaces.Services.ApiSunat
{
    /// <summary>
    /// Lo no secreto de la sección <c>ApiSunat</c> que necesitan los casos de uso (fase C6b): la dirección pública (https) de la API SUNAT que
    /// la consola entrega al ERP junto con las credenciales de su empresa. Sin ella no se entrega nada.
    /// </summary>
    public interface IApiSunatSettings
    {
        /// <summary>Dirección pública (https) de la API SUNAT; null o vacía si no está configurada.</summary>
        string? PublicUrl { get; }

        bool IsPublicUrlConfigured { get; }
    }
}
