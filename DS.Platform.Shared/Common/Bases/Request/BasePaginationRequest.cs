namespace DS.Platform.Shared.Commons.Bases.Request
{
    /// <summary>
    /// Page request of every list. The page size is always 1..50, whichever name the client sends (records or numRecordsPage):
    /// 0, negative or missing → 10; above 50 → 50. A page below 1 → 1. The only way to get every row is Download = true (exports).
    /// </summary>
    public class BasePaginationRequest
    {
        public const int MaxRecordsPage = 50;
        public const int DefaultRecordsPage = 10;

        private int _numPage = 1;
        private int _numRecordsPage = DefaultRecordsPage;

        public int NumPage
        {
            get => _numPage;
            set => _numPage = value < 1 ? 1 : value;
        }

        public int NumRecordsPage
        {
            get => _numRecordsPage;
            set => _numRecordsPage = value < 1 ? DefaultRecordsPage : Math.Min(value, MaxRecordsPage);
        }

        /// <summary>Same as <see cref="NumRecordsPage"/> (the name the frontend sends).</summary>
        public int Records
        {
            get => NumRecordsPage;
            set => NumRecordsPage = value;
        }
    }
}
