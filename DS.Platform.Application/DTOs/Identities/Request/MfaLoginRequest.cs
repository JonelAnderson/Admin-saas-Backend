namespace DS.Platform.Application.DTOs.Identities.Request
{
    /// <summary>Segundo paso del login (o confirmación del enrolamiento): el ticket y uno de los dos códigos.</summary>
    public class MfaLoginRequest
    {
        public string? MfaTicket { get; set; }

        /// <summary>Código de 6 dígitos de la app autenticadora.</summary>
        public string? Code { get; set; }

        /// <summary>Código de recuperación de un solo uso (xxxxx-xxxxx).</summary>
        public string? RecoveryCode { get; set; }
        /// <summary>Recordar este dispositivo: el próximo login desde él no pide el código (salvo 7 días sin entrar).</summary>
        public bool RememberDevice { get; set; }
        /// <summary>Navegador / sistema, para reconocer el dispositivo.</summary>
        public string? DeviceLabel { get; set; }
    }
}
