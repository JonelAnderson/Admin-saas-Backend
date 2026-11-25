namespace DS.Platform.Application.DTOs.Identities.Response
{
    public class UserResponse
    {
        public string? Id { get; set; }
        public string? UserName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string ChargeName { get; set; } = string.Empty;
        public string ReportUser { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string? UserStatus { get; set; }
        public bool EmailConfirmed { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ProfilePictureDataUrl { get; set; }

        /// <summary>Autenticación en dos pasos activada.</summary>
        public bool MfaEnabled { get; set; }

        /// <summary>Fin del bloqueo por intentos fallidos (UTC), o null si no está bloqueado.</summary>
        public System.DateTime? LockedUntil { get; set; }
    }
}