using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Data;
using PoultrySlaughterPOS.Models;
using PoultrySlaughterPOS.Services.Repositories;
using System.Linq.Expressions;

namespace PoultrySlaughterPOS.Repositories
{
    /// <summary>
    /// Truck repository implementation providing optimized data access for truck operations.
    /// Implements domain-specific business logic for truck loading, performance tracking, and reconciliation.
    /// Thread-safe implementation using DbContextFactory pattern for concurrent POS terminal access.
    /// </summary>
    public class TruckRepository : BaseRepository<Truck, int>, ITruckRepository
    {
        public TruckRepository(IDbContextFactory<PoultryDbContext> contextFactory, ILogger<TruckRepository> logger)
            : base(contextFactory, logger)
        {
        }

        protected override DbSet<Truck> GetDbSet(PoultryDbContext context) => context.Trucks;
        protected override Expression<Func<Truck, bool>> GetByIdPredicate(int id) => truck => truck.TruckId == id;

        #region IRepository<Truck> Base Interface Implementation

        public async Task<Truck?> GetAsync(Expression<Func<Truck, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                return await context.Trucks.AsNoTracking()
                    .FirstOrDefaultAsync(predicate, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving truck with predicate");
                throw;
            }
        }

        public async Task<IEnumerable<Truck>> GetPagedAsync(
            int pageNumber,
            int pageSize,
            Expression<Func<Truck, bool>>? filter = null,
            Func<IQueryable<Truck>, IOrderedQueryable<Truck>>? orderBy = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var query = context.Trucks.AsNoTracking();

                if (filter != null)
                    query = query.Where(filter);

                if (orderBy != null)
                    query = orderBy(query);
                else
                    query = query.OrderBy(t => t.TruckNumber);

                return await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged trucks");
                throw;
            }
        }

        public async Task<IEnumerable<TResult>> SelectAsync<TResult>(
            Expression<Func<Truck, TResult>> selector,
            Expression<Func<Truck, bool>>? predicate = null,
            CancellationToken cancellationToken = default) where TResult : class
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var query = context.Trucks.AsNoTracking();

                if (predicate != null)
                    query = query.Where(predicate);

