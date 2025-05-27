using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Data;
using PoultrySlaughterPOS.Models;
using System.Text.Json;

namespace PoultrySlaughterPOS.Services.Repositories
{
    /// <summary>
    /// Enterprise-grade daily reconciliation repository implementation providing advanced variance analysis
    /// and operational efficiency metrics for comprehensive truck loading reconciliation
    /// </summary>
    public class DailyReconciliationRepository : Repository<DailyReconciliation>, IDailyReconciliationRepository
    {
        public DailyReconciliationRepository(PoultryDbContext context, ILogger<DailyReconciliationRepository> logger)
            : base(context, logger)
        {
        }

        #region Core Reconciliation Operations

        public async Task<DailyReconciliation?> GetTruckReconciliationAsync(int truckId, DateTime reconciliationDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var targetDate = reconciliationDate.Date;
                return await _dbSet
                    .Include(dr => dr.Truck)
                    .FirstOrDefaultAsync(dr => dr.TruckId == truckId && dr.ReconciliationDate.Date == targetDate, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving reconciliation for truck {TruckId} on date {Date}", truckId, reconciliationDate);
                throw;
            }
        }

        public async Task<IEnumerable<DailyReconciliation>> GetDailyReconciliationsAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                var targetDate = date.Date;
                return await _dbSet
                    .Include(dr => dr.Truck)
                    .Where(dr => dr.ReconciliationDate.Date == targetDate)
                    .OrderBy(dr => dr.TruckId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving daily reconciliations for date {Date}", date);
                throw;
            }
        }

