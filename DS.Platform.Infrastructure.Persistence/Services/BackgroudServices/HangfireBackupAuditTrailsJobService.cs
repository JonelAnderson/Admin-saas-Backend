using Hangfire;
using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace DS.Platform.Infrastructure.Persistence.Services.BackgroudServices
{
    public class HangfireBackupAuditTrailsJobService
    {
        private readonly BackupAuditTrailsJobService _backupAuditTrailsJobService;
        public HangfireBackupAuditTrailsJobService(BackupAuditTrailsJobService backupAuditTrailsJobService)
        {
            _backupAuditTrailsJobService = backupAuditTrailsJobService;
        }

        public void ScheduleBackupAuditTrailsJob()
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");


            Expression<Func<Task>> methodCall = () => _backupAuditTrailsJobService.BackupAuditTrailsJob(CancellationToken.None);


            var options = new RecurringJobOptions
            {
                TimeZone = timeZone
            };

            RecurringJob.AddOrUpdate("BackupAuditTrailsJob", methodCall, "0 4 1 * *", options);


        }
    }
}