                return await query
                    .Select(selector)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting projected truck entities");
                throw;
            }
        }

        #endregion

        #region Domain-Specific Queries

        public async Task<IEnumerable<Truck>> GetActiveTrucksAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                return await context.Trucks
                    .AsNoTracking()
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
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Trucks
                    .AsNoTracking()
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

        public async Task<Truck?> GetTruckByNumberAsync(string truckNumber, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                return await context.Trucks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TruckNumber == truckNumber, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving truck by number {TruckNumber}", truckNumber);
                throw;
            }
        }

        public async Task<Truck?> GetTruckWithCurrentLoadAsync(int truckId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var today = DateTime.Today;
                return await context.Trucks
                    .AsNoTracking()
                    .Include(t => t.TruckLoads.Where(tl => tl.LoadDate.Date == today))
                    .FirstOrDefaultAsync(t => t.TruckId == truckId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving truck {TruckId} with current load", truckId);
                throw;
            }
        }

        #endregion

        #region Load Management Operations

        public async Task<IEnumerable<Truck>> GetTrucksForLoadingAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var today = DateTime.Today;
                return await context.Trucks
                    .AsNoTracking()
                    .Where(t => t.IsActive && !t.TruckLoads.Any(tl => tl.LoadDate.Date == today && tl.Status == "LOADED"))
                    .OrderBy(t => t.TruckNumber)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trucks available for loading");
                throw;
            }
        }

        public async Task<IEnumerable<Truck>> GetTrucksInTransitAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var today = DateTime.Today;
                return await context.Trucks
                    .AsNoTracking()
                    .Include(t => t.TruckLoads.Where(tl => tl.LoadDate.Date == today && tl.Status == "IN_TRANSIT"))
                    .Where(t => t.IsActive && t.TruckLoads.Any(tl => tl.LoadDate.Date == today && tl.Status == "IN_TRANSIT"))
                    .OrderBy(t => t.TruckNumber)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trucks in transit");
                throw;
            }
        }

        public async Task<IEnumerable<Truck>> GetCompletedTrucksAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var targetDate = date.Date;
                return await context.Trucks
                    .AsNoTracking()
                    .Include(t => t.TruckLoads.Where(tl => tl.LoadDate.Date == targetDate && tl.Status == "COMPLETED"))
                    .Where(t => t.TruckLoads.Any(tl => tl.LoadDate.Date == targetDate && tl.Status == "COMPLETED"))
                    .OrderBy(t => t.TruckNumber)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving completed trucks for date {Date}", date);
                throw;
            }
        }

        #endregion

        #region Performance and Analytics

        public async Task<Dictionary<int, decimal>> GetTruckLoadCapacityAsync(IEnumerable<int> truckIds, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var truckIdsList = truckIds.ToList();
                var last30Days = DateTime.Today.AddDays(-30);

                var capacityData = await context.TruckLoads
                    .AsNoTracking()
                    .Where(tl => truckIdsList.Contains(tl.TruckId) && tl.LoadDate >= last30Days)
                    .GroupBy(tl => tl.TruckId)
                    .Select(g => new { TruckId = g.Key, AverageCapacity = g.Average(tl => tl.TotalWeight) })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return capacityData.ToDictionary(cd => cd.TruckId, cd => cd.AverageCapacity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating truck load capacity for trucks {TruckIds}", string.Join(",", truckIds));
                throw;
            }
        }

        public async Task<IEnumerable<(Truck Truck, int LoadCount, decimal TotalWeight)>> GetTruckPerformanceAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var performanceQuery = await context.Trucks
                    .AsNoTracking()
                    .Where(t => t.IsActive)
                    .Select(t => new
                    {
                        Truck = t,
                        LoadCount = t.TruckLoads.Count(tl => tl.LoadDate >= fromDate && tl.LoadDate <= toDate),
                        TotalWeight = t.TruckLoads
                            .Where(tl => tl.LoadDate >= fromDate && tl.LoadDate <= toDate)
                            .Sum(tl => tl.TotalWeight)
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return performanceQuery.Select(pq => (pq.Truck, pq.LoadCount, pq.TotalWeight));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving truck performance data from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        public async Task<bool> IsTruckAvailableForLoadingAsync(int truckId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var truck = await context.Trucks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TruckId == truckId, cancellationToken)
                    .ConfigureAwait(false);

                if (truck == null || !truck.IsActive)
                    return false;

                var today = DateTime.Today;
                var hasActiveLoad = await context.TruckLoads
                    .AsNoTracking()
                    .AnyAsync(tl => tl.TruckId == truckId &&
                                    tl.LoadDate.Date == today &&
                                    (tl.Status == "LOADED" || tl.Status == "IN_TRANSIT"),
                             cancellationToken)
                    .ConfigureAwait(false);

                return !hasActiveLoad;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking truck {TruckId} availability for loading", truckId);
                throw;
            }
        }

        #endregion

        #region Validation and Business Rules

        public async Task<bool> TruckNumberExistsAsync(string truckNumber, int? excludeTruckId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Trucks.AsNoTracking().Where(t => t.TruckNumber == truckNumber);

                if (excludeTruckId.HasValue)
                    query = query.Where(t => t.TruckId != excludeTruckId.Value);

                return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if truck number {TruckNumber} exists", truckNumber);
                throw;
            }
        }

        public async Task<int> GetActiveTruckCountAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                return await context.Trucks
                    .AsNoTracking()
                    .CountAsync(t => t.IsActive, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting active trucks");
                throw;
            }
        }

        #endregion

        #region Reconciliation Support

        public async Task<IEnumerable<Truck>> GetTrucksRequiringReconciliationAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var targetDate = date.Date;
                return await context.Trucks
                    .AsNoTracking()
                    .Include(t => t.TruckLoads.Where(tl => tl.LoadDate.Date == targetDate))
                    .Include(t => t.Invoices.Where(i => i.InvoiceDate.Date == targetDate))
                    .Where(t => t.TruckLoads.Any(tl => tl.LoadDate.Date == targetDate && tl.Status == "COMPLETED") &&
                               !t.DailyReconciliations.Any(dr => dr.ReconciliationDate.Date == targetDate))
                    .OrderBy(t => t.TruckNumber)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trucks requiring reconciliation for date {Date}", date);
                throw;
            }
        }

        public async Task<(decimal LoadedWeight, decimal SoldWeight)> GetTruckWeightSummaryAsync(int truckId, DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var targetDate = date.Date;

                // Get loaded weight from truck loads
                var loadedWeight = await context.TruckLoads
                    .AsNoTracking()
                    .Where(tl => tl.TruckId == truckId && tl.LoadDate.Date == targetDate)
                    .SumAsync(tl => tl.TotalWeight, cancellationToken)
                    .ConfigureAwait(false);

                // Get sold weight from invoices
                var soldWeight = await context.Invoices
                    .AsNoTracking()
                    .Where(i => i.TruckId == truckId && i.InvoiceDate.Date == targetDate)
                    .SumAsync(i => i.NetWeight, cancellationToken)
                    .ConfigureAwait(false);

                return (loadedWeight, soldWeight);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving weight summary for truck {TruckId} on date {Date}", truckId, date);
                throw;
            }
        }

        #endregion
    }
}