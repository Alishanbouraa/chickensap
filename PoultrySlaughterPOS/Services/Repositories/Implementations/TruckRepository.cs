using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Data;
using PoultrySlaughterPOS.Models;

namespace PoultrySlaughterPOS.Services.Repositories
{
    /// <summary>
    /// Enterprise-grade truck repository implementation providing specialized
    /// truck management operations with performance optimization and comprehensive logging
    /// </summary>
    public class TruckRepository : Repository<Truck>, ITruckRepository
    {
        public TruckRepository(PoultryDbContext context, ILogger<TruckRepository> logger)
            : base(context, logger)
        {
        }

        public async Task<IEnumerable<Truck>> GetActiveTrucksAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.TruckNumber)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active trucks");
                throw;
            }
        }

        public async Task<IEnumerable<Truck>> GetTrucksWithLoadsAsync(DateTime? loadDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var query = _dbSet
                    .Include(t => t.TruckLoads)
                    .Where(t => t.IsActive);

                if (loadDate.HasValue)
                {
                    var targetDate = loadDate.Value.Date;
                    query = query.Where(t => t.TruckLoads.Any(tl => tl.LoadDate.Date == targetDate));
                }

                return await query
                    .OrderBy(t => t.TruckNumber)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trucks with loads for date {LoadDate}", loadDate);
                throw;
            }
        }

        public async Task<Truck?> GetTruckWithCurrentLoadAsync(int truckId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Include(t => t.TruckLoads.Where(tl => tl.LoadDate.Date == DateTime.Today))
                    .FirstOrDefaultAsync(t => t.TruckId == truckId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving truck {TruckId} with current load", truckId);
                throw;
            }
        }

        public async Task<IEnumerable<Truck>> GetTrucksByPerformanceAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Include(t => t.TruckLoads.Where(tl => tl.LoadDate >= startDate && tl.LoadDate <= endDate))
                    .Include(t => t.Invoices.Where(i => i.InvoiceDate >= startDate && i.InvoiceDate <= endDate))
                    .Where(t => t.IsActive)
                    .OrderByDescending(t => t.Invoices.Sum(i => i.FinalAmount))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trucks by performance between {StartDate} and {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<Dictionary<int, decimal>> GetTruckLoadCapacityUtilizationAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                var targetDate = date.Date;
                return await _context.TruckLoads
                    .Where(tl => tl.LoadDate.Date == targetDate)
                    .GroupBy(tl => tl.TruckId)
                    .ToDictionaryAsync(
                        g => g.Key,
                        g => g.Sum(tl => tl.TotalWeight),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating truck load capacity utilization for date {Date}", date);
                throw;
            }
        }

        public async Task<bool> HasActiveLoadAsync(int truckId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.TruckLoads
                    .AnyAsync(tl => tl.TruckId == truckId &&
                                   tl.LoadDate.Date == DateTime.Today &&
                                   tl.Status == "LOADED",
                              cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking active load for truck {TruckId}", truckId);
                throw;
            }
        }

        public async Task<decimal> GetTotalLoadWeightAsync(int truckId, DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                var targetDate = date.Date;
                return await _context.TruckLoads
                    .Where(tl => tl.TruckId == truckId && tl.LoadDate.Date == targetDate)
                    .SumAsync(tl => tl.TotalWeight, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving total load weight for truck {TruckId} on date {Date}", truckId, date);
                throw;
            }
        }

        public async Task<int> GetDailyTripCountAsync(int truckId, DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                var targetDate = date.Date;
                return await _context.TruckLoads
                    .CountAsync(tl => tl.TruckId == truckId && tl.LoadDate.Date == targetDate, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving daily trip count for truck {TruckId} on date {Date}", truckId, date);
                throw;
            }
        }

        public async Task<IEnumerable<Truck>> GetTrucksByDriverAsync(string driverName, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Where(t => t.DriverName.Contains(driverName) && t.IsActive)
                    .OrderBy(t => t.TruckNumber)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trucks by driver {DriverName}", driverName);
                throw;
            }
        }

        public async Task<bool> IsDriverAssignedAsync(string driverName, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .AnyAsync(t => t.DriverName == driverName && t.IsActive, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if driver {DriverName} is assigned", driverName);
                throw;
            }
        }

        public async Task<IEnumerable<Truck>> GetTrucksRequiringReconciliationAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                var targetDate = date.Date;
                return await _dbSet
                    .Include(t => t.TruckLoads.Where(tl => tl.LoadDate.Date == targetDate))
                    .Include(t => t.DailyReconciliations)
                    .Where(t => t.IsActive &&
                               t.TruckLoads.Any(tl => tl.LoadDate.Date == targetDate) &&
                               !t.DailyReconciliations.Any(dr => dr.ReconciliationDate.Date == targetDate))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trucks requiring reconciliation for date {Date}", date);
                throw;
            }
        }

        public async Task<Dictionary<int, (decimal LoadWeight, decimal SoldWeight)>> GetDailyWeightComparisonAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                var targetDate = date.Date;

                var loadWeights = await _context.TruckLoads
                    .Where(tl => tl.LoadDate.Date == targetDate)
                    .GroupBy(tl => tl.TruckId)
                    .ToDictionaryAsync(g => g.Key, g => g.Sum(tl => tl.TotalWeight), cancellationToken)
                    .ConfigureAwait(false);

                var soldWeights = await _context.Invoices
                    .Where(i => i.InvoiceDate.Date == targetDate)
                    .GroupBy(i => i.TruckId)
                    .ToDictionaryAsync(g => g.Key, g => g.Sum(i => i.NetWeight), cancellationToken)
                    .ConfigureAwait(false);

                var result = new Dictionary<int, (decimal LoadWeight, decimal SoldWeight)>();

                foreach (var truckId in loadWeights.Keys.Union(soldWeights.Keys))
                {
                    result[truckId] = (
                        loadWeights.GetValueOrDefault(truckId, 0),
                        soldWeights.GetValueOrDefault(truckId, 0)
                    );
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving daily weight comparison for date {Date}", date);
                throw;
            }
        }

        public async Task<bool> UpdateTruckStatusBatchAsync(IEnumerable<int> truckIds, bool isActive, CancellationToken cancellationToken = default)
        {
            try
            {
                var trucks = await _dbSet
                    .Where(t => truckIds.Contains(t.TruckId))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                foreach (var truck in trucks)
                {
                    truck.IsActive = isActive;
                    truck.UpdatedDate = DateTime.Now;
                }

                _logger.LogDebug("Updated status for {TruckCount} trucks to {Status}", trucks.Count, isActive ? "Active" : "Inactive");
                return trucks.Any();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating truck status batch");
                throw;
            }
        }

        public async Task<IEnumerable<Truck>> GetTrucksWithMinimalLoadHistoryAsync(int dayThreshold, CancellationToken cancellationToken = default)
        {
            try
            {
                var cutoffDate = DateTime.Today.AddDays(-dayThreshold);

                return await _dbSet
                    .Include(t => t.TruckLoads)
                    .Where(t => t.IsActive &&
                               !t.TruckLoads.Any(tl => tl.LoadDate >= cutoffDate))
                    .OrderBy(t => t.TruckNumber)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trucks with minimal load history (threshold: {DayThreshold} days)", dayThreshold);
                throw;
            }
        }
    }
}