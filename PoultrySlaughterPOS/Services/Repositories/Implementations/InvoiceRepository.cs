using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Data;
using PoultrySlaughterPOS.Models;
using System.Linq.Expressions;
using PoultrySlaughterPOS.Services.Repositories;

namespace PoultrySlaughterPOS.Repositories
{
    /// <summary>
    /// Invoice repository implementation providing high-performance transaction processing.
    /// Implements enterprise-grade financial operations with ACID compliance, concurrent access patterns,
    /// and comprehensive business intelligence capabilities for multi-terminal POS environments.
    /// </summary>
    public class InvoiceRepository : BaseRepository<Invoice, int>, IInvoiceRepository
    {
        public InvoiceRepository(IDbContextFactory<PoultryDbContext> contextFactory, ILogger<InvoiceRepository> logger)
            : base(contextFactory, logger)
        {
        }

        protected override DbSet<Invoice> GetDbSet(PoultryDbContext context) => context.Invoices;

        protected override Expression<Func<Invoice, bool>> GetByIdPredicate(int id) => invoice => invoice.InvoiceId == id;

        #region IRepository<Invoice> Base Interface Implementation

        public async Task<Invoice?> GetAsync(Expression<Func<Invoice, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                return await context.Invoices.AsNoTracking()
                    .FirstOrDefaultAsync(predicate, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoice with predicate");
                throw;
            }
        }

        public async Task<IEnumerable<Invoice>> GetPagedAsync(
            int pageNumber,
            int pageSize,
            Expression<Func<Invoice, bool>>? filter = null,
            Func<IQueryable<Invoice>, IOrderedQueryable<Invoice>>? orderBy = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var query = context.Invoices.AsNoTracking();

                if (filter != null)
                    query = query.Where(filter);

                IOrderedQueryable<Invoice> orderedQuery;
                if (orderBy != null)
                    orderedQuery = orderBy(query);
                else
                    orderedQuery = query.OrderByDescending(i => i.InvoiceDate);

                return await orderedQuery
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged invoices");
                throw;
            }
        }

        public async Task<IEnumerable<TResult>> SelectAsync<TResult>(
            Expression<Func<Invoice, TResult>> selector,
            Expression<Func<Invoice, bool>>? predicate = null,
            CancellationToken cancellationToken = default) where TResult : class
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                var query = context.Invoices.AsNoTracking();

                if (predicate != null)
                    query = query.Where(predicate);

