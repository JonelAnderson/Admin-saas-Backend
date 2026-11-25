namespace DS.Platform.Domain.Enums
{
    /// <summary>Acción que la consola pide a un sistema externo (bandeja <c>ops.RemoteActions</c>, diseño §5.1).</summary>
    public enum RemoteActionType
    {
        /// <summary>Suspender la emisión de una empresa emisora de la API SUNAT por falta de pago (la API rechaza emisiones nuevas con 402).</summary>
        SuspendIssuer = 1,

        /// <summary>Volver a permitir la emisión de una empresa emisora.</summary>
        ReactivateIssuer = 2,

        /// <summary>Aplicar a la empresa el plan de la API SUNAT que corresponde a las suscripciones de su cliente.</summary>
        ApplyPlan = 3
    }

    /// <summary>
    /// Estado de una acción remota: Pendiente → Enviando (la tomó el despachador) → Hecha. Si falla queda Fallida con la siguiente
    /// hora de reintento; tras 10 intentos o un error que no se arregla reintentando queda Abandonada (se reintenta a mano).
    /// Cancelada: otra acción posterior sobre el mismo emisor la dejó sin efecto.
    /// </summary>
    public enum RemoteActionStatus
    {
        Pending = 1,
        Sending = 2,
        Done = 3,
        Failed = 4,
        Abandoned = 5,
        Cancelled = 6
    }
}
