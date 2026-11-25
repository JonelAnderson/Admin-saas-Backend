namespace DS.Platform.Domain.Enums
{
    /// <summary>
    /// Estado de una entrega de credenciales de la API SUNAT al ERP (fase C6b). Pendiente → En curso → Lista → Entregada. Los estados
    /// de la bandeja (Pendiente, En curso, Fallida, Abandonada, Cancelada) se comportan como los de <see cref="RemoteActionStatus"/>;
    /// Lista (la API ya generó las credenciales y esperan al ERP), Entregada y Vencida son propios de esta entrega.
    /// </summary>
    public enum ApiCredentialDeliveryStatus
    {
        /// <summary>Falta pedirle a la API SUNAT que regenere las credenciales del emisor.</summary>
        Pending = 1,

        /// <summary>El despachador la tomó (si el proceso muere, otro la retoma cuando vence su bloqueo).</summary>
        Sending = 2,

        /// <summary>La API generó las credenciales (guardadas cifradas): el ERP las recoge con <c>Pull</c> y las confirma con <c>Ack</c>.</summary>
        Ready = 3,

        /// <summary>El ERP las recogió y confirmó: el secreto ya se borró de la consola.</summary>
        Delivered = 4,

        /// <summary>La llamada a la API falló y se reintenta con espera creciente.</summary>
        Failed = 5,

        /// <summary>Sin más reintentos (error no reintentable o intentos agotados): se reintenta a mano.</summary>
        Abandoned = 6,

        /// <summary>El operador la canceló (si ya había credenciales, se borraron).</summary>
        Cancelled = 7,

        /// <summary>Estuvo lista 7 días sin que el ERP la recogiera: el secreto se borró y hay que entregar de nuevo.</summary>
        Expired = 8
    }
}