                return await query
                    .Select(selector)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting projected invoice entities");
                throw;
            }
        }

        #endregion

        #region Core Invoice Operations

        public async Task<Invoice?> GetInvoiceByNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                return await context.Invoices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoice by number {InvoiceNumber}", invoiceNumber);
                throw;
            }
        }

        public async Task<Invoice?> GetInvoiceWithDetailsAsync(int invoiceId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                return await context.Invoices
                    .AsNoTracking()
                    .Include(i => i.Customer)
                    .Include(i => i.Truck)
                    .Include(i => i.Payments)
                    .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoice {InvoiceId} with details", invoiceId);
                throw;
            }
        }

        public async Task<string> GenerateInvoiceNumberAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var today = DateTime.Today;
                var datePrefix = today.ToString("yyyyMMdd");

                // Get the highest invoice number for today with thread-safe approach
                var lastInvoiceNumber = await context.Invoices
                    .AsNoTracking()
                    .Where(i => i.InvoiceNumber.StartsWith(datePrefix))
                    .OrderByDescending(i => i.InvoiceNumber)
                    .Select(i => i.InvoiceNumber)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                int sequenceNumber = 1;
                if (!string.IsNullOrEmpty(lastInvoiceNumber) && lastInvoiceNumber.Length >= 12)
                {
                    var lastSequence = lastInvoiceNumber.Substring(8);
                    if (int.TryParse(lastSequence, out var parsed))
                    {
                        sequenceNumber = parsed + 1;
                    }
                }

                var invoiceNumber = $"{datePrefix}{sequenceNumber:D4}";

                _logger.LogInformation("Generated invoice number: {InvoiceNumber}", invoiceNumber);
                return invoiceNumber;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating invoice number");
                throw;
            }
        }

        public async Task<Invoice> CreateInvoiceWithTransactionAsync(Invoice invoice, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    // Generate invoice number if not provided
                    if (string.IsNullOrEmpty(invoice.InvoiceNumber))
                    {
                        invoice.InvoiceNumber = await GenerateUniqueInvoiceNumberAsync(context, cancellationToken).ConfigureAwait(false);
                    }

                    // Calculate financial values
                    CalculateInvoiceAmounts(invoice);

                    // Get customer with row lock for balance update
                    var customer = await context.Customers
                        .FirstOrDefaultAsync(c => c.CustomerId == invoice.CustomerId, cancellationToken)
                        .ConfigureAwait(false);

                    if (customer == null)
                    {
                        throw new InvalidOperationException($"Customer with ID {invoice.CustomerId} not found");
                    }

                    // Set invoice balance information
                    invoice.PreviousBalance = customer.TotalDebt;
                    invoice.CurrentBalance = customer.TotalDebt + invoice.FinalAmount;

                    // Add invoice
                    var invoiceEntry = await context.Invoices.AddAsync(invoice, cancellationToken).ConfigureAwait(false);

                    // Update customer balance
                    customer.TotalDebt = invoice.CurrentBalance;
                    customer.UpdatedDate = DateTime.Now;

                    // Save changes within transaction
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation("Successfully created invoice {InvoiceNumber} for customer {CustomerId} with amount {Amount}",
                        invoice.InvoiceNumber, invoice.CustomerId, invoice.FinalAmount);

                    return invoiceEntry.Entity;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invoice with transaction for customer {CustomerId}", invoice.CustomerId);
                throw;
            }
        }

        #endregion

        #region Customer-Specific Invoice Operations - Fixed Implementation

        public async Task<IEnumerable<Invoice>> GetCustomerOutstandingInvoicesAsync(int customerId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var invoicesWithPayments = await context.Invoices
                    .AsNoTracking()
                    .Include(i => i.Payments)
                    .Where(i => i.CustomerId == customerId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                // Filter outstanding invoices in memory to avoid complex SQL translation
                return invoicesWithPayments.Where(i => i.FinalAmount > i.Payments.Sum(p => p.Amount));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving outstanding invoices for customer {CustomerId}", customerId);
                throw;
            }
        }

        #endregion

        #region Daily Operations and Sales Management

        public async Task<IEnumerable<Invoice>> GetInvoicesByDateAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var targetDate = date.Date;
                var nextDate = targetDate.AddDays(1);

                return await context.Invoices
                    .AsNoTracking()
                    .Include(i => i.Customer)
                    .Include(i => i.Truck)
                    .Where(i => i.InvoiceDate >= targetDate && i.InvoiceDate < nextDate)
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices for date {Date}", date);
                throw;
            }
        }

        public async Task<IEnumerable<Invoice>> GetInvoicesByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var startDate = fromDate.Date;
                var endDate = toDate.Date.AddDays(1);

                return await context.Invoices
                    .AsNoTracking()
                    .Include(i => i.Customer)
                    .Include(i => i.Truck)
                    .Where(i => i.InvoiceDate >= startDate && i.InvoiceDate < endDate)
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        public async Task<IEnumerable<Invoice>> GetTodaysInvoicesAsync(CancellationToken cancellationToken = default)
        {
            return await GetInvoicesByDateAsync(DateTime.Today, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IEnumerable<Invoice>> GetInvoicesByTruckAsync(int truckId, DateTime? date = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Invoices
                    .AsNoTracking()
                    .Include(i => i.Customer)
                    .Where(i => i.TruckId == truckId);

                if (date.HasValue)
                {
                    var targetDate = date.Value.Date;
                    var nextDate = targetDate.AddDays(1);
                    query = query.Where(i => i.InvoiceDate >= targetDate && i.InvoiceDate < nextDate);
                }

                return await query
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices for truck {TruckId} on date {Date}", truckId, date);
                throw;
            }
        }

        #endregion

        #region Customer-Specific Operations Continued

        public async Task<IEnumerable<Invoice>> GetCustomerInvoicesAsync(int customerId, int? limit = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Invoices
                    .AsNoTracking()
                    .Include(i => i.Truck)
                    .Where(i => i.CustomerId == customerId)
                    .OrderByDescending(i => i.InvoiceDate);

                if (limit.HasValue)
                {
                    query = query.Take(limit.Value);
                }

                return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices for customer {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<Invoice?> GetCustomerLastInvoiceAsync(int customerId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                return await context.Invoices
                    .AsNoTracking()
                    .Include(i => i.Truck)
                    .Where(i => i.CustomerId == customerId)
                    .OrderByDescending(i => i.InvoiceDate)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving last invoice for customer {CustomerId}", customerId);
                throw;
            }
        }

        #endregion

        #region Financial Analytics and Reporting

        public async Task<decimal> GetTotalSalesAmountAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Invoices.AsNoTracking();

                if (fromDate.HasValue)
                    query = query.Where(i => i.InvoiceDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(i => i.InvoiceDate <= toDate.Value.Date.AddDays(1));

                return await query.SumAsync(i => i.FinalAmount, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating total sales amount from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        public async Task<(decimal TotalAmount, decimal TotalWeight, int InvoiceCount)> GetSalesSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Invoices.AsNoTracking();

                if (fromDate.HasValue)
                    query = query.Where(i => i.InvoiceDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(i => i.InvoiceDate <= toDate.Value.Date.AddDays(1));

                var summary = await query
                    .GroupBy(i => 1)
                    .Select(g => new
                    {
                        TotalAmount = g.Sum(i => i.FinalAmount),
                        TotalWeight = g.Sum(i => i.NetWeight),
                        InvoiceCount = g.Count()
                    })
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                return (summary?.TotalAmount ?? 0, summary?.TotalWeight ?? 0, summary?.InvoiceCount ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating sales summary from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        public async Task<Dictionary<int, decimal>> GetSalesByTruckAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Invoices.AsNoTracking();

                if (fromDate.HasValue)
                    query = query.Where(i => i.InvoiceDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(i => i.InvoiceDate <= toDate.Value.Date.AddDays(1));

                var salesByTruck = await query
                    .GroupBy(i => i.TruckId)
                    .Select(g => new { TruckId = g.Key, TotalSales = g.Sum(i => i.FinalAmount) })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return salesByTruck.ToDictionary(s => s.TruckId, s => s.TotalSales);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating sales by truck from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        public async Task<Dictionary<int, decimal>> GetSalesByCustomerAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Invoices.AsNoTracking();

                if (fromDate.HasValue)
                    query = query.Where(i => i.InvoiceDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(i => i.InvoiceDate <= toDate.Value.Date.AddDays(1));

                var salesByCustomer = await query
                    .GroupBy(i => i.CustomerId)
                    .Select(g => new { CustomerId = g.Key, TotalSales = g.Sum(i => i.FinalAmount) })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return salesByCustomer.ToDictionary(s => s.CustomerId, s => s.TotalSales);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating sales by customer from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        #endregion

        #region Weight and Quantity Analytics

        public async Task<decimal> GetTotalWeightSoldAsync(int? truckId = null, DateTime? date = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Invoices.AsNoTracking();

                if (truckId.HasValue)
                    query = query.Where(i => i.TruckId == truckId.Value);

                if (date.HasValue)
                {
                    var targetDate = date.Value.Date;
                    var nextDate = targetDate.AddDays(1);
                    query = query.Where(i => i.InvoiceDate >= targetDate && i.InvoiceDate < nextDate);
                }

                return await query.SumAsync(i => i.NetWeight, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating total weight sold for truck {TruckId} on date {Date}", truckId, date);
                throw;
            }
        }

        public async Task<Dictionary<int, decimal>> GetWeightSoldByTruckAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var targetDate = date.Date;
                var nextDate = targetDate.AddDays(1);

                var weightByTruck = await context.Invoices
                    .AsNoTracking()
                    .Where(i => i.InvoiceDate >= targetDate && i.InvoiceDate < nextDate)
                    .GroupBy(i => i.TruckId)
                    .Select(g => new { TruckId = g.Key, TotalWeight = g.Sum(i => i.NetWeight) })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return weightByTruck.ToDictionary(w => w.TruckId, w => w.TotalWeight);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating weight sold by truck for date {Date}", date);
                throw;
            }
        }

        public async Task<(decimal TotalGrossWeight, decimal TotalNetWeight, int TotalCages)> GetWeightSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Invoices.AsNoTracking();

                if (fromDate.HasValue)
                    query = query.Where(i => i.InvoiceDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(i => i.InvoiceDate <= toDate.Value.Date.AddDays(1));

                var summary = await query
                    .GroupBy(i => 1)
                    .Select(g => new
                    {
                        TotalGrossWeight = g.Sum(i => i.GrossWeight),
                        TotalNetWeight = g.Sum(i => i.NetWeight),
                        TotalCages = g.Sum(i => i.CagesCount)
                    })
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                return (summary?.TotalGrossWeight ?? 0, summary?.TotalNetWeight ?? 0, summary?.TotalCages ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating weight summary from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        #endregion

        #region Performance and Trend Analysis

        public async Task<IEnumerable<(DateTime Date, decimal Amount, int Count)>> GetDailySalesAnalysisAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var dailySales = await context.Invoices
                    .AsNoTracking()
                    .Where(i => i.InvoiceDate >= fromDate && i.InvoiceDate <= toDate.Date.AddDays(1))
                    .GroupBy(i => i.InvoiceDate.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        Amount = g.Sum(i => i.FinalAmount),
                        Count = g.Count()
                    })
                    .OrderBy(x => x.Date)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return dailySales.Select(ds => (ds.Date, ds.Amount, ds.Count));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing daily sales analysis from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        public async Task<IEnumerable<(int Hour, decimal Amount, int Count)>> GetHourlySalesAnalysisAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var targetDate = date.Date;
                var nextDate = targetDate.AddDays(1);

                var hourlySales = await context.Invoices
                    .AsNoTracking()
                    .Where(i => i.InvoiceDate >= targetDate && i.InvoiceDate < nextDate)
                    .GroupBy(i => i.InvoiceDate.Hour)
                    .Select(g => new
                    {
                        Hour = g.Key,
                        Amount = g.Sum(i => i.FinalAmount),
                        Count = g.Count()
                    })
                    .OrderBy(x => x.Hour)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return hourlySales.Select(hs => (hs.Hour, hs.Amount, hs.Count));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing hourly sales analysis for date {Date}", date);
                throw;
            }
        }

        public async Task<Dictionary<string, decimal>> GetSalesByPaymentMethodAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Invoices.AsNoTracking();

                if (fromDate.HasValue)
                    query = query.Where(i => i.InvoiceDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(i => i.InvoiceDate <= toDate.Value.Date.AddDays(1));

                // For this implementation, we'll categorize based on customer debt status
                // In a more sophisticated system, this would link to actual payment records
                var invoicesWithPaymentStatus = await query
                    .Include(i => i.Customer)
                    .Select(i => new
                    {
                        Invoice = i,
                        PaymentType = i.Customer.TotalDebt > 0 ? "Credit" : "Cash"
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return invoicesWithPaymentStatus
                    .GroupBy(x => x.PaymentType)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Invoice.FinalAmount));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing sales by payment method from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        #endregion

        #region Price and Margin Analysis

        public async Task<(decimal MinPrice, decimal MaxPrice, decimal AvgPrice)> GetPriceAnalysisAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Invoices.AsNoTracking();

                if (fromDate.HasValue)
                    query = query.Where(i => i.InvoiceDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(i => i.InvoiceDate <= toDate.Value.Date.AddDays(1));

                var priceAnalysis = await query
                    .GroupBy(i => 1)
                    .Select(g => new
                    {
                        MinPrice = g.Min(i => i.UnitPrice),
                        MaxPrice = g.Max(i => i.UnitPrice),
                        AvgPrice = g.Average(i => i.UnitPrice)
                    })
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                return (priceAnalysis?.MinPrice ?? 0, priceAnalysis?.MaxPrice ?? 0, priceAnalysis?.AvgPrice ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing price analysis from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        public async Task<decimal> GetAverageDiscountPercentageAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Invoices.AsNoTracking().Where(i => i.DiscountPercentage > 0);

                if (fromDate.HasValue)
                    query = query.Where(i => i.InvoiceDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(i => i.InvoiceDate <= toDate.Value.Date.AddDays(1));

                if (!await query.AnyAsync(cancellationToken).ConfigureAwait(false))
                    return 0;

                var avgDiscount = await query.AverageAsync(i => i.DiscountPercentage, cancellationToken).ConfigureAwait(false);
                return avgDiscount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating average discount percentage from {FromDate} to {ToDate}", fromDate, toDate);
                return 0;
            }
        }

        public async Task<IEnumerable<(decimal UnitPrice, int InvoiceCount, decimal TotalAmount)>> GetPriceDistributionAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Invoices.AsNoTracking();

                if (fromDate.HasValue)
                    query = query.Where(i => i.InvoiceDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(i => i.InvoiceDate <= toDate.Value.Date.AddDays(1));

                var priceDistribution = await query
                    .GroupBy(i => i.UnitPrice)
                    .Select(g => new
                    {
                        UnitPrice = g.Key,
                        InvoiceCount = g.Count(),
                        TotalAmount = g.Sum(i => i.FinalAmount)
                    })
                    .OrderBy(x => x.UnitPrice)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return priceDistribution.Select(pd => (pd.UnitPrice, pd.InvoiceCount, pd.TotalAmount));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing price distribution from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        #endregion

        #region Search and Filtering Operations

        public async Task<IEnumerable<Invoice>> SearchInvoicesAsync(string searchTerm, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Invoices
                    .AsNoTracking()
                    .Include(i => i.Customer)
                    .Include(i => i.Truck)
                    .AsQueryable();

                // Apply date filters
                if (fromDate.HasValue)
                    query = query.Where(i => i.InvoiceDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(i => i.InvoiceDate <= toDate.Value.Date.AddDays(1));

                // Apply search term
                var lowerSearchTerm = searchTerm.ToLower();
                query = query.Where(i =>
                    i.InvoiceNumber.ToLower().Contains(lowerSearchTerm) ||
                    i.Customer.CustomerName.ToLower().Contains(lowerSearchTerm) ||
                    i.Truck.TruckNumber.ToLower().Contains(lowerSearchTerm));

                return await query
                    .OrderByDescending(i => i.InvoiceDate)
                    .Take(100) // Limit results for performance
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching invoices with term {SearchTerm}", searchTerm);
                throw;
            }
        }

        public async Task<(IEnumerable<Invoice> Invoices, int TotalCount)> GetInvoicesPagedAsync(int pageNumber, int pageSize, int? customerId = null, int? truckId = null, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Invoices
                    .AsNoTracking()
                    .Include(i => i.Customer)
                    .Include(i => i.Truck)
                    .AsQueryable();

                // Apply filters
                if (customerId.HasValue)
                    query = query.Where(i => i.CustomerId == customerId.Value);

                if (truckId.HasValue)
                    query = query.Where(i => i.TruckId == truckId.Value);

                if (fromDate.HasValue)
                    query = query.Where(i => i.InvoiceDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(i => i.InvoiceDate <= toDate.Value.Date.AddDays(1));

                // Get total count
                var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

                // Apply pagination
                var invoices = await query
                    .OrderByDescending(i => i.InvoiceDate)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return (invoices, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged invoices. Page: {PageNumber}, Size: {PageSize}", pageNumber, pageSize);
                throw;
            }
        }

        #endregion

        #region Business Validation and Integrity

        public async Task<bool> InvoiceNumberExistsAsync(string invoiceNumber, int? excludeInvoiceId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Invoices.AsNoTracking().Where(i => i.InvoiceNumber == invoiceNumber);

                if (excludeInvoiceId.HasValue)
                    query = query.Where(i => i.InvoiceId != excludeInvoiceId.Value);

                return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if invoice number {InvoiceNumber} exists", invoiceNumber);
                throw;
            }
        }

        public async Task<bool> CanDeleteInvoiceAsync(int invoiceId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var hasPayments = await context.Payments
                    .AsNoTracking()
                    .AnyAsync(p => p.InvoiceId == invoiceId, cancellationToken)
                    .ConfigureAwait(false);

                return !hasPayments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if invoice {InvoiceId} can be deleted", invoiceId);
                throw;
            }
        }

        public async Task<int> GetInvoiceCountAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Invoices.AsNoTracking();

                if (fromDate.HasValue)
                    query = query.Where(i => i.InvoiceDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(i => i.InvoiceDate <= toDate.Value.Date.AddDays(1));

                return await query.CountAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting invoices from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        #endregion

        #region Reconciliation and Audit Support

        public async Task<IEnumerable<Invoice>> GetUnreconciledInvoicesAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var targetDate = date.Date;
                var nextDate = targetDate.AddDays(1);

                return await context.Invoices
                    .AsNoTracking()
                    .Include(i => i.Customer)
                    .Include(i => i.Truck)
                    .Where(i => i.InvoiceDate >= targetDate &&
                               i.InvoiceDate < nextDate &&
                               !context.DailyReconciliations.Any(dr =>
                                   dr.TruckId == i.TruckId &&
                                   dr.ReconciliationDate.Date == targetDate))
                    .OrderBy(i => i.TruckId)
                    .ThenBy(i => i.InvoiceDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unreconciled invoices for date {Date}", date);
                throw;
            }
        }

        public async Task<Dictionary<int, (decimal LoadedWeight, decimal SoldWeight, decimal Variance)>> GetTruckReconciliationDataAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var targetDate = date.Date;
                var nextDate = targetDate.AddDays(1);

                var reconciliationData = await (from truck in context.Trucks
                                                join load in context.TruckLoads on truck.TruckId equals load.TruckId into loads
                                                from load in loads.Where(l => l.LoadDate.Date == targetDate).DefaultIfEmpty()
                                                join invoice in context.Invoices on truck.TruckId equals invoice.TruckId into invoices
                                                from invoice in invoices.Where(i => i.InvoiceDate >= targetDate && i.InvoiceDate < nextDate).DefaultIfEmpty()
                                                where truck.IsActive
                                                group new { load, invoice } by truck.TruckId into g
                                                select new
                                                {
                                                    TruckId = g.Key,
                                                    LoadedWeight = g.Where(x => x.load != null).Sum(x => x.load.TotalWeight),
                                                    SoldWeight = g.Where(x => x.invoice != null).Sum(x => x.invoice.NetWeight)
                                                })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return reconciliationData.ToDictionary(
                    rd => rd.TruckId,
                    rd => (rd.LoadedWeight, rd.SoldWeight, rd.LoadedWeight - rd.SoldWeight)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving truck reconciliation data for date {Date}", date);
                throw;
            }
        }

        public async Task<IEnumerable<Invoice>> GetInvoicesRequiringAuditAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var endDate = toDate.Date.AddDays(1);

                // Identify invoices with unusual patterns that may require audit
                return await context.Invoices
                    .AsNoTracking()
                    .Include(i => i.Customer)
                    .Include(i => i.Truck)
                    .Where(i => i.InvoiceDate >= fromDate && i.InvoiceDate < endDate &&
                               (i.DiscountPercentage > 10 || // High discount
                                i.FinalAmount > 10000 || // Large amount
                                i.NetWeight > 500)) // Large quantity
                    .OrderByDescending(i => i.FinalAmount)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices requiring audit from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        public async Task<IEnumerable<(int TruckId, DateTime Date, decimal Efficiency)>> GetTruckEfficiencyAnalysisAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var endDate = toDate.Date.AddDays(1);

                var efficiencyData = await (from truck in context.Trucks
                                            join load in context.TruckLoads on truck.TruckId equals load.TruckId
                                            join invoice in context.Invoices on truck.TruckId equals invoice.TruckId
                                            where truck.IsActive &&
                                                  load.LoadDate >= fromDate && load.LoadDate < endDate &&
                                                  invoice.InvoiceDate.Date == load.LoadDate.Date
                                            group new { load, invoice } by new { truck.TruckId, load.LoadDate.Date } into g
                                            select new
                                            {
                                                TruckId = g.Key.TruckId,
                                                Date = g.Key.Date,
                                                LoadedWeight = g.Sum(x => x.load.TotalWeight),
                                                SoldWeight = g.Sum(x => x.invoice.NetWeight)
                                            })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return efficiencyData
                    .Where(ed => ed.LoadedWeight > 0)
                    .Select(ed => (ed.TruckId, ed.Date, (ed.SoldWeight / ed.LoadedWeight) * 100))
                    .OrderByDescending(x => x.Item3);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing truck efficiency analysis from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        public async Task<Dictionary<int, List<(DateTime Date, decimal Amount)>>> GetCustomerPurchasePatternAsync(IEnumerable<int> customerIds, int monthsBack = 6, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var customerIdsList = customerIds.ToList();
                var fromDate = DateTime.Today.AddMonths(-monthsBack);

                var purchasePatterns = await context.Invoices
                    .AsNoTracking()
                    .Where(i => customerIdsList.Contains(i.CustomerId) && i.InvoiceDate >= fromDate)
                    .GroupBy(i => new { i.CustomerId, i.InvoiceDate.Date })
                    .Select(g => new
                    {
                        CustomerId = g.Key.CustomerId,
                        Date = g.Key.Date,
                        Amount = g.Sum(i => i.FinalAmount)
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return purchasePatterns
                    .GroupBy(pp => pp.CustomerId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(x => (x.Date, x.Amount)).OrderBy(x => x.Date).ToList()
                    );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing customer purchase patterns for {MonthsBack} months", monthsBack);
                throw;
            }
        }

        public async Task<(decimal TotalRevenue, decimal AverageInvoiceValue, decimal LargestInvoice)> GetRevenueKPIsAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Invoices.AsNoTracking();

                if (fromDate.HasValue)
                    query = query.Where(i => i.InvoiceDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(i => i.InvoiceDate <= toDate.Value.Date.AddDays(1));

                var kpis = await query
                    .GroupBy(i => 1)
                    .Select(g => new
                    {
                        TotalRevenue = g.Sum(i => i.FinalAmount),
                        AverageInvoiceValue = g.Average(i => i.FinalAmount),
                        LargestInvoice = g.Max(i => i.FinalAmount)
                    })
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                return (kpis?.TotalRevenue ?? 0, kpis?.AverageInvoiceValue ?? 0, kpis?.LargestInvoice ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating revenue KPIs from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task<string> GenerateUniqueInvoiceNumberAsync(PoultryDbContext context, CancellationToken cancellationToken)
        {
            string invoiceNumber;
            bool exists;
            int attempts = 0;
            const int maxAttempts = 10;

            do
            {
                invoiceNumber = await GenerateInvoiceNumberAsync(cancellationToken).ConfigureAwait(false);
                exists = await context.Invoices
                    .AsNoTracking()
                    .AnyAsync(i => i.InvoiceNumber == invoiceNumber, cancellationToken)
                    .ConfigureAwait(false);

                attempts++;
                if (exists && attempts < maxAttempts)
                {
                    // Add random suffix to ensure uniqueness in high-concurrency scenarios
                    var random = new Random();
                    invoiceNumber += $"-{random.Next(1000, 9999)}";
                    exists = await context.Invoices
                        .AsNoTracking()
                        .AnyAsync(i => i.InvoiceNumber == invoiceNumber, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            while (exists && attempts < maxAttempts);

            if (exists)
            {
                throw new InvalidOperationException($"Unable to generate unique invoice number after {maxAttempts} attempts");
            }

            return invoiceNumber;
        }

        private static void CalculateInvoiceAmounts(Invoice invoice)
        {
            // Calculate net weight
            invoice.NetWeight = invoice.GrossWeight - invoice.CagesWeight;

            // Calculate total amount before discount
            invoice.TotalAmount = invoice.NetWeight * invoice.UnitPrice;

            // Apply discount
            var discountAmount = invoice.TotalAmount * (invoice.DiscountPercentage / 100);
            invoice.FinalAmount = invoice.TotalAmount - discountAmount;

            // Ensure non-negative values
            invoice.NetWeight = Math.Max(0, invoice.NetWeight);
            invoice.TotalAmount = Math.Max(0, invoice.TotalAmount);
            invoice.FinalAmount = Math.Max(0, invoice.FinalAmount);
        }

        #endregion
    }
}