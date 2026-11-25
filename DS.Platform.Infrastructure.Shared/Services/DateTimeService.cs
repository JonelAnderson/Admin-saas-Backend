using DS.Platform.Application.Interfaces.Services;
using System;

namespace DS.Platform.Infrastructure.Shared.Services
{
    public class DateTimeService : IDateTimeService
    {
        public DateTime NowUtc => DateTime.UtcNow;
    }
}