using System;
using System.Collections.Generic;

namespace DS.Platform.Application.DTOs.Identities.Response
{
    public class TokenResponse
    {
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }
        public string? UserImageURL { get; set; }
        public DateTime RefreshTokenExpiryTime { get; set; }

        /// <summary>Contraseña correcta y MFA activo: falta <c>POST login/mfa</c> con <see cref="MfaTicket"/>.</summary>
        public bool MfaRequired { get; set; }

        /// <summary>Contraseña correcta sin MFA: falta enrolarse (<c>login/mfa/setup</c> y <c>login/mfa/setup/confirm</c>).</summary>
        public bool MfaSetupRequired { get; set; }

        /// <summary>Dato opaco (Data Protection, 5 minutos). No es un JWT.</summary>
        public string? MfaTicket { get; set; }

        public int? RecoveryCodesLeft { get; set; }

        /// <summary>Solo al confirmar el enrolamiento: los 10 códigos de recuperación, que no se vuelven a mostrar.</summary>
        public List<string>? RecoveryCodes { get; set; }
        /// <summary>Al recordar el dispositivo: su token (el frontend lo guarda y lo manda en el login).</summary>
        public string? DeviceToken { get; set; }
        /// <summary>El login entró sin código porque el dispositivo está recordado.</summary>
        public bool DeviceTrusted { get; set; }
    }
}
