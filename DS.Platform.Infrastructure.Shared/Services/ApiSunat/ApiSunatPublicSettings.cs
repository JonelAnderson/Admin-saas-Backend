using System;
using DS.Platform.Application.Interfaces.Services.ApiSunat;
using Microsoft.Extensions.Options;

namespace DS.Platform.Infrastructure.Shared.Services.ApiSunat
{
    /// <summary>Lo no secreto de <c>ApiSunat</c> para los casos de uso: la dirección pública (https) que se entrega al ERP.</summary>
    public class ApiSunatPublicSettings(IOptions<ApiSunatSettings> options) : IApiSunatSettings
    {
        public string? PublicUrl => string.IsNullOrWhiteSpace(options.Value.PublicUrl) ? null : options.Value.PublicUrl.Trim().TrimEnd('/');

        public bool IsPublicUrlConfigured
            => Uri.TryCreate(PublicUrl, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps
               && string.IsNullOrEmpty(url.UserInfo);
    }
}
