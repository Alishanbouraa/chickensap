using PoultrySlaughterPOS.Models;

namespace PoultrySlaughterPOS.Services.Repositories
{
    /// <summary>
    /// Truck repository interface providing domain-specific operations for truck management.
    /// Extends base repository with specialized queries for truck loading and performance tracking.
    /// </summary>
    public interface ITruckRepository : IRepository<Truck>
    {
        // Domain-specific queries for truck operations
        Task<IEnumerable<Truck>> GetActiveTrucksAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Truck>> GetTrucksWithLoadsAsync(DateTime? loadDate = null, CancellationToken cancellationToken = default);
        Task<Truck?> GetTruckByNumberAsync(string truckNumber, CancellationToken cancellationToken = default);
        Task<Truck?> GetTruckWithCurrentLoadAsync(int truckId, CancellationToken cancellationToken = default);

        // Load management specific operations
        Task<IEnumerable<Truck>> GetTrucksForLoadingAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Truck>> GetTrucksInTransitAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Truck>> GetCompletedTrucksAsync(DateTime date, CancellationToken cancellationToken = default);

        // Performance and analytics queries
        Task<Dictionary<int, decimal>> GetTruckLoadCapacityAsync(IEnumerable<int> truckIds, CancellationToken cancellationToken = default);
        Task<IEnumerable<(Truck Truck, int LoadCount, decimal TotalWeight)>> GetTruckPerformanceAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
        Task<bool> IsTruckAvailableForLoadingAsync(int truckId, CancellationToken cancellationToken = default);

        // Validation and business rule queries
        Task<bool> TruckNumberExistsAsync(string truckNumber, int? excludeTruckId = null, CancellationToken cancellationToken = default);
        Task<int> GetActiveTruckCountAsync(CancellationToken cancellationToken = default);

        // Reconciliation support queries
        Task<IEnumerable<Truck>> GetTrucksRequiringReconciliationAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<(decimal LoadedWeight, decimal SoldWeight)> GetTruckWeightSummaryAsync(int truckId, DateTime date, CancellationToken cancellationToken = default);
    }
}