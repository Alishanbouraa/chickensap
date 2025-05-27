using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Data;
using PoultrySlaughterPOS.Models;
using System.Text.Json;
using System.IO; // Added missing import
using System.Globalization; // Added for culture-specific operations

namespace PoultrySlaughterPOS.Services.Repositories
{
    /// <summary>
    /// Enterprise-grade audit log repository implementation providing comprehensive audit trail management,
    /// compliance tracking, and forensic analysis capabilities for the Poultry Slaughter POS system
    /// </summary>
    public class AuditLogRepository : Repository<AuditLog>, IAuditLogRepository
    {
        public AuditLogRepository(PoultryDbContext context, ILogger<AuditLogRepository> logger)
            : base(context, logger)
        {
        }

        #region Core Audit Operations

        public async Task<AuditLog> LogOperationAsync(string tableName, string operation, string? oldValues, string? newValues, string userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var auditLog = new AuditLog
                {
                    TableName = tableName,
                    Operation = operation,
                    OldValues = oldValues,
                    NewValues = newValues,
                    UserId = userId,
                    CreatedDate = DateTime.Now
                };

                await _dbSet.AddAsync(auditLog, cancellationToken).ConfigureAwait(false);
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Audit log created for {Operation} on {TableName} by user {UserId}", operation, tableName, userId);
                return auditLog;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating audit log for {Operation} on {TableName}", operation, tableName);
                throw;
            }
        }

        public async Task<IEnumerable<AuditLog>> GetTableAuditHistoryAsync(string tableName, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Where(al => al.TableName == tableName)
                    .OrderByDescending(al => al.CreatedDate)
                    .Take(1000) // Limit for performance
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit history for table {TableName}", tableName);
                throw;
            }
        }

        public async Task<IEnumerable<AuditLog>> GetEntityAuditTrailAsync(string tableName, int entityId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Where(al => al.TableName == tableName &&
                                (al.OldValues != null && al.OldValues.Contains($"\"{entityId}\"")) ||
                                (al.NewValues != null && al.NewValues.Contains($"\"{entityId}\"")))
                    .OrderByDescending(al => al.CreatedDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit trail for {TableName} entity {EntityId}", tableName, entityId);
                throw;
            }
        }

        public async Task<IEnumerable<AuditLog>> GetUserActivityLogAsync(string userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var query = _dbSet.Where(al => al.UserId == userId);

                if (startDate.HasValue)
                    query = query.Where(al => al.CreatedDate >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(al => al.CreatedDate <= endDate.Value.Date.AddDays(1));

                return await query
                    .OrderByDescending(al => al.CreatedDate)
                    .Take(500) // Limit for performance
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user activity log for user {UserId}", userId);
                throw;
            }
        }

        #endregion

        #region Advanced Audit Analysis

        public async Task<Dictionary<string, int>> GetOperationStatisticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var endDateInclusive = endDate.Date.AddDays(1);

                var stats = await _dbSet
                    .Where(al => al.CreatedDate >= startDate && al.CreatedDate < endDateInclusive)
                    .GroupBy(al => al.Operation)
                    .Select(g => new { Operation = g.Key, Count = g.Count() })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return stats.ToDictionary(s => s.Operation, s => s.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving operation statistics from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetTableModificationFrequencyAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var endDateInclusive = endDate.Date.AddDays(1);

                var frequency = await _dbSet
                    .Where(al => al.CreatedDate >= startDate && al.CreatedDate < endDateInclusive)
                    .GroupBy(al => al.TableName)
                    .Select(g => new { TableName = g.Key, Count = g.Count() })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return frequency.ToDictionary(f => f.TableName, f => f.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving table modification frequency from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<IEnumerable<AuditLog>> GetSuspiciousActivitiesAsync(int activityThreshold, TimeSpan timeWindow, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromTime = DateTime.Now.Subtract(timeWindow);

                // Get activities that exceed the threshold within the time window
                var userActivities = await _dbSet
                    .Where(al => al.CreatedDate >= fromTime)
                    .GroupBy(al => al.UserId)
                    .Where(g => g.Count() > activityThreshold)
                    .Select(g => new { UserId = g.Key, Count = g.Count() })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var suspiciousUserIds = userActivities.Select(ua => ua.UserId).ToList();

                if (!suspiciousUserIds.Any())
                    return Enumerable.Empty<AuditLog>();

                return await _dbSet
                    .Where(al => suspiciousUserIds.Contains(al.UserId) && al.CreatedDate >= fromTime)
                    .OrderByDescending(al => al.CreatedDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving suspicious activities with threshold {Threshold} and window {TimeWindow}", activityThreshold, timeWindow);
                throw;
            }
        }

        public async Task<Dictionary<string, List<string>>> GetUserPermissionAnalysisAsync(string userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var userActivities = await _dbSet
                    .Where(al => al.UserId == userId)
                    .GroupBy(al => al.Operation)
                    .Select(g => new { Operation = g.Key, Tables = g.Select(al => al.TableName).Distinct().ToList() })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return userActivities.ToDictionary(ua => ua.Operation, ua => ua.Tables);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing user permissions for user {UserId}", userId);
                throw;
            }
        }

        #endregion

        #region Compliance and Forensic Support

        public async Task<IEnumerable<AuditLog>> GetAuditsByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var endDateInclusive = endDate.Date.AddDays(1);

                return await _dbSet
                    .Where(al => al.CreatedDate >= startDate && al.CreatedDate < endDateInclusive)
                    .OrderByDescending(al => al.CreatedDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audits by date range from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<IEnumerable<AuditLog>> GetCriticalOperationsAsync(IEnumerable<string> criticalOperations, CancellationToken cancellationToken = default)
        {
            try
            {
                var operationsList = criticalOperations.ToList();

                return await _dbSet
                    .Where(al => operationsList.Contains(al.Operation))
                    .OrderByDescending(al => al.CreatedDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving critical operations");
                throw;
            }
        }

        public async Task<bool> VerifyAuditIntegrityAsync(int auditId, CancellationToken cancellationToken = default)
        {
            try
            {
                var auditLog = await _dbSet.FindAsync(new object[] { auditId }, cancellationToken).ConfigureAwait(false);

                if (auditLog == null)
                    return false;

                // Basic integrity checks
                var isValid = !string.IsNullOrEmpty(auditLog.TableName) &&
                             !string.IsNullOrEmpty(auditLog.Operation) &&
                             auditLog.CreatedDate <= DateTime.Now &&
                             !string.IsNullOrEmpty(auditLog.UserId);

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying audit integrity for audit {AuditId}", auditId);
                throw;
            }
        }

        public async Task<string> GenerateAuditReportAsync(DateTime startDate, DateTime endDate, string? userId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var query = _dbSet.Where(al => al.CreatedDate >= startDate && al.CreatedDate <= endDate.Date.AddDays(1));

                if (!string.IsNullOrEmpty(userId))
                    query = query.Where(al => al.UserId == userId);

                var auditLogs = await query
                    .OrderByDescending(al => al.CreatedDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var report = new
                {
                    GeneratedDate = DateTime.Now,
                    Period = new { StartDate = startDate, EndDate = endDate },
                    UserId = userId,
                    TotalEntries = auditLogs.Count,
                    OperationSummary = auditLogs.GroupBy(al => al.Operation).ToDictionary(g => g.Key, g => g.Count()),
                    TableSummary = auditLogs.GroupBy(al => al.TableName).ToDictionary(g => g.Key, g => g.Count()),
                    AuditEntries = auditLogs.Take(100) // Limit for report size
                };

                return JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating audit report from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        #endregion

        #region Security and Access Control Monitoring

        public async Task<IEnumerable<AuditLog>> GetAfterHoursActivityAsync(TimeSpan businessStartTime, TimeSpan businessEndTime, CancellationToken cancellationToken = default)
        {
            try
            {
                var yesterday = DateTime.Today.AddDays(-1);
                var today = DateTime.Today.AddDays(1);

                // Fixed: Removed ContinueWith and used proper async/await pattern
                var logs = await _dbSet
                    .Where(al => al.CreatedDate >= yesterday && al.CreatedDate < today)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                // Filter in memory to avoid complex SQL translation
                return logs.Where(al =>
                {
                    var timeOfDay = al.CreatedDate.TimeOfDay;
                    return timeOfDay < businessStartTime || timeOfDay > businessEndTime;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving after hours activity");
                throw;
            }
        }

        public async Task<IEnumerable<AuditLog>> GetUnauthorizedAccessAttemptsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Look for operations that might indicate unauthorized access
                var suspiciousOperations = new[] { "DELETE", "UPDATE" };

                return await _dbSet
                    .Where(al => suspiciousOperations.Contains(al.Operation) &&
                               (al.CreatedDate.Hour < 6 || al.CreatedDate.Hour > 22)) // Outside business hours
                    .OrderByDescending(al => al.CreatedDate)
                    .Take(100)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unauthorized access attempts");
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetUserActivitySummaryAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var endDateInclusive = endDate.Date.AddDays(1);

                var activity = await _dbSet
                    .Where(al => al.CreatedDate >= startDate && al.CreatedDate < endDateInclusive)
                    .GroupBy(al => al.UserId)
                    .Select(g => new { UserId = g.Key, Count = g.Count() })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return activity.ToDictionary(a => a.UserId ?? "Unknown", a => a.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user activity summary from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<bool> FlagSuspiciousActivityAsync(int auditId, string suspicionReason, CancellationToken cancellationToken = default)
        {
            try
            {
                var auditLog = await _dbSet.FindAsync(new object[] { auditId }, cancellationToken).ConfigureAwait(false);

                if (auditLog != null)
                {
                    // Add a note to the existing audit log or create a new flag entry
                    var flagEntry = new AuditLog
                    {
                        TableName = "AUDIT_FLAGS",
                        Operation = "FLAG",
                        OldValues = auditLog.AuditId.ToString(),
                        NewValues = suspicionReason,
                        UserId = "SYSTEM",
                        CreatedDate = DateTime.Now
                    };

                    await _dbSet.AddAsync(flagEntry, cancellationToken).ConfigureAwait(false);
                    var saved = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    _logger.LogWarning("Flagged suspicious activity for audit {AuditId}: {Reason}", auditId, suspicionReason);
                    return saved > 0;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flagging suspicious activity for audit {AuditId}", auditId);
                throw;
            }
        }

        #endregion

        #region File Operations and Export Functions

        public async Task<bool> ExportAuditLogsAsync(string filePath, DateTime startDate, DateTime endDate, string format = "CSV", CancellationToken cancellationToken = default)
        {
            try
            {
                var auditLogs = await GetAuditsByDateRangeAsync(startDate, endDate, cancellationToken).ConfigureAwait(false);

                if (format.ToUpper() == "CSV")
                {
                    var csvContent = "AuditId,TableName,Operation,UserId,CreatedDate,OldValues,NewValues\n";
                    csvContent += string.Join("\n", auditLogs.Select(al =>
                        $"{al.AuditId},{al.TableName},{al.Operation},{al.UserId},{al.CreatedDate:yyyy-MM-dd HH:mm:ss},\"{al.OldValues?.Replace("\"", "\"\"") ?? ""}\",\"{al.NewValues?.Replace("\"", "\"\"") ?? ""}\""));

                    await File.WriteAllTextAsync(filePath, csvContent, cancellationToken).ConfigureAwait(false);
                }
                else if (format.ToUpper() == "JSON")
                {
                    var jsonContent = JsonSerializer.Serialize(auditLogs, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(filePath, jsonContent, cancellationToken).ConfigureAwait(false);
                }

                _logger.LogInformation("Exported {Count} audit logs to {FilePath} in {Format} format", auditLogs.Count(), filePath, format);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting audit logs to {FilePath}", filePath);
                throw;
            }
        }

        public async Task<int> ArchiveAuditLogsAsync(DateTime archiveDate, string archiveLocation, CancellationToken cancellationToken = default)
        {
            try
            {
                var logsToArchive = await _dbSet
                    .Where(al => al.CreatedDate < archiveDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (logsToArchive.Any())
                {
                    // Export to archive location
                    var archiveFilePath = Path.Combine(archiveLocation, $"audit_archive_{archiveDate:yyyyMMdd}.json");
                    var jsonContent = JsonSerializer.Serialize(logsToArchive, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(archiveFilePath, jsonContent, cancellationToken).ConfigureAwait(false);

                    // Remove archived logs from active database
                    _dbSet.RemoveRange(logsToArchive);
                    var deleted = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation("Archived {Count} audit logs to {ArchivePath}", logsToArchive.Count, archiveFilePath);
                    return deleted;
                }

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving audit logs to {ArchiveLocation}", archiveLocation);
                throw;
            }
        }

        #endregion

        #region Stub Implementations for Remaining Interface Methods

        public Task<IEnumerable<AuditLog>> GetDataModificationsAsync(string tableName, string fieldName, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) =>
            Task.FromResult(Enumerable.Empty<AuditLog>());

        public Task<Dictionary<string, (string OldValue, string NewValue, DateTime ModifiedDate)>> GetFieldChangeHistoryAsync(string tableName, int entityId, string fieldName, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, (string, string, DateTime)>());

        public Task<IEnumerable<AuditLog>> GetBulkOperationsAsync(string operation, int minimumRecordCount, CancellationToken cancellationToken = default) =>
            Task.FromResult(Enumerable.Empty<AuditLog>());

        public Task<long> GetAuditLogSizeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0L);

        public Task<Dictionary<DateTime, int>> GetAuditVolumeByDateAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<DateTime, int>());

        public Task<IEnumerable<AuditLog>> GetHighFrequencyOperationsAsync(TimeSpan timeWindow, int operationCountThreshold, CancellationToken cancellationToken = default) =>
            Task.FromResult(Enumerable.Empty<AuditLog>());

        public Task<bool> OptimizeAuditLogStorageAsync(int retentionDays, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IEnumerable<AuditLog>> SearchAuditLogsAsync(string searchTerm, CancellationToken cancellationToken = default) =>
            Task.FromResult(Enumerable.Empty<AuditLog>());

        public Task<IEnumerable<AuditLog>> GetAuditsByOperationTypeAsync(string operationType, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Enumerable.Empty<AuditLog>());

        public Task<IEnumerable<AuditLog>> GetRecentCriticalChangesAsync(IEnumerable<string> criticalTables, int hours = 24, CancellationToken cancellationToken = default) =>
            Task.FromResult(Enumerable.Empty<AuditLog>());

        public Task<Dictionary<string, object?>> GetEntityStateAtTimeAsync(string tableName, int entityId, DateTime pointInTime, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, object?>());

        public Task<IEnumerable<AuditLog>> GetChangesSinceTimestampAsync(string tableName, DateTime timestamp, CancellationToken cancellationToken = default) =>
            Task.FromResult(Enumerable.Empty<AuditLog>());

        public Task<bool> CanRollbackChangeAsync(int auditId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<string> GenerateRollbackScriptAsync(int auditId, CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Empty);

        public Task<IEnumerable<AuditLog>> GetComplianceAuditTrailAsync(string regulatoryStandard, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) =>
            Task.FromResult(Enumerable.Empty<AuditLog>());

        public Task<Dictionary<string, object>> GetComplianceMetricsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, object>());

        public Task<int> CreateAuditLogsBatchAsync(IEnumerable<AuditLog> auditLogs, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<int> PurgeOldAuditLogsAsync(DateTime olderThanDate, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<bool> SetupAuditAlertAsync(string tableName, string operation, string alertCondition, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IEnumerable<AuditLog>> GetTriggeredAlertsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Enumerable.Empty<AuditLog>());

        public Task<bool> ProcessAuditAlertsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<Dictionary<string, decimal>> GetAuditTrendAnalysisAsync(DateTime startDate, DateTime endDate, string groupBy = "Daily", CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, decimal>());

        public Task<(int TotalOperations, int UniqueUsers, int AffectedTables)> GetAuditSummaryStatisticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) =>
            Task.FromResult((0, 0, 0));

        public Task<IEnumerable<(string TableName, string Operation, int Frequency)>> GetMostFrequentOperationsAsync(int topCount, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) =>
            Task.FromResult(Enumerable.Empty<(string, string, int)>());

        #endregion

        #region Private Helper Methods

        private static int GetWeekOfYear(DateTime date)
        {
            var culture = CultureInfo.CurrentCulture;
            return culture.Calendar.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        #endregion
    }
}