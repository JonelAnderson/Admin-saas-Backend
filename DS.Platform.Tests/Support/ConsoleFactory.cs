using DS.Platform.Application.Interfaces.Services;

namespace DS.Platform.Tests.Support
{
    /// <summary>Reloj que la prueba mueve (para vencimientos, gracia y suspensión). Arranca a mediodía de Lima.</summary>
    public sealed class TestClock : IDateTimeService
    {
        public TestClock(DateOnly limaToday) => SetLimaDate(limaToday);

        public DateTime NowUtc { get; private set; }

        /// <summary>Adelanta el reloj (reintentos con espera de minutos u horas).</summary>
        public void Advance(TimeSpan span) => NowUtc += span;

        /// <summary>Mediodía de Lima del día indicado (17:00 UTC).</summary>
        public void SetLimaDate(DateOnly limaDate)
            => NowUtc = DateTime.SpecifyKind(limaDate.ToDateTime(new TimeOnly(17, 0)), DateTimeKind.Utc);
    }

    public static class TestRuc
    {
        private static readonly int[] Weights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

        /// <summary>RUC válido a partir de sus 10 primeros dígitos (agrega el dígito verificador módulo 11).</summary>
        public static string From(string firstTen)
        {
            var sum = 0;
            for (var i = 0; i < 10; i++)
            {
                sum += (firstTen[i] - '0') * Weights[i];
            }
            var check = 11 - sum % 11;
            check = check switch { 10 => 0, 11 => 1, _ => check };
            return firstTen + check;
        }
    }
}
