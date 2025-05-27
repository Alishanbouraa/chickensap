using PoultrySlaughterPOS.Models;

namespace PoultrySlaughterPOS.Services.Repositories
{
    /// <summary>
    /// Enterprise-grade audit log repository interface providing comprehensive
    /// audit trail management, compliance tracking, and forensic analysis capabilities
    /// </summary>
    public interface IAuditLogRepository : IRepository<AuditLog>
    {
        // Core Audit Operations
        Task<AuditLog> LogOperationAsync(string tableName, string operation, string? oldValues, string? newValues, string userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<AuditLog>> GetTableAuditHistoryAsync(string tableName, CancellationToken cancellationToken = default);
        Task<IEnumerable<AuditLog>> GetEntityAuditTrailAsync(string tableName, int entityId, CancellationToken cancellationToken = default);
        Task<IEnumerable<AuditLog>> GetUserActivityLogAsync(string userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);

        // Advanced Audit Analysis
        Task<Dictionary<string, int>> GetOperationStatisticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<Dictionary<string, int>> GetTableModificationFrequencyAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<AuditLog>> GetSuspiciousActivitiesAsync(int activityThreshold, TimeSpan timeWindow, CancellationToken cancellationToken = default);
        Task<Dictionary<string, List<string>>> GetUserPermissionAnalysisAsync(string userId, CancellationToken cancellationToken = default);

        // Compliance and Forensic Support
        Task<IEnumerable<AuditLog>> GetAuditsByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<AuditLog>> GetCriticalOperationsAsync(IEnumerable<string> criticalOperations, CancellationToken cancellationToken = default);
        Task<bool> VerifyAuditIntegrityAsync(int auditId, CancellationToken cancellationToken = default);
        Task<string> GenerateAuditReportAsync(DateTime startDate, DateTime endDate, string? userId = null, CancellationToken cancellationToken = default);

        // Data Change Analysis
        Task<IEnumerable<AuditLog>> GetDataModificationsAsync(string tableName, string fieldName, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<Dictionary<string, (string OldValue, string NewValue, DateTime ModifiedDate)>> GetFieldChangeHistoryAsync(string tableName, int entityId, string fieldName, CancellationToken cancellationToken = default);
        Task<IEnumerable<AuditLog>> GetBulkOperationsAsync(string operation, int minimumRecordCount, CancellationToken cancellationToken = default);

        // Performance and System Health Monitoring
        Task<long> GetAuditLogSizeAsync(CancellationToken cancellationToken = default);
        Task<Dictionary<DateTime, int>> GetAuditVolumeByDateAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<AuditLog>> GetHighFrequencyOperationsAsync(TimeSpan timeWindow, int operationCountThreshold, CancellationToken cancellationToken = default);
        Task<bool> OptimizeAuditLogStorageAsync(int retentionDays, CancellationToken cancellationToken = default);

        // Security and Access Control Monitoring
        Task<IEnumerable<AuditLog>> GetUnauthorizedAccessAttemptsAsync(CancellationToken cancellationToken = default);
        Task<Dictionary<string, int>> GetUserActivitySummaryAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<AuditLog>> GetAfterHoursActivityAsync(TimeSpan businessStartTime, TimeSpan businessEndTime, CancellationToken cancellationToken = default);
        Task<bool> FlagSuspiciousActivityAsync(int auditId, string suspicionReason, CancellationToken cancellationToken = default);

        // Advanced Search and Filtering
        Task<IEnumerable<AuditLog>> SearchAuditLogsAsync(string searchTerm, CancellationToken cancellationToken = default);
        Task<IEnumerable<AuditLog>> GetAuditsByOperationTypeAsync(string operationType, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<AuditLog>> GetRecentCriticalChangesAsync(IEnumerable<string> criticalTables, int hours = 24, CancellationToken cancellationToken = default);

        // Data Recovery and Rollback Support
        Task<Dictionary<string, object?>> GetEntityStateAtTimeAsync(string tableName, int entityId, DateTime pointInTime, CancellationToken cancellationToken = default);
        Task<IEnumerable<AuditLog>> GetChangesSinceTimestampAsync(string tableName, DateTime timestamp, CancellationToken cancellationToken = default);
        Task<bool> CanRollbackChangeAsync(int auditId, CancellationToken cancellationToken = default);
        Task<string> GenerateRollbackScriptAsync(int auditId, CancellationToken cancellationToken = default);

        // Regulatory Compliance and Reporting
        Task<IEnumerable<AuditLog>> GetComplianceAuditTrailAsync(string regulatoryStandard, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<bool> ExportAuditLogsAsync(string filePath, DateTime startDate, DateTime endDate, string format = "CSV", CancellationToken cancellationToken = default);
        Task<Dictionary<string, object>> GetComplianceMetricsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        // Batch Operations and Performance Optimization
        Task<int> CreateAuditLogsBatchAsync(IEnumerable<AuditLog> auditLogs, CancellationToken cancellationToken = default);
        Task<int> PurgeOldAuditLogsAsync(DateTime olderThanDate, CancellationToken cancellationToken = default);
        Task<int> ArchiveAuditLogsAsync(DateTime archiveDate, string archiveLocation, CancellationToken cancellationToken = default);

        // Real-time Monitoring and Alerts
        Task<bool> SetupAuditAlertAsync(string tableName, string operation, string alertCondition, CancellationToken cancellationToken = default);
        Task<IEnumerable<AuditLog>> GetTriggeredAlertsAsync(CancellationToken cancellationToken = default);
        Task<bool> ProcessAuditAlertsAsync(CancellationToken cancellationToken = default);

        // Statistical Analysis and Insights
        Task<Dictionary<string, decimal>> GetAuditTrendAnalysisAsync(DateTime startDate, DateTime endDate, string groupBy = "Daily", CancellationToken cancellationToken = default);
        Task<(int TotalOperations, int UniqueUsers, int AffectedTables)> GetAuditSummaryStatisticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<(string TableName, string Operation, int Frequency)>> GetMostFrequentOperationsAsync(int topCount, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    }
}