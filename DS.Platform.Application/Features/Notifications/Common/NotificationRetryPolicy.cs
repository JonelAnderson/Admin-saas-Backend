namespace DS.Platform.Application.Features.Notifications.Common
{
    /// <summary>
    /// Reintentos del correo: 5 intentos con espera creciente (5 min → 6 h). Un aviso de cobro que no pudo salir en 3 días ya no sirve
    /// (el cliente pudo haber pagado): caduca y queda Omitido. Pura.
    /// </summary>
    public static class NotificationRetryPolicy
    {
        public const int MaxAttempts = 5;
        public static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan Expiry = TimeSpan.FromDays(3);

        private static readonly TimeSpan[] Delays = [TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2), TimeSpan.FromHours(6)];

        public static TimeSpan DelayAfter(int attemptsDone) => Delays[Math.Clamp(attemptsDone - 1, 0, Delays.Length - 1)];

        public static bool IsExhausted(int attemptsDone) => attemptsDone >= MaxAttempts;

        public static bool IsExpired(DateTime createdOnUtc, DateTime nowUtc) => nowUtc - createdOnUtc > Expiry;
    }
}
