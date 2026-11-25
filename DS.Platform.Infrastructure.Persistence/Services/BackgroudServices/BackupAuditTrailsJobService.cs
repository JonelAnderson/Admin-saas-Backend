using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DS.Platform.Infrastructure.Persistence.Services.BackgroudServices
{
    /// <summary>
    /// Monthly maintenance (PostgreSQL): moves every row of "AuditTrails" into "AuditTrails_dd_MM_yyyy" (Lima date).
    /// The move is a single DELETE ... RETURNING / INSERT statement, so rows written meanwhile are never lost or copied twice.
    /// </summary>
    public class BackupAuditTrailsJobService
    {
        private readonly IDateTimeService _dateTimeService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackupAuditTrailsJobService> _logger;

        public BackupAuditTrailsJobService(IServiceProvider serviceProvider, ILogger<BackupAuditTrailsJobService> logger, IDateTimeService dateTimeService)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _dateTimeService = dateTimeService;
        }

        /// <summary>Backup table name for a UTC instant (Lima is UTC-5, no DST). Digits and underscores only: safe to inline as an identifier.</summary>
        public static string BackupTableName(DateTime nowUtc) => $"AuditTrails_{nowUtc.AddHours(-5):dd_MM_yyyy}";

        public static string CreateTableSql(string tableName)
            => $"CREATE TABLE IF NOT EXISTS \"{tableName}\" (LIKE \"AuditTrails\" INCLUDING DEFAULTS INCLUDING CONSTRAINTS INCLUDING INDEXES)";

        public static string MoveRowsSql(string tableName)
            => $"WITH moved AS (DELETE FROM \"AuditTrails\" RETURNING *) INSERT INTO \"{tableName}\" SELECT * FROM moved";

        public async Task<bool> BackupAuditTrailsJob(CancellationToken stoppingToken)
        {
            var tableName = BackupTableName(_dateTimeService.NowUtc);
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                _logger.LogInformation("Iniciando backup de AuditTrails hacia la tabla {Table}", tableName);

                await using var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);
                await dbContext.Database.ExecuteSqlRawAsync(CreateTableSql(tableName), stoppingToken);
                var moved = await dbContext.Database.ExecuteSqlRawAsync(MoveRowsSql(tableName), stoppingToken);
                await transaction.CommitAsync(stoppingToken);

                _logger.LogInformation("Backup de AuditTrails terminado: {Rows} filas movidas a {Table}", moved, tableName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el backup de AuditTrails hacia {Table}", tableName);
                return false;
            }
        }
    }
}
