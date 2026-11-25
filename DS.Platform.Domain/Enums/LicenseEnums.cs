namespace DS.Platform.Domain.Enums
{
    /// <summary>
    /// Estado de la licencia que recibe una instancia del ERP (se deriva de la suscripción del ERP del cliente). Con Suspended o
    /// Cancelled el ERP queda en solo lectura: se consulta todo, no se registra ni modifica nada.
    /// </summary>
    public enum LicenseStatus
    {
        Active = 1,
        Grace = 2,
        Suspended = 3,
        Cancelled = 4
    }
}
