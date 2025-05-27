using PoultrySlaughterPOS.Models;

namespace PoultrySlaughterPOS.Services.Repositories
{
    /// <summary>
    /// Advanced repository interface for truck-specific database operations
    /// with specialized queries for POS workflow management
    /// </summary>
    public interface ITruckRepository : IRepository<Truck>
    {
        // Truck Status Management
        Task<IEnumerable<Truck>> GetActiveTrucksAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Truck>> GetTrucksWithLoadsAsync(DateTime? loadDate = null, CancellationToken cancellationToken = default);
        Task<Truck?> GetTruckWithCurrentLoadAsync(int truckId, CancellationToken cancellationToken = default);

        // Performance Analytics
        Task<IEnumerable<Truck>> GetTrucksByPerformanceAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<Dictionary<int, decimal>> GetTruckLoadCapacityUtilizationAsync(DateTime date, CancellationToken cancellationToken = default);

        // Load Management Integration
        Task<bool> HasActiveLoadAsync(int truckId, CancellationToken cancellationToken = default);
        Task<decimal> GetTotalLoadWeightAsync(int truckId, DateTime date, CancellationToken cancellationToken = default);
        Task<int> GetDailyTripCountAsync(int truckId, DateTime date, CancellationToken cancellationToken = default);

        // Driver Management
        Task<IEnumerable<Truck>> GetTrucksByDriverAsync(string driverName, CancellationToken cancellationToken = default);
        Task<bool> IsDriverAssignedAsync(string driverName, CancellationToken cancellationToken = default);

        // Advanced Queries for Reconciliation
        Task<IEnumerable<Truck>> GetTrucksRequiringReconciliationAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<Dictionary<int, (decimal LoadWeight, decimal SoldWeight)>> GetDailyWeightComparisonAsync(DateTime date, CancellationToken cancellationToken = default);

        // Maintenance and Status Tracking
        Task<bool> UpdateTruckStatusBatchAsync(IEnumerable<int> truckIds, bool isActive, CancellationToken cancellationToken = default);
        Task<IEnumerable<Truck>> GetTrucksWithMinimalLoadHistoryAsync(int dayThreshold, CancellationToken cancellationToken = default);
    }
}