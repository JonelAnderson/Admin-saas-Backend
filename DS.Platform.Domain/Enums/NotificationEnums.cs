namespace DS.Platform.Domain.Enums
{
    /// <summary>Aviso por correo al cliente (diseño §5.4).</summary>
    public enum NotificationType
    {
        /// <summary>Faltan 5 días o menos para que venza lo cubierto (o la prueba).</summary>
        DueSoon = 1,
        /// <summary>Hoy es el último día cubierto.</summary>
        DueToday = 2,
        /// <summary>Venció y entró en gracia: todavía opera, con aviso.</summary>
        GraceEntered = 3,
        /// <summary>Se suspendió por falta de pago.</summary>
        Suspended = 4,
        /// <summary>Se registró un pago: lleva la constancia en el cuerpo.</summary>
        PaymentRegistered = 5,
        /// <summary>El certificado digital de una empresa emisora de la API SUNAT está por vencer o venció.</summary>
        CertificateExpiring = 6,
        /// <summary>Invitación al portal de clientes de la API SUNAT: el cuerpo lleva el enlace con el token y se borra al enviarse.</summary>
        PortalInvitation = 7
    }

    /// <summary>Pendiente → Enviando → Enviado. Fallida se reintenta; Abandonada y Omitida no.</summary>
    public enum NotificationStatus
    {
        Pending = 1,
        Sending = 2,
        Sent = 3,
        Failed = 4,
        Abandoned = 5,
        /// <summary>No se envió a propósito: el cliente no tiene correo o el aviso caducó sin poder enviarse.</summary>
        Skipped = 6
    }
}
