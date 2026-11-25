namespace DS.Platform.Domain.Enums
{
    /// <summary>Medio de pago de un cliente.</summary>
    public enum PaymentMethod
    {
        Transfer = 1,
        Deposit = 2,
        Yape = 3,
        Plin = 4,
        Cash = 5,
        Card = 6,
        Other = 9
    }

    /// <summary>Un pago nunca se edita: se registra o se anula (con motivo).</summary>
    public enum PaymentStatus
    {
        Registered = 1,
        Voided = 2
    }
}
