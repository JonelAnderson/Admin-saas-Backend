namespace DS.Platform.Shared.Utilities
{
    /// <summary>
    /// ERP-wide UUID v7 identifiers (RFC 9562). Do not use Guid.NewGuid() for persisted uuid columns.
    /// </summary>
    public static class ErpGuid
    {
        public static Guid NewId() => Guid.CreateVersion7();

        public static bool IsVersion7(Guid id) => id.Version == 7;
    }
}
