namespace DS.Platform.Application.Features.RemoteActions.Common
{
    /// <summary>
    /// Reintentos de la bandeja (diseño §5.1): espera creciente de 1 min a 6 h y 10 intentos. Pasado el último queda Abandonada y
    /// aparece en el tablero con su botón "Reintentar". Es pura: recibe el número de intentos ya hechos.
    /// </summary>
    public static class RemoteActionRetryPolicy
    {
        public const int MaxAttempts = 10;

        /// <summary>Cuánto bloquea el despachador una acción que toma (si el proceso muere, otro la retoma pasado este tiempo).</summary>
        public static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

        private static readonly TimeSpan[] Delays =
        [
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(4), TimeSpan.FromHours(6), TimeSpan.FromHours(6)
        ];

        /// <summary>Espera antes del siguiente intento tras <paramref name="attemptsDone"/> intentos fallidos (1 = el primero falló).</summary>
        public static TimeSpan DelayAfter(int attemptsDone)
            => Delays[Math.Clamp(attemptsDone - 1, 0, Delays.Length - 1)];

        public static bool IsExhausted(int attemptsDone) => attemptsDone >= MaxAttempts;
    }
}
