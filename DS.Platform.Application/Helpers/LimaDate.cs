namespace DS.Platform.Application.Helpers
{
    /// <summary>Fecha de negocio de Lima (UTC−5, sin horario de verano): vencimientos, pruebas y suspensiones.</summary>
    public static class LimaDate
    {
        public static readonly TimeSpan Offset = TimeSpan.FromHours(-5);

        public static DateOnly Today(DateTime nowUtc) => DateOnly.FromDateTime(nowUtc.Add(Offset));
    }
}
