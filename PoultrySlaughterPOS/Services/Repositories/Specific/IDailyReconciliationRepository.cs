using PoultrySlaughterPOS.Models;

namespace PoultrySlaughterPOS.Services.Repositories
{
    /// <summary>
    /// Advanced repository interface for daily reconciliation operations management
    /// providing comprehensive variance analysis and operational efficiency metrics
    /// </summary>
    public interface IDailyReconciliationRepository : IRepository<DailyReconciliation>
    {
        // Core Reconciliation Operations
        Task<DailyReconciliation?> GetTruckReconciliationAsync(int truckId, DateTime reconciliationDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<DailyReconciliation>> GetDailyReconciliationsAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<bool> CreateReconciliationRecordAsync(int truckId, DateTime date, decimal loadWeight, decimal soldWeight, CancellationToken cancellationToken = default);
        Task<bool> IsReconciliationCompleteAsync(int truckId, DateTime date, CancellationToken cancellationToken = default);

        // Wastage Analysis and Variance Management
        Task<decimal> CalculateWastagePercentageAsync(decimal loadWeight, decimal soldWeight);
        Task<decimal> GetDailyWastagePercentageAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<Dictionary<int, decimal>> GetTruckWastageAnalysisAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<DailyReconciliation>> GetHighWastageReconciliationsAsync(decimal wastageThreshold, CancellationToken cancellationToken = default);

        // Performance Monitoring and KPI Tracking
        Task<(decimal TotalLoadWeight, decimal TotalSoldWeight, decimal TotalWastage)> GetPeriodReconciliationSummaryAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<Dictionary<int, (decimal AverageWastage, int ReconciliationCount)>> GetTruckPerformanceMetricsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<Dictionary<DateTime, decimal>> GetWastageTrendAnalysisAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        // Status Management and Workflow Control
        Task<IEnumerable<DailyReconciliation>> GetReconciliationsByStatusAsync(string status, CancellationToken cancellationToken = default);
        Task<bool> UpdateReconciliationStatusAsync(int reconciliationId, string newStatus, string notes, CancellationToken cancellationToken = default);
        Task<IEnumerable<DailyReconciliation>> GetPendingReconciliationsAsync(CancellationToken cancellationToken = default);
        Task<int> GetPendingReconciliationCountAsync(CancellationToken cancellationToken = default);

        // Advanced Analytics and Reporting
        Task<IEnumerable<DailyReconciliation>> GetReconciliationsRequiringReviewAsync(decimal criticalWastageThreshold, CancellationToken cancellationToken = default);
        Task<Dictionary<string, decimal>> GetWastageDistributionAnalysisAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<DailyReconciliation>> GetBestPerformingReconciliationsAsync(int topCount, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<decimal> GetTruckEfficiencyRatingAsync(int truckId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        // Variance Investigation and Root Cause Analysis
        Task<IEnumerable<DailyReconciliation>> GetVarianceAnomaliesAsync(decimal standardDeviationThreshold, CancellationToken cancellationToken = default);
        Task<Dictionary<int, List<DailyReconciliation>>> GetConsistentVariancePatternsByTruckAsync(decimal varianceThreshold, int dayRange, CancellationToken cancellationToken = default);
        Task<bool> FlagReconciliationForInvestigationAsync(int reconciliationId, string investigationReason, CancellationToken cancellationToken = default);

        // Operational Efficiency Metrics
        Task<decimal> GetOperationalEfficiencyRateAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<Dictionary<int, decimal>> GetTruckUtilizationRatesAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<(decimal BestEfficiency, decimal WorstEfficiency, decimal AverageEfficiency)> GetEfficiencyBenchmarksAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        // Financial Impact Assessment
        Task<decimal> CalculateFinancialImpactOfWastageAsync(decimal wastageWeight, decimal unitPrice);
        Task<Dictionary<DateTime, decimal>> GetDailyWastageFinancialImpactAsync(DateTime startDate, DateTime endDate, decimal averageUnitPrice, CancellationToken cancellationToken = default);
        Task<decimal> GetTotalWastageFinancialLossAsync(DateTime startDate, DateTime endDate, decimal unitPrice, CancellationToken cancellationToken = default);

        // Data Quality and Integrity Management
        Task<bool> ValidateReconciliationDataIntegrityAsync(int reconciliationId, CancellationToken cancellationToken = default);
        Task<IEnumerable<DailyReconciliation>> GetIncompleteReconciliationsAsync(int dayThreshold, CancellationToken cancellationToken = default);
        Task<bool> RecalculateReconciliationMetricsAsync(int reconciliationId, CancellationToken cancellationToken = default);

        // Historical Analysis and Predictive Insights
        Task<Dictionary<int, decimal>> GetSeasonalWastagePatternsByTruckAsync(int truckId, int monthRange, CancellationToken cancellationToken = default);
        Task<decimal> PredictExpectedWastageAsync(int truckId, decimal loadWeight, DateTime forecastDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<DailyReconciliation>> GetHistoricalPerformanceBaselineAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        // Bulk Operations and Performance Optimization
        Task<IEnumerable<DailyReconciliation>> CreateReconciliationBatchAsync(IEnumerable<DailyReconciliation> reconciliations, CancellationToken cancellationToken = default);
        Task<int> UpdateReconciliationStatusBatchAsync(IEnumerable<int> reconciliationIds, string status, CancellationToken cancellationToken = default);
        Task<Dictionary<int, DailyReconciliation>> GetMultipleTruckReconciliationsAsync(IEnumerable<int> truckIds, DateTime date, CancellationToken cancellationToken = default);

        // Compliance and Audit Support
        Task<IEnumerable<DailyReconciliation>> GetReconciliationsForAuditAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<bool> GenerateReconciliationAuditTrailAsync(int reconciliationId, CancellationToken cancellationToken = default);
        Task<Dictionary<string, int>> GetReconciliationComplianceMetricsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    }
}