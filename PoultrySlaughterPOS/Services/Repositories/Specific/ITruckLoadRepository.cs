using PoultrySlaughterPOS.Models;

namespace PoultrySlaughterPOS.Services.Repositories
{
    /// <summary>
    /// Enterprise-level repository interface for truck loading operations management
    /// supporting complex logistics workflows and performance optimization metrics
    /// </summary>
    public interface ITruckLoadRepository : IRepository<TruckLoad>
    {
        // Core Load Management Operations
        Task<TruckLoad?> GetTruckCurrentLoadAsync(int truckId, CancellationToken cancellationToken = default);
        Task<IEnumerable<TruckLoad>> GetTruckLoadsByDateAsync(DateTime loadDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<TruckLoad>> GetTruckLoadsByDateRangeAsync(int truckId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<TruckLoad?> GetMostRecentLoadAsync(int truckId, CancellationToken cancellationToken = default);

        // Load Status Management and Workflow
        Task<IEnumerable<TruckLoad>> GetLoadsByStatusAsync(string status, CancellationToken cancellationToken = default);
        Task<bool> UpdateLoadStatusAsync(int loadId, string newStatus, CancellationToken cancellationToken = default);
        Task<int> UpdateMultipleLoadStatusAsync(IEnumerable<int> loadIds, string newStatus, CancellationToken cancellationToken = default);
        Task<bool> IsLoadInProgressAsync(int truckId, CancellationToken cancellationToken = default);

        // Weight and Capacity Analytics
        Task<decimal> GetTotalLoadWeightByDateAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<decimal> GetTruckTotalLoadWeightAsync(int truckId, DateTime date, CancellationToken cancellationToken = default);
        Task<Dictionary<int, decimal>> GetDailyLoadWeightsByTruckAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<(decimal MinWeight, decimal MaxWeight, decimal AverageWeight)> GetLoadWeightStatisticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        // Cage Management and Optimization
        Task<int> GetTotalCagesCountByDateAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<Dictionary<int, int>> GetCagesCountByTruckAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<decimal> CalculateAverageWeightPerCageAsync(int truckId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<TruckLoad>> GetLoadsByCagesCountRangeAsync(int minCages, int maxCages, CancellationToken cancellationToken = default);

        // Performance Monitoring and KPIs
        Task<Dictionary<int, decimal>> GetTruckEfficiencyMetricsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<TruckLoad>> GetHighVolumeLoadsAsync(decimal weightThreshold, CancellationToken cancellationToken = default);
        Task<Dictionary<DateTime, decimal>> GetLoadTrendAnalysisAsync(int truckId, int dayRange, CancellationToken cancellationToken = default);
        Task<decimal> GetTruckCapacityUtilizationAsync(int truckId, decimal maxCapacity, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        // Reconciliation Support Operations
        Task<IEnumerable<TruckLoad>> GetLoadsForReconciliationAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<Dictionary<int, (decimal LoadWeight, decimal SoldWeight, decimal Variance)>> GetLoadVsSalesComparisonAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<IEnumerable<TruckLoad>> GetUnreconciledLoadsAsync(int dayThreshold, CancellationToken cancellationToken = default);
        Task<bool> MarkLoadAsReconciledAsync(int loadId, CancellationToken cancellationToken = default);

        // Advanced Search and Filtering
        Task<IEnumerable<TruckLoad>> SearchLoadsByNotesAsync(string searchTerm, CancellationToken cancellationToken = default);
        Task<IEnumerable<TruckLoad>> GetLoadsByWeightRangeAsync(decimal minWeight, decimal maxWeight, CancellationToken cancellationToken = default);
        Task<IEnumerable<TruckLoad>> GetRecentLoadsAsync(int hours = 24, CancellationToken cancellationToken = default);

        // Load Planning and Optimization
        Task<decimal> GetAverageLoadTimeAsync(int truckId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<TruckLoad>> GetOptimalLoadsByTruckCapacityAsync(decimal targetCapacity, decimal tolerance, CancellationToken cancellationToken = default);
        Task<Dictionary<int, int>> GetTruckLoadFrequencyAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        // Data Integrity and Validation
        Task<bool> ValidateLoadDataIntegrityAsync(int loadId, CancellationToken cancellationToken = default);
        Task<IEnumerable<TruckLoad>> GetLoadsWithAnomaliesAsync(CancellationToken cancellationToken = default);
        Task<bool> RecalculateLoadMetricsAsync(int loadId, CancellationToken cancellationToken = default);

        // Bulk Operations for Performance
        Task<IEnumerable<TruckLoad>> CreateLoadBatchAsync(IEnumerable<TruckLoad> loads, CancellationToken cancellationToken = default);
        Task<int> UpdateLoadWeightsBatchAsync(Dictionary<int, decimal> loadWeightUpdates, CancellationToken cancellationToken = default);
        Task<IEnumerable<TruckLoad>> GetTruckLoadsBatchAsync(IEnumerable<int> truckIds, DateTime date, CancellationToken cancellationToken = default);

        // Historical Analysis and Reporting
        Task<Dictionary<string, decimal>> GetMonthlyLoadSummaryAsync(int year, int month, CancellationToken cancellationToken = default);
        Task<IEnumerable<TruckLoad>> GetTopPerformingLoadsAsync(int topCount, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<Dictionary<int, decimal>> GetSeasonalLoadPatternsAsync(int truckId, int yearRange, CancellationToken cancellationToken = default);
    }
}