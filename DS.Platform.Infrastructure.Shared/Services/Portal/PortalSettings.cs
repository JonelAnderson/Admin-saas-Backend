using System;
using DS.Platform.Application.Interfaces.Services.Portal;
using Microsoft.Extensions.Options;

namespace DS.Platform.Infrastructure.Shared.Services.Portal
{
    /// <summary>Sección <c>Portal</c> (nada secreto): dónde vive el portal de clientes de la API SUNAT.</summary>
    public class PortalOptions
    {
        public const string SectionName = "Portal";

        public string? PublicUrl { get; set; }
    }

    public class PortalSettings(IOptions<PortalOptions> options) : IPortalSettings
    {
        private readonly PortalOptions _options = options.Value;

        public string? PublicUrl => _options.PublicUrl;

        public bool IsConfigured => Uri.TryCreate(PublicUrl, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps;
    }
}
