using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Data;
using PoultrySlaughterPOS.Models;
using System.Text.Json;

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

        #region Data Change Analysis

        public async Task<IEnumerable<AuditLog>> GetDataModificationsAsync(string tableName, string fieldName, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var endDateInclusive = endDate.Date.AddDays(1);

                return await _dbSet
                    .Where(al => al.TableName == tableName &&
                               al.CreatedDate >= startDate &&
                               al.CreatedDate < endDateInclusive &&
                               ((al.OldValues != null && al.OldValues.Contains($"\"{fieldName}\"")) ||
                                (al.NewValues != null && al.NewValues.Contains($"\"{fieldName}\""))))
                    .OrderByDescending(al => al.CreatedDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving data modifications for {TableName}.{FieldName}", tableName, fieldName);
                throw;
            }
        }

        public async Task<Dictionary<string, (string OldValue, string NewValue, DateTime ModifiedDate)>> GetFieldChangeHistoryAsync(string tableName, int entityId, string fieldName, CancellationToken cancellationToken = default)
        {
            try
            {
                var auditLogs = await _dbSet
                    .Where(al => al.TableName == tableName &&
                               ((al.OldValues != null && al.OldValues.Contains($"\"{entityId}\"")) ||
                                (al.NewValues != null && al.NewValues.Contains($"\"{entityId}\""))))
                    .OrderByDescending(al => al.CreatedDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var changes = new Dictionary<string, (string OldValue, string NewValue, DateTime ModifiedDate)>();

                foreach (var log in auditLogs.Take(10)) // Limit to recent changes
                {
                    var key = $"{log.Operation}_{log.CreatedDate:yyyyMMddHHmmss}";
                    changes[key] = (log.OldValues ?? "", log.NewValues ?? "", log.CreatedDate);
                }

                return changes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving field change history for {TableName}.{EntityId}.{FieldName}", tableName, entityId, fieldName);
                throw;
            }
        }

        public async Task<IEnumerable<AuditLog>> GetBulkOperationsAsync(string operation, int minimumRecordCount, CancellationToken cancellationToken = default)
        {
            try
            {
                // Group by operation and timestamp to identify bulk operations
                var bulkOperations = await _dbSet
                    .Where(al => al.Operation == operation)
                    .GroupBy(al => new { al.Operation, al.CreatedDate.Hour, al.CreatedDate.Minute })
                    .Where(g => g.Count() >= minimumRecordCount)
                    .SelectMany(g => g)
                    .OrderByDescending(al => al.CreatedDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return bulkOperations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving bulk operations for {Operation} with minimum count {MinimumCount}", operation, minimumRecordCount);
                throw;
            }
        }

        #endregion

        #region Audit Storage and Performance Management

        public async Task<long> GetAuditLogSizeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var count = await _dbSet.CountAsync(cancellationToken).ConfigureAwait(false);

                // Estimate size based on average record size (approximate)
                const long estimatedBytesPerRecord = 1024; // 1KB per record estimate
                return count * estimatedBytesPerRecord;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating audit log size");
                throw;
            }
        }

        public async Task<Dictionary<DateTime, int>> GetAuditVolumeByDateAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var endDateInclusive = endDate.Date.AddDays(1);

                var volumeData = await _dbSet
                    .Where(al => al.CreatedDate >= startDate && al.CreatedDate < endDateInclusive)
                    .GroupBy(al => al.CreatedDate.Date)
                    .Select(g => new { Date = g.Key, Count = g.Count() })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return volumeData.ToDictionary(vd => vd.Date, vd => vd.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit volume by date from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<IEnumerable<AuditLog>> GetHighFrequencyOperationsAsync(TimeSpan timeWindow, int operationCountThreshold, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromTime = DateTime.Now.Subtract(timeWindow);

                return await _dbSet
                    .Where(al => al.CreatedDate >= fromTime)
                    .GroupBy(al => new { al.Operation, al.UserId })
                    .Where(g => g.Count() > operationCountThreshold)
                    .SelectMany(g => g)
                    .OrderByDescending(al => al.CreatedDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving high frequency operations with threshold {Threshold} and window {TimeWindow}", operationCountThreshold, timeWindow);
                throw;
            }
        }

        public async Task<bool> OptimizeAuditLogStorageAsync(int retentionDays, CancellationToken cancellationToken = default)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-retentionDays);

                var oldLogs = await _dbSet
                    .Where(al => al.CreatedDate < cutoffDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (oldLogs.Any())
                {
                    _dbSet.RemoveRange(oldLogs);
                    var deleted = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation("Optimized audit log storage by removing {Count} old records", deleted);
                    return deleted > 0;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing audit log storage with retention {RetentionDays} days", retentionDays);
                throw;
            }
        }

        #endregion

        #region Security and Access Control Monitoring

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

        public async Task<IEnumerable<AuditLog>> GetAfterHoursActivityAsync(TimeSpan businessStartTime, TimeSpan businessEndTime, CancellationToken cancellationToken = default)
        {
            try
            {
                var yesterday = DateTime.Today.AddDays(-1);
                var today = DateTime.Today.AddDays(1);

                return await _dbSet
                    .Where(al => al.CreatedDate >= yesterday && al.CreatedDate < today)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false)
                    .ContinueWith(task =>
                    {
                        var logs = task.Result;
                        return logs.Where(al =>
                        {
                            var timeOfDay = al.CreatedDate.TimeOfDay;
                            return timeOfDay < businessStartTime || timeOfDay > businessEndTime;
                        });
                    }, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving after hours activity");
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

        #region Advanced Search and Filtering

        public async Task<IEnumerable<AuditLog>> SearchAuditLogsAsync(string searchTerm, CancellationToken cancellationToken = default)
        {
            try
            {
                var lowerSearchTerm = searchTerm.ToLower();

                return await _dbSet
                    .Where(al => al.TableName.ToLower().Contains(lowerSearchTerm) ||
                               al.Operation.ToLower().Contains(lowerSearchTerm) ||
                               (al.UserId != null && al.UserId.ToLower().Contains(lowerSearchTerm)) ||
                               (al.OldValues != null && al.OldValues.ToLower().Contains(lowerSearchTerm)) ||
                               (al.NewValues != null && al.NewValues.ToLower().Contains(lowerSearchTerm)))
                    .OrderByDescending(al => al.CreatedDate)
                    .Take(100)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching audit logs with term {SearchTerm}", searchTerm);
                throw;
            }
        }

        public async Task<IEnumerable<AuditLog>> GetAuditsByOperationTypeAsync(string operationType, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var query = _dbSet.Where(al => al.Operation == operationType);

                if (startDate.HasValue)
                    query = query.Where(al => al.CreatedDate >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(al => al.CreatedDate <= endDate.Value.Date.AddDays(1));

                return await query
                    .OrderByDescending(al => al.CreatedDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audits by operation type {OperationType}", operationType);
                throw;
            }
        }

        public async Task<IEnumerable<AuditLog>> GetRecentCriticalChangesAsync(IEnumerable<string> criticalTables, int hours = 24, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromTime = DateTime.Now.AddHours(-hours);
                var tablesList = criticalTables.ToList();

                return await _dbSet
                    .Where(al => tablesList.Contains(al.TableName) && al.CreatedDate >= fromTime)
                    .OrderByDescending(al => al.CreatedDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent critical changes for {Hours} hours", hours);
                throw;
            }
        }

        #endregion

        #region Data Recovery and Rollback Support

        public async Task<Dictionary<string, object?>> GetEntityStateAtTimeAsync(string tableName, int entityId, DateTime pointInTime, CancellationToken cancellationToken = default)
        {
            try
            {
                var auditLog = await _dbSet
                    .Where(al => al.TableName == tableName &&
                               al.CreatedDate <= pointInTime &&
                               ((al.OldValues != null && al.OldValues.Contains($"\"{entityId}\"")) ||
                                (al.NewValues != null && al.NewValues.Contains($"\"{entityId}\""))))
                    .OrderByDescending(al => al.CreatedDate)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (auditLog?.OldValues != null)
                {
                    try
                    {
                        return JsonSerializer.Deserialize<Dictionary<string, object?>>(auditLog.OldValues) ?? new Dictionary<string, object?>();
                    }
                    catch (JsonException)
                    {
                        return new Dictionary<string, object?> { ["RawData"] = auditLog.OldValues };
                    }
                }

                return new Dictionary<string, object?>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving entity state at time for {TableName}.{EntityId} at {PointInTime}", tableName, entityId, pointInTime);
                throw;
            }
        }

        public async Task<IEnumerable<AuditLog>> GetChangesSinceTimestampAsync(string tableName, DateTime timestamp, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Where(al => al.TableName == tableName && al.CreatedDate > timestamp)
                    .OrderBy(al => al.CreatedDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving changes since timestamp for {TableName} since {Timestamp}", tableName, timestamp);
                throw;
            }
        }

        public async Task<bool> CanRollbackChangeAsync(int auditId, CancellationToken cancellationToken = default)
        {
            try
            {
                var auditLog = await _dbSet.FindAsync(new object[] { auditId }, cancellationToken).ConfigureAwait(false);

                if (auditLog == null)
                    return false;

                // Check if the operation is reversible and if old values exist
                var reversibleOperations = new[] { "UPDATE", "DELETE" };
                return reversibleOperations.Contains(auditLog.Operation) && !string.IsNullOrEmpty(auditLog.OldValues);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking rollback capability for audit {AuditId}", auditId);
                throw;
            }
        }

        public async Task<string> GenerateRollbackScriptAsync(int auditId, CancellationToken cancellationToken = default)
        {
            try
            {
                var auditLog = await _dbSet.FindAsync(new object[] { auditId }, cancellationToken).ConfigureAwait(false);

                if (auditLog == null || string.IsNullOrEmpty(auditLog.OldValues))
                    return string.Empty;

                // Generate a basic rollback script
                var script = $"-- Rollback script for {auditLog.TableName} {auditLog.Operation}\n";
                script += $"-- Original operation performed by {auditLog.UserId} on {auditLog.CreatedDate}\n";
                script += $"-- Old Values: {auditLog.OldValues}\n";
                script += $"-- WARNING: Review and test this script before execution\n\n";

                switch (auditLog.Operation.ToUpper())
                {
                    case "UPDATE":
                        script += $"-- UPDATE {auditLog.TableName} SET ... WHERE ... ;\n";
                        break;
                    case "DELETE":
                        script += $"-- INSERT INTO {auditLog.TableName} (...) VALUES (...);\n";
                        break;
                    default:
                        script += $"-- Rollback not supported for {auditLog.Operation} operations\n";
                        break;
                }

                return script;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating rollback script for audit {AuditId}", auditId);
                throw;
            }
        }

        #endregion

        #region Regulatory Compliance and Reporting

        public async Task<IEnumerable<AuditLog>> GetComplianceAuditTrailAsync(string regulatoryStandard, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var endDateInclusive = endDate.Date.AddDays(1);

                // For compliance purposes, return all audit logs within the date range
                return await _dbSet
                    .Where(al => al.CreatedDate >= startDate && al.CreatedDate < endDateInclusive)
                    .OrderBy(al => al.CreatedDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving compliance audit trail for {Standard} from {StartDate} to {EndDate}", regulatoryStandard, startDate, endDate);
                throw;
            }
        }

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

        public async Task<Dictionary<string, object>> GetComplianceMetricsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var endDateInclusive = endDate.Date.AddDays(1);
                var auditLogs = await _dbSet
                    .Where(al => al.CreatedDate >= startDate && al.CreatedDate < endDateInclusive)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var metrics = new Dictionary<string, object>
                {
                    ["TotalAuditEntries"] = auditLogs.Count,
                    ["UniqueUsers"] = auditLogs.Select(al => al.UserId).Distinct().Count(),
                    ["TablesModified"] = auditLogs.Select(al => al.TableName).Distinct().Count(),
                    ["OperationBreakdown"] = auditLogs.GroupBy(al => al.Operation).ToDictionary(g => g.Key, g => g.Count()),
                    ["DailyAuditVolume"] = auditLogs.GroupBy(al => al.CreatedDate.Date).ToDictionary(g => g.Key.ToString("yyyy-MM-dd"), g => g.Count())
                };

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating compliance metrics from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        #endregion

        #region Batch Operations and Performance Optimization

        public async Task<int> CreateAuditLogsBatchAsync(IEnumerable<AuditLog> auditLogs, CancellationToken cancellationToken = default)
        {
            try
            {
                var logsList = auditLogs.ToList();
                await _dbSet.AddRangeAsync(logsList, cancellationToken).ConfigureAwait(false);
                var saved = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Created {Count} audit logs in batch operation", logsList.Count);
                return saved;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating audit logs batch");
                throw;
            }
        }

        public async Task<int> PurgeOldAuditLogsAsync(DateTime olderThanDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var oldLogs = await _dbSet
                    .Where(al => al.CreatedDate < olderThanDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (oldLogs.Any())
                {
                    _dbSet.RemoveRange(oldLogs);
                    var deleted = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation("Purged {Count} old audit logs older than {Date}", oldLogs.Count, olderThanDate);
                    return deleted;
                }

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error purging old audit logs older than {Date}", olderThanDate);
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

        #region Real-time Monitoring and Alerts

        public async Task<bool> SetupAuditAlertAsync(string tableName, string operation, string alertCondition, CancellationToken cancellationToken = default)
        {
            try
            {
                // Create an alert configuration entry
                var alertConfig = new AuditLog
                {
                    TableName = "AUDIT_ALERTS",
                    Operation = "CREATE_ALERT",
                    NewValues = JsonSerializer.Serialize(new { TableName = tableName, Operation = operation, Condition = alertCondition }),
                    UserId = "SYSTEM",
                    CreatedDate = DateTime.Now
                };

                await _dbSet.AddAsync(alertConfig, cancellationToken).ConfigureAwait(false);
                var saved = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Set up audit alert for {TableName} {Operation} with condition {Condition}", tableName, operation, alertCondition);
                return saved > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up audit alert for {TableName} {Operation}", tableName, operation);
                throw;
            }
        }

        public async Task<IEnumerable<AuditLog>> GetTriggeredAlertsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Where(al => al.TableName == "AUDIT_ALERTS" && al.Operation == "ALERT_TRIGGERED")
                    .OrderByDescending(al => al.CreatedDate)
                    .Take(50)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving triggered alerts");
                throw;
            }
        }

        public async Task<bool> ProcessAuditAlertsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // This is a placeholder for alert processing logic
                // In a real implementation, this would check for alert conditions and trigger notifications
                var recentActivity = await _dbSet
                    .Where(al => al.CreatedDate >= DateTime.Now.AddMinutes(-5))
                    .CountAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (recentActivity > 100) // Example threshold
                {
                    var alertLog = new AuditLog
                    {
                        TableName = "AUDIT_ALERTS",
                        Operation = "ALERT_TRIGGERED",
                        NewValues = $"High activity detected: {recentActivity} operations in 5 minutes",
                        UserId = "SYSTEM",
                        CreatedDate = DateTime.Now
                    };

                    await _dbSet.AddAsync(alertLog, cancellationToken).ConfigureAwait(false);
                    await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    _logger.LogWarning("High audit activity alert triggered: {ActivityCount} operations", recentActivity);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing audit alerts");
                throw;
            }
        }

        #endregion

        #region Statistical Analysis and Insights

        public async Task<Dictionary<string, decimal>> GetAuditTrendAnalysisAsync(DateTime startDate, DateTime endDate, string groupBy = "Daily", CancellationToken cancellationToken = default)
        {
            try
            {
                var endDateInclusive = endDate.Date.AddDays(1);
                var auditLogs = await _dbSet
                    .Where(al => al.CreatedDate >= startDate && al.CreatedDate < endDateInclusive)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                Dictionary<string, decimal> trends;

                switch (groupBy.ToLower())
                {
                    case "hourly":
                        trends = auditLogs
                            .GroupBy(al => al.CreatedDate.ToString("yyyy-MM-dd HH"))
                            .ToDictionary(g => g.Key, g => (decimal)g.Count());
                        break;
                    case "weekly":
                        trends = auditLogs
                            .GroupBy(al => $"{al.CreatedDate.Year}-W{GetWeekOfYear(al.CreatedDate)}")
                            .ToDictionary(g => g.Key, g => (decimal)g.Count());
                        break;
                    case "monthly":
                        trends = auditLogs
                            .GroupBy(al => al.CreatedDate.ToString("yyyy-MM"))
                            .ToDictionary(g => g.Key, g => (decimal)g.Count());
                        break;
                    default: // Daily
                        trends = auditLogs
                            .GroupBy(al => al.CreatedDate.ToString("yyyy-MM-dd"))
                            .ToDictionary(g => g.Key, g => (decimal)g.Count());
                        break;
                }

                return trends;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing audit trend analysis from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<(int TotalOperations, int UniqueUsers, int AffectedTables)> GetAuditSummaryStatisticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var endDateInclusive = endDate.Date.AddDays(1);
                var auditLogs = await _dbSet
                    .Where(al => al.CreatedDate >= startDate && al.CreatedDate < endDateInclusive)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var totalOperations = auditLogs.Count;
                var uniqueUsers = auditLogs.Select(al => al.UserId).Distinct().Count();
                var affectedTables = auditLogs.Select(al => al.TableName).Distinct().Count();

                return (totalOperations, uniqueUsers, affectedTables);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating audit summary statistics from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<IEnumerable<(string TableName, string Operation, int Frequency)>> GetMostFrequentOperationsAsync(int topCount, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var endDateInclusive = endDate.Date.AddDays(1);

                var operations = await _dbSet
                    .Where(al => al.CreatedDate >= startDate && al.CreatedDate < endDateInclusive)
                    .GroupBy(al => new { al.TableName, al.Operation })
                    .Select(g => new { g.Key.TableName, g.Key.Operation, Frequency = g.Count() })
                    .OrderByDescending(x => x.Frequency)
                    .Take(topCount)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return operations.Select(o => (o.TableName, o.Operation, o.Frequency));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving most frequent operations from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        #endregion

        #region Private Helper Methods

        private static int GetWeekOfYear(DateTime date)
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            return culture.Calendar.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        #endregion
    }
}