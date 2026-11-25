using System.Linq.Expressions;
using DS.Platform.Application.DTOs.Instances.Request;
using DS.Platform.Application.DTOs.Instances.Response;
using DS.Platform.Domain.Entities.Erp;

namespace DS.Platform.Application.Mappings.Instances
{
    /// <summary>
    /// Mapeo explícito de instancias del ERP (sin AutoMapper). El estado de licencia y de contacto no están en la tabla: los agrega el
    /// handler. La clave, el hash, el cliente y la telemetría nunca se copian de un formulario.
    /// </summary>
    public static class InstanceMappings
    {
        public static readonly Expression<Func<ErpInstance, InstanceResponseDto>> ToResponse = i => new InstanceResponseDto
        {
            Id = i.Id,
            CustomerId = i.CustomerId,
            CustomerRuc = i.Customer != null ? i.Customer.Ruc : string.Empty,
            CustomerName = i.Customer != null ? i.Customer.BusinessName : string.Empty,
            Name = i.Name,
            Url = i.Url,
            Server = i.Server,
            Notes = i.Notes,
            LicenseKeyHint = i.LicenseKeyHint,
            KeyRotatedOnUtc = i.KeyRotatedOnUtc,
            LastContactOnUtc = i.LastContactOnUtc,
            Version = i.Version,
            LastMigration = i.LastMigration,
            Companies = i.Companies,
            Users = i.Users,
            Branches = i.Branches,
            DocumentsThisMonth = i.DocumentsThisMonth,
            IsActive = i.IsActive,
            IsProvisioned = i.CoolifyServiceUuid != null,
            DeployedVersion = i.DeployedVersion
        };

        public static ErpInstance ToEntity(this CreateInstanceRequestDto dto, string keyHash, string keyHint, DateTime nowUtc)
        {
            var instance = new ErpInstance
            {
                CustomerId = dto.CustomerId,
                Name = dto.Name.Trim(),
                LicenseKeyHash = keyHash,
                LicenseKeyHint = keyHint,
                KeyRotatedOnUtc = nowUtc,
                IsActive = true
            };
            instance.Url = Clean(dto.Url);
            instance.Server = Clean(dto.Server);
            instance.Notes = Clean(dto.Notes);
            return instance;
        }

        /// <summary>Copia los datos editables del formulario (no toca cliente, clave, telemetría ni auditoría).</summary>
        public static void ApplyTo(this UpdateInstanceRequestDto dto, ErpInstance instance)
        {
            instance.Name = dto.Name.Trim();
            instance.Url = Clean(dto.Url);
            instance.Server = Clean(dto.Server);
            instance.Notes = Clean(dto.Notes);
        }

        /// <summary>Copia lo que informó la instancia en su consulta (texto acotado: viene de afuera).</summary>
        public static void ApplyTo(this LicenseCheckRequestDto report, ErpInstance instance, DateTime nowUtc)
        {
            instance.LastContactOnUtc = nowUtc;
            instance.Version = Limit(report.Version, 40);
            instance.LastMigration = Limit(report.LastMigration, 150);
            instance.Companies = NonNegative(report.Companies);
            instance.Users = NonNegative(report.Users);
            instance.Branches = NonNegative(report.Branches);
            instance.DocumentsThisMonth = NonNegative(report.DocumentsThisMonth);
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? Limit(string? value, int max)
        {
            var clean = Clean(value);
            return clean is null ? null : clean.Length > max ? clean[..max] : clean;
        }

        private static int? NonNegative(int? value) => value is >= 0 ? value : null;
    }
}