        public async Task<bool> CreateReconciliationRecordAsync(int truckId, DateTime date, decimal loadWeight, decimal soldWeight, CancellationToken cancellationToken = default)
        {
            try
            {
                var wastageWeight = loadWeight - soldWeight;
                var wastagePercentage = loadWeight > 0 ? (wastageWeight / loadWeight) * 100 : 0;

                var reconciliation = new DailyReconciliation
                {
                    TruckId = truckId,
                    ReconciliationDate = date.Date,
                    LoadWeight = loadWeight,
                    SoldWeight = soldWeight,
                    WastageWeight = wastageWeight,
                    WastagePercentage = wastagePercentage,
                    Status = "COMPLETED",
                    CreatedDate = DateTime.Now
                };

                await _dbSet.AddAsync(reconciliation, cancellationToken).ConfigureAwait(false);
                var rowsAffected = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Created reconciliation record for truck {TruckId} on date {Date}. Wastage: {WastagePercentage}%",
                    truckId, date, wastagePercentage);

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating reconciliation record for truck {TruckId} on date {Date}", truckId, date);
                throw;
            }
        }

        public async Task<bool> IsReconciliationCompleteAsync(int truckId, DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                var targetDate = date.Date;
                return await _dbSet
                    .AnyAsync(dr => dr.TruckId == truckId && dr.ReconciliationDate.Date == targetDate, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking reconciliation completion for truck {TruckId} on date {Date}", truckId, date);
                throw;
            }
        }

        #endregion

        #region Wastage Analysis and Variance Management

        public async Task<decimal> CalculateWastagePercentageAsync(decimal loadWeight, decimal soldWeight)
        {
            try
            {
                if (loadWeight <= 0) return 0;
                var wastageWeight = loadWeight - soldWeight;
                return (wastageWeight / loadWeight) * 100;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating wastage percentage for load {LoadWeight} and sold {SoldWeight}", loadWeight, soldWeight);
                throw;
            }
        }

        public async Task<decimal> GetDailyWastagePercentageAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                var targetDate = date.Date;
                var reconciliations = await _dbSet
                    .Where(dr => dr.ReconciliationDate.Date == targetDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (!reconciliations.Any()) return 0;

                var totalLoadWeight = reconciliations.Sum(r => r.LoadWeight);
                var totalSoldWeight = reconciliations.Sum(r => r.SoldWeight);

                return totalLoadWeight > 0 ? ((totalLoadWeight - totalSoldWeight) / totalLoadWeight) * 100 : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating daily wastage percentage for date {Date}", date);
                throw;
            }
        }

        public async Task<Dictionary<int, decimal>> GetTruckWastageAnalysisAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromDate = startDate.Date;
                var toDate = endDate.Date.AddDays(1);

                var wastageAnalysis = await _dbSet
                    .Where(dr => dr.ReconciliationDate >= fromDate && dr.ReconciliationDate < toDate)
                    .GroupBy(dr => dr.TruckId)
                    .Select(g => new
                    {
                        TruckId = g.Key,
                        AverageWastage = g.Average(dr => dr.WastagePercentage)
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return wastageAnalysis.ToDictionary(wa => wa.TruckId, wa => wa.AverageWastage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing truck wastage analysis from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<IEnumerable<DailyReconciliation>> GetHighWastageReconciliationsAsync(decimal wastageThreshold, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Include(dr => dr.Truck)
                    .Where(dr => dr.WastagePercentage >= wastageThreshold)
                    .OrderByDescending(dr => dr.WastagePercentage)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving high wastage reconciliations above threshold {Threshold}", wastageThreshold);
                throw;
            }
        }

        #endregion

        #region Performance Monitoring and KPI Tracking

        public async Task<(decimal TotalLoadWeight, decimal TotalSoldWeight, decimal TotalWastage)> GetPeriodReconciliationSummaryAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromDate = startDate.Date;
                var toDate = endDate.Date.AddDays(1);

                var summary = await _dbSet
                    .Where(dr => dr.ReconciliationDate >= fromDate && dr.ReconciliationDate < toDate)
                    .GroupBy(dr => 1)
                    .Select(g => new
                    {
                        TotalLoadWeight = g.Sum(dr => dr.LoadWeight),
                        TotalSoldWeight = g.Sum(dr => dr.SoldWeight),
                        TotalWastage = g.Sum(dr => dr.WastageWeight)
                    })
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                return (summary?.TotalLoadWeight ?? 0, summary?.TotalSoldWeight ?? 0, summary?.TotalWastage ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating period reconciliation summary from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<Dictionary<int, (decimal AverageWastage, int ReconciliationCount)>> GetTruckPerformanceMetricsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromDate = startDate.Date;
                var toDate = endDate.Date.AddDays(1);

                var metrics = await _dbSet
                    .Where(dr => dr.ReconciliationDate >= fromDate && dr.ReconciliationDate < toDate)
                    .GroupBy(dr => dr.TruckId)
                    .Select(g => new
                    {
                        TruckId = g.Key,
                        AverageWastage = g.Average(dr => dr.WastagePercentage),
                        ReconciliationCount = g.Count()
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return metrics.ToDictionary(m => m.TruckId, m => (m.AverageWastage, m.ReconciliationCount));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating truck performance metrics from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<Dictionary<DateTime, decimal>> GetWastageTrendAnalysisAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromDate = startDate.Date;
                var toDate = endDate.Date.AddDays(1);

                var trends = await _dbSet
                    .Where(dr => dr.ReconciliationDate >= fromDate && dr.ReconciliationDate < toDate)
                    .GroupBy(dr => dr.ReconciliationDate.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        AverageWastage = g.Average(dr => dr.WastagePercentage)
                    })
                    .OrderBy(x => x.Date)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return trends.ToDictionary(t => t.Date, t => t.AverageWastage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing wastage trend analysis from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        #endregion

        #region Status Management and Workflow Control

        public async Task<IEnumerable<DailyReconciliation>> GetReconciliationsByStatusAsync(string status, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Include(dr => dr.Truck)
                    .Where(dr => dr.Status == status)
                    .OrderByDescending(dr => dr.ReconciliationDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving reconciliations by status {Status}", status);
                throw;
            }
        }

        public async Task<bool> UpdateReconciliationStatusAsync(int reconciliationId, string newStatus, string notes, CancellationToken cancellationToken = default)
        {
            try
            {
                var reconciliation = await _dbSet
                    .FirstOrDefaultAsync(dr => dr.ReconciliationId == reconciliationId, cancellationToken)
                    .ConfigureAwait(false);

                if (reconciliation == null)
                {
                    _logger.LogWarning("Reconciliation {ReconciliationId} not found for status update", reconciliationId);
                    return false;
                }

                reconciliation.Status = newStatus;
                reconciliation.Notes = notes;

                var rowsAffected = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Updated status for reconciliation {ReconciliationId} to {NewStatus}", reconciliationId, newStatus);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for reconciliation {ReconciliationId} to {NewStatus}", reconciliationId, newStatus);
                throw;
            }
        }

        public async Task<IEnumerable<DailyReconciliation>> GetPendingReconciliationsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Include(dr => dr.Truck)
                    .Where(dr => dr.Status == "PENDING")
                    .OrderBy(dr => dr.ReconciliationDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending reconciliations");
                throw;
            }
        }

        public async Task<int> GetPendingReconciliationCountAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .CountAsync(dr => dr.Status == "PENDING", cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting pending reconciliations");
                throw;
            }
        }

        #endregion

        #region Advanced Analytics and Reporting

        public async Task<IEnumerable<DailyReconciliation>> GetReconciliationsRequiringReviewAsync(decimal criticalWastageThreshold, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Include(dr => dr.Truck)
                    .Where(dr => dr.WastagePercentage >= criticalWastageThreshold || dr.Status == "PENDING")
                    .OrderByDescending(dr => dr.WastagePercentage)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving reconciliations requiring review with threshold {Threshold}", criticalWastageThreshold);
                throw;
            }
        }

        public async Task<Dictionary<string, decimal>> GetWastageDistributionAnalysisAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromDate = startDate.Date;
                var toDate = endDate.Date.AddDays(1);

                var reconciliations = await _dbSet
                    .Where(dr => dr.ReconciliationDate >= fromDate && dr.ReconciliationDate < toDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var distribution = new Dictionary<string, decimal>
                {
                    ["0-2%"] = reconciliations.Count(r => r.WastagePercentage >= 0 && r.WastagePercentage < 2),
                    ["2-5%"] = reconciliations.Count(r => r.WastagePercentage >= 2 && r.WastagePercentage < 5),
                    ["5-10%"] = reconciliations.Count(r => r.WastagePercentage >= 5 && r.WastagePercentage < 10),
                    ["10%+"] = reconciliations.Count(r => r.WastagePercentage >= 10)
                };

                return distribution;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing wastage distribution analysis from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<IEnumerable<DailyReconciliation>> GetBestPerformingReconciliationsAsync(int topCount, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromDate = startDate.Date;
                var toDate = endDate.Date.AddDays(1);

                return await _dbSet
                    .Include(dr => dr.Truck)
                    .Where(dr => dr.ReconciliationDate >= fromDate && dr.ReconciliationDate < toDate)
                    .OrderBy(dr => dr.WastagePercentage)
                    .Take(topCount)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving best performing reconciliations from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<decimal> GetTruckEfficiencyRatingAsync(int truckId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromDate = startDate.Date;
                var toDate = endDate.Date.AddDays(1);

                var avgWastage = await _dbSet
                    .Where(dr => dr.TruckId == truckId && dr.ReconciliationDate >= fromDate && dr.ReconciliationDate < toDate)
                    .AverageAsync(dr => dr.WastagePercentage, cancellationToken)
                    .ConfigureAwait(false);

                // Convert wastage to efficiency (lower wastage = higher efficiency)
                return Math.Max(0, 100 - avgWastage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating truck efficiency rating for truck {TruckId} from {StartDate} to {EndDate}", truckId, startDate, endDate);
                return 0;
            }
        }

        #endregion

        #region Variance Investigation and Root Cause Analysis

        public async Task<IEnumerable<DailyReconciliation>> GetVarianceAnomaliesAsync(decimal standardDeviationThreshold, CancellationToken cancellationToken = default)
        {
            try
            {
                var last30Days = DateTime.Today.AddDays(-30);
                var reconciliations = await _dbSet
                    .Where(dr => dr.ReconciliationDate >= last30Days)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (!reconciliations.Any()) return Enumerable.Empty<DailyReconciliation>();

                var avgWastage = reconciliations.Average(r => (double)r.WastagePercentage);
                var variance = reconciliations.Average(r => Math.Pow((double)r.WastagePercentage - avgWastage, 2));
                var standardDeviation = Math.Sqrt(variance);

                var threshold = avgWastage + (standardDeviationThreshold * standardDeviation);

                return reconciliations
                    .Where(r => (double)r.WastagePercentage > threshold)
                    .OrderByDescending(r => r.WastagePercentage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving variance anomalies with threshold {Threshold}", standardDeviationThreshold);
                throw;
            }
        }

        public async Task<Dictionary<int, List<DailyReconciliation>>> GetConsistentVariancePatternsByTruckAsync(decimal varianceThreshold, int dayRange, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromDate = DateTime.Today.AddDays(-dayRange);
                var reconciliations = await _dbSet
                    .Include(dr => dr.Truck)
                    .Where(dr => dr.ReconciliationDate >= fromDate && dr.WastagePercentage >= varianceThreshold)
                    .OrderByDescending(dr => dr.ReconciliationDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return reconciliations
                    .GroupBy(r => r.TruckId)
                    .Where(g => g.Count() >= 3) // At least 3 occurrences to be considered a pattern
                    .ToDictionary(g => g.Key, g => g.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving consistent variance patterns with threshold {Threshold} over {DayRange} days", varianceThreshold, dayRange);
                throw;
            }
        }

        public async Task<bool> FlagReconciliationForInvestigationAsync(int reconciliationId, string investigationReason, CancellationToken cancellationToken = default)
        {
            try
            {
                var reconciliation = await _dbSet
                    .FirstOrDefaultAsync(dr => dr.ReconciliationId == reconciliationId, cancellationToken)
                    .ConfigureAwait(false);

                if (reconciliation == null)
                {
                    _logger.LogWarning("Reconciliation {ReconciliationId} not found for investigation flagging", reconciliationId);
                    return false;
                }

                reconciliation.Status = "UNDER_INVESTIGATION";
                reconciliation.Notes = $"Investigation: {investigationReason}. Previous notes: {reconciliation.Notes}";

                var rowsAffected = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogWarning("Flagged reconciliation {ReconciliationId} for investigation: {Reason}", reconciliationId, investigationReason);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flagging reconciliation {ReconciliationId} for investigation", reconciliationId);
                throw;
            }
        }

        #endregion

        #region Operational Efficiency Metrics

        public async Task<decimal> GetOperationalEfficiencyRateAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromDate = startDate.Date;
                var toDate = endDate.Date.AddDays(1);

                var avgWastagePercentage = await _dbSet
                    .Where(dr => dr.ReconciliationDate >= fromDate && dr.ReconciliationDate < toDate)
                    .AverageAsync(dr => dr.WastagePercentage, cancellationToken)
                    .ConfigureAwait(false);

                // Convert to efficiency rate (100% - wastage%)
                return Math.Max(0, 100 - avgWastagePercentage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating operational efficiency rate from {StartDate} to {EndDate}", startDate, endDate);
                return 0;
            }
        }

        public async Task<Dictionary<int, decimal>> GetTruckUtilizationRatesAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromDate = startDate.Date;
                var toDate = endDate.Date.AddDays(1);

                var utilization = await _dbSet
                    .Where(dr => dr.ReconciliationDate >= fromDate && dr.ReconciliationDate < toDate)
                    .GroupBy(dr => dr.TruckId)
                    .Select(g => new
                    {
                        TruckId = g.Key,
                        UtilizationRate = 100 - g.Average(dr => dr.WastagePercentage)
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return utilization.ToDictionary(u => u.TruckId, u => Math.Max(0, u.UtilizationRate));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating truck utilization rates from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<(decimal BestEfficiency, decimal WorstEfficiency, decimal AverageEfficiency)> GetEfficiencyBenchmarksAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromDate = startDate.Date;
                var toDate = endDate.Date.AddDays(1);

                var wastageStats = await _dbSet
                    .Where(dr => dr.ReconciliationDate >= fromDate && dr.ReconciliationDate < toDate)
                    .GroupBy(dr => 1)
                    .Select(g => new
                    {
                        MinWastage = g.Min(dr => dr.WastagePercentage),
                        MaxWastage = g.Max(dr => dr.WastagePercentage),
                        AvgWastage = g.Average(dr => dr.WastagePercentage)
                    })
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (wastageStats == null)
                    return (0, 0, 0);

                return (100 - wastageStats.MinWastage, 100 - wastageStats.MaxWastage, 100 - wastageStats.AvgWastage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating efficiency benchmarks from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        #endregion

        #region Financial Impact Assessment

        public async Task<decimal> CalculateFinancialImpactOfWastageAsync(decimal wastageWeight, decimal unitPrice)
        {
            try
            {
                return wastageWeight * unitPrice;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating financial impact of wastage {WastageWeight} at price {UnitPrice}", wastageWeight, unitPrice);
                throw;
            }
        }

        public async Task<Dictionary<DateTime, decimal>> GetDailyWastageFinancialImpactAsync(DateTime startDate, DateTime endDate, decimal averageUnitPrice, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromDate = startDate.Date;
                var toDate = endDate.Date.AddDays(1);

                var dailyWastage = await _dbSet
                    .Where(dr => dr.ReconciliationDate >= fromDate && dr.ReconciliationDate < toDate)
                    .GroupBy(dr => dr.ReconciliationDate.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        TotalWastageWeight = g.Sum(dr => dr.WastageWeight)
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return dailyWastage.ToDictionary(
                    dw => dw.Date,
                    dw => dw.TotalWastageWeight * averageUnitPrice
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating daily wastage financial impact from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<decimal> GetTotalWastageFinancialLossAsync(DateTime startDate, DateTime endDate, decimal unitPrice, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromDate = startDate.Date;
                var toDate = endDate.Date.AddDays(1);

                var totalWastageWeight = await _dbSet
                    .Where(dr => dr.ReconciliationDate >= fromDate && dr.ReconciliationDate < toDate)
                    .SumAsync(dr => dr.WastageWeight, cancellationToken)
                    .ConfigureAwait(false);

                return totalWastageWeight * unitPrice;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating total wastage financial loss from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        #endregion

        #region Data Quality and Integrity Management

        public async Task<bool> ValidateReconciliationDataIntegrityAsync(int reconciliationId, CancellationToken cancellationToken = default)
        {
            try
            {
                var reconciliation = await _dbSet
                    .FirstOrDefaultAsync(dr => dr.ReconciliationId == reconciliationId, cancellationToken)
                    .ConfigureAwait(false);

                if (reconciliation == null)
                    return false;

                // Validate data integrity
                var isValid = reconciliation.LoadWeight >= 0 &&
                             reconciliation.SoldWeight >= 0 &&
                             reconciliation.WastageWeight >= 0 &&
                             reconciliation.LoadWeight >= reconciliation.SoldWeight &&
                             Math.Abs(reconciliation.WastageWeight - (reconciliation.LoadWeight - reconciliation.SoldWeight)) < 0.01m;

                // Validate percentage calculation
                if (reconciliation.LoadWeight > 0)
                {
                    var expectedPercentage = (reconciliation.WastageWeight / reconciliation.LoadWeight) * 100;
                    isValid = isValid && Math.Abs(reconciliation.WastagePercentage - expectedPercentage) < 0.01m;
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating reconciliation data integrity for {ReconciliationId}", reconciliationId);
                throw;
            }
        }

        public async Task<IEnumerable<DailyReconciliation>> GetIncompleteReconciliationsAsync(int dayThreshold, CancellationToken cancellationToken = default)
        {
            try
            {
                var cutoffDate = DateTime.Today.AddDays(-dayThreshold);

                return await _dbSet
                    .Include(dr => dr.Truck)
                    .Where(dr => dr.ReconciliationDate <= cutoffDate && dr.Status == "PENDING")
                    .OrderBy(dr => dr.ReconciliationDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving incomplete reconciliations older than {DayThreshold} days", dayThreshold);
                throw;
            }
        }

        public async Task<bool> RecalculateReconciliationMetricsAsync(int reconciliationId, CancellationToken cancellationToken = default)
        {
            try
            {
                var reconciliation = await _dbSet
                    .FirstOrDefaultAsync(dr => dr.ReconciliationId == reconciliationId, cancellationToken)
                    .ConfigureAwait(false);

                if (reconciliation == null)
                    return false;

                // Recalculate metrics
                reconciliation.WastageWeight = reconciliation.LoadWeight - reconciliation.SoldWeight;
                reconciliation.WastagePercentage = reconciliation.LoadWeight > 0
                    ? (reconciliation.WastageWeight / reconciliation.LoadWeight) * 100
                    : 0;

                var rowsAffected = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Recalculated metrics for reconciliation {ReconciliationId}", reconciliationId);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recalculating reconciliation metrics for {ReconciliationId}", reconciliationId);
                throw;
            }
        }

        #endregion

        #region Historical Analysis and Predictive Insights

        public async Task<Dictionary<int, decimal>> GetSeasonalWastagePatternsByTruckAsync(int truckId, int monthRange, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromDate = DateTime.Today.AddMonths(-monthRange);

                var patterns = await _dbSet
                    .Where(dr => dr.TruckId == truckId && dr.ReconciliationDate >= fromDate)
                    .GroupBy(dr => dr.ReconciliationDate.Month)
                    .Select(g => new
                    {
                        Month = g.Key,
                        AverageWastage = g.Average(dr => dr.WastagePercentage)
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return patterns.ToDictionary(p => p.Month, p => p.AverageWastage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving seasonal wastage patterns for truck {TruckId} over {MonthRange} months", truckId, monthRange);
                throw;
            }
        }

        public async Task<decimal> PredictExpectedWastageAsync(int truckId, decimal loadWeight, DateTime forecastDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var last30Days = DateTime.Today.AddDays(-30);
                var historicalData = await _dbSet
                    .Where(dr => dr.TruckId == truckId && dr.ReconciliationDate >= last30Days)
                    .Select(dr => dr.WastagePercentage)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (!historicalData.Any())
                    return 0;

                // Simple average-based prediction
                var avgWastagePercentage = historicalData.Average();
                var predictedWastageWeight = loadWeight * (avgWastagePercentage / 100);

                return predictedWastageWeight;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error predicting expected wastage for truck {TruckId} with load {LoadWeight}", truckId, loadWeight);
                throw;
            }
        }

        public async Task<IEnumerable<DailyReconciliation>> GetHistoricalPerformanceBaselineAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromDate = startDate.Date;
                var toDate = endDate.Date.AddDays(1);

                return await _dbSet
                    .Include(dr => dr.Truck)
                    .Where(dr => dr.ReconciliationDate >= fromDate && dr.ReconciliationDate < toDate)
                    .OrderBy(dr => dr.ReconciliationDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving historical performance baseline from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        #endregion

        #region Bulk Operations and Performance Optimization

        public async Task<IEnumerable<DailyReconciliation>> CreateReconciliationBatchAsync(IEnumerable<DailyReconciliation> reconciliations, CancellationToken cancellationToken = default)
        {
            try
            {
                var reconciliationsList = reconciliations.ToList();

                // Calculate metrics for each reconciliation
                foreach (var reconciliation in reconciliationsList)
                {
                    reconciliation.WastageWeight = reconciliation.LoadWeight - reconciliation.SoldWeight;
                    reconciliation.WastagePercentage = reconciliation.LoadWeight > 0
                        ? (reconciliation.WastageWeight / reconciliation.LoadWeight) * 100
                        : 0;
                    reconciliation.CreatedDate = DateTime.Now;
                }

                await _dbSet.AddRangeAsync(reconciliationsList, cancellationToken).ConfigureAwait(false);
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Created {Count} reconciliations in batch operation", reconciliationsList.Count);
                return reconciliationsList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating reconciliation batch");
                throw;
            }
        }

        public async Task<int> UpdateReconciliationStatusBatchAsync(IEnumerable<int> reconciliationIds, string status, CancellationToken cancellationToken = default)
        {
            try
            {
                var idsList = reconciliationIds.ToList();
                var reconciliations = await _dbSet
                    .Where(dr => idsList.Contains(dr.ReconciliationId))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                foreach (var reconciliation in reconciliations)
                {
                    reconciliation.Status = status;
                }

                var rowsAffected = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Updated status to {Status} for {Count} reconciliations", status, reconciliations.Count);
                return rowsAffected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating reconciliation status batch to {Status}", status);
                throw;
            }
        }

        public async Task<Dictionary<int, DailyReconciliation>> GetMultipleTruckReconciliationsAsync(IEnumerable<int> truckIds, DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                var truckIdsList = truckIds.ToList();
                var targetDate = date.Date;

                var reconciliations = await _dbSet
                    .Include(dr => dr.Truck)
                    .Where(dr => truckIdsList.Contains(dr.TruckId) && dr.ReconciliationDate.Date == targetDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return reconciliations.ToDictionary(r => r.TruckId, r => r);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving multiple truck reconciliations for date {Date}", date);
                throw;
            }
        }

        #endregion

        #region Compliance and Audit Support

        public async Task<IEnumerable<DailyReconciliation>> GetReconciliationsForAuditAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromDate = startDate.Date;
                var toDate = endDate.Date.AddDays(1);

                return await _dbSet
                    .Include(dr => dr.Truck)
                    .Where(dr => dr.ReconciliationDate >= fromDate && dr.ReconciliationDate < toDate)
                    .OrderBy(dr => dr.ReconciliationDate)
                    .ThenBy(dr => dr.TruckId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving reconciliations for audit from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<bool> GenerateReconciliationAuditTrailAsync(int reconciliationId, CancellationToken cancellationToken = default)
        {
            try
            {
                var reconciliation = await _dbSet
                    .Include(dr => dr.Truck)
                    .FirstOrDefaultAsync(dr => dr.ReconciliationId == reconciliationId, cancellationToken)
                    .ConfigureAwait(false);

                if (reconciliation == null)
                    return false;

                var auditTrail = new
                {
                    ReconciliationId = reconciliation.ReconciliationId,
                    TruckNumber = reconciliation.Truck.TruckNumber,
                    ReconciliationDate = reconciliation.ReconciliationDate,
                    LoadWeight = reconciliation.LoadWeight,
                    SoldWeight = reconciliation.SoldWeight,
                    WastageWeight = reconciliation.WastageWeight,
                    WastagePercentage = reconciliation.WastagePercentage,
                    Status = reconciliation.Status,
                    CreatedDate = reconciliation.CreatedDate,
                    Notes = reconciliation.Notes
                };

                var auditJson = JsonSerializer.Serialize(auditTrail, new JsonSerializerOptions { WriteIndented = true });

                // In a real implementation, this would be saved to an audit table or file
                _logger.LogInformation("Generated audit trail for reconciliation {ReconciliationId}: {AuditTrail}", reconciliationId, auditJson);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating audit trail for reconciliation {ReconciliationId}", reconciliationId);
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetReconciliationComplianceMetricsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var fromDate = startDate.Date;
                var toDate = endDate.Date.AddDays(1);

                var reconciliations = await _dbSet
                    .Where(dr => dr.ReconciliationDate >= fromDate && dr.ReconciliationDate < toDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var metrics = new Dictionary<string, int>
                {
                    ["TotalReconciliations"] = reconciliations.Count,
                    ["CompletedReconciliations"] = reconciliations.Count(r => r.Status == "COMPLETED"),
                    ["PendingReconciliations"] = reconciliations.Count(r => r.Status == "PENDING"),
                    ["HighWastageReconciliations"] = reconciliations.Count(r => r.WastagePercentage > 10),
                    ["UnderInvestigation"] = reconciliations.Count(r => r.Status == "UNDER_INVESTIGATION")
                };

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating reconciliation compliance metrics from {StartDate} to {EndDate}", startDate, endDate);
                throw;
            }
        }

        #endregion
    }
}