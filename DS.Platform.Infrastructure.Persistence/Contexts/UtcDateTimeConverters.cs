using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DS.Platform.Infrastructure.Persistence.Contexts
{
    /// <summary>
    /// All DateTime columns are PostgreSQL <c>timestamp with time zone</c>, which Npgsql only accepts as UTC.
    /// Dates bound from JSON without an offset ("2026-10-26") arrive as Kind=Unspecified and made saves fail
    /// with DbUpdateException; they are stored as UTC as-is. Local values are converted; reads come back as UTC.
    /// </summary>
    public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => ToUtc(value),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
        public static DateTime ToUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    public class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
        value => value.HasValue ? UtcDateTimeConverter.ToUtc(value.Value) : value,
        value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value);
}
