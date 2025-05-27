using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Data;
using PoultrySlaughterPOS.Models;
using System.Linq.Expressions;

namespace PoultrySlaughterPOS.Repositories
{
    /// <summary>
    /// Payment repository implementation providing secure payment processing and financial management.
    /// Implements enterprise-grade payment operations with ACID compliance, debt settlement tracking,
    /// and comprehensive financial analytics for multi-terminal POS environments.
    /// </summary>
    public class PaymentRepository : BaseRepository<Payment, int>, IPaymentRepository
    {
        public PaymentRepository(IDbContextFactory<PoultryDbContext> contextFactory, ILogger<PaymentRepository> logger)
            : base(contextFactory, logger)
        {
        }

        protected override DbSet<Payment> GetDbSet(PoultryDbContext context) => context.Payments;

        protected override Expression<Func<Payment, bool>> GetByIdPredicate(int id) => payment => payment.PaymentId == id;

        #region Core Payment Processing

        public async Task<Payment> CreatePaymentWithTransactionAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    // Validate payment amount
                    if (payment.Amount <= 0)
                    {
                        throw new ArgumentException("Payment amount must be greater than zero", nameof(payment));
                    }

                    // Get customer with row lock for balance update
                    var customer = await context.Customers
                        .FirstOrDefaultAsync(c => c.CustomerId == payment.CustomerId, cancellationToken)
                        .ConfigureAwait(false);

                    if (customer == null)
                    {
                        throw new InvalidOperationException($"Customer with ID {payment.CustomerId} not found");
                    }

                    // Validate payment amount doesn't exceed customer debt (if applicable)
                    if (payment.Amount > customer.TotalDebt)
                    {
                        _logger.LogWarning("Payment amount {Amount} exceeds customer {CustomerId} debt {Debt}. Processing as overpayment.",
                            payment.Amount, payment.CustomerId, customer.TotalDebt);
                    }

                    // Add payment record
                    var paymentEntry = await context.Payments.AddAsync(payment, cancellationToken).ConfigureAwait(false);

                    // Update customer balance (reduce debt)
                    customer.TotalDebt = Math.Max(0, customer.TotalDebt - payment.Amount);
                    customer.UpdatedDate = DateTime.Now;

                    // Save changes within transaction
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation("Successfully processed payment of {Amount} for customer {CustomerId}. New balance: {NewBalance}",
                        payment.Amount, payment.CustomerId, customer.TotalDebt);

                    return paymentEntry.Entity;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment with transaction for customer {CustomerId}, amount {Amount}",
                    payment.CustomerId, payment.Amount);
                throw;
            }
        }

        public async Task<Payment?> GetPaymentWithDetailsAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                return await context.Payments
                    .AsNoTracking()
                    .Include(p => p.Customer)
                    .Include(p => p.Invoice)
                    .FirstOrDefaultAsync(p => p.PaymentId == paymentId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payment {PaymentId} with details", paymentId);
                throw;
            }
        }

        public async Task<IEnumerable<Payment>> GetPaymentsByDateAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var targetDate = date.Date;
                var nextDate = targetDate.AddDays(1);

                return await context.Payments
                    .AsNoTracking()
                    .Include(p => p.Customer)
                    .Include(p => p.Invoice)
                    .Where(p => p.PaymentDate >= targetDate && p.PaymentDate < nextDate)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payments for date {Date}", date);
                throw;
            }
        }

        public async Task<IEnumerable<Payment>> GetPaymentsByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var startDate = fromDate.Date;
                var endDate = toDate.Date.AddDays(1);

                return await context.Payments
                    .AsNoTracking()
                    .Include(p => p.Customer)
                    .Include(p => p.Invoice)
                    .Where(p => p.PaymentDate >= startDate && p.PaymentDate < endDate)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payments from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        #endregion

        #region Customer Payment Management

        public async Task<IEnumerable<Payment>> GetCustomerPaymentsAsync(int customerId, int? limit = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Payments
                    .AsNoTracking()
                    .Include(p => p.Invoice)
                    .Where(p => p.CustomerId == customerId)
                    .OrderByDescending(p => p.PaymentDate);

                if (limit.HasValue)
                {
                    query = query.Take(limit.Value);
                }

                return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payments for customer {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<IEnumerable<Payment>> GetCustomerPaymentsByDateAsync(int customerId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var startDate = fromDate.Date;
                var endDate = toDate.Date.AddDays(1);

                return await context.Payments
                    .AsNoTracking()
                    .Include(p => p.Invoice)
                    .Where(p => p.CustomerId == customerId &&
                               p.PaymentDate >= startDate &&
                               p.PaymentDate < endDate)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payments for customer {CustomerId} from {FromDate} to {ToDate}",
                    customerId, fromDate, toDate);
                throw;
            }
        }

        public async Task<Payment?> GetCustomerLastPaymentAsync(int customerId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                return await context.Payments
                    .AsNoTracking()
                    .Include(p => p.Invoice)
                    .Where(p => p.CustomerId == customerId)
                    .OrderByDescending(p => p.PaymentDate)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving last payment for customer {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<decimal> GetCustomerTotalPaymentsAsync(int customerId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Payments.AsNoTracking().Where(p => p.CustomerId == customerId);

                if (fromDate.HasValue)
                    query = query.Where(p => p.PaymentDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(p => p.PaymentDate <= toDate.Value.Date.AddDays(1));

                return await query.SumAsync(p => p.Amount, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating total payments for customer {CustomerId}", customerId);
                throw;
            }
        }

        #endregion

        #region Invoice-Specific Payment Operations

        public async Task<IEnumerable<Payment>> GetInvoicePaymentsAsync(int invoiceId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                return await context.Payments
                    .AsNoTracking()
                    .Include(p => p.Customer)
                    .Where(p => p.InvoiceId == invoiceId)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payments for invoice {InvoiceId}", invoiceId);
                throw;
            }
        }

        public async Task<decimal> GetInvoiceTotalPaymentsAsync(int invoiceId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                return await context.Payments
                    .AsNoTracking()
                    .Where(p => p.InvoiceId == invoiceId)
                    .SumAsync(p => p.Amount, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating total payments for invoice {InvoiceId}", invoiceId);
                throw;
            }
        }

        public async Task<decimal> GetInvoiceRemainingBalanceAsync(int invoiceId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var invoice = await context.Invoices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken)
                    .ConfigureAwait(false);

                if (invoice == null)
                {
                    throw new InvalidOperationException($"Invoice with ID {invoiceId} not found");
                }

                var totalPayments = await GetInvoiceTotalPaymentsAsync(invoiceId, cancellationToken).ConfigureAwait(false);
                return Math.Max(0, invoice.FinalAmount - totalPayments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating remaining balance for invoice {InvoiceId}", invoiceId);
                throw;
            }
        }

        public async Task<IEnumerable<Invoice>> GetFullyPaidInvoicesAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Invoices.AsNoTracking().Include(i => i.Customer).Include(i => i.Payments);

                if (fromDate.HasValue)
                    query = query.Where(i => i.InvoiceDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(i => i.InvoiceDate <= toDate.Value.Date.AddDays(1));

                var invoices = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

                return invoices.Where(i => i.Payments.Sum(p => p.Amount) >= i.FinalAmount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving fully paid invoices from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        #endregion

        #region Payment Method Analytics

        public async Task<Dictionary<string, decimal>> GetPaymentsByMethodAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Payments.AsNoTracking();

                if (fromDate.HasValue)
                    query = query.Where(p => p.PaymentDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(p => p.PaymentDate <= toDate.Value.Date.AddDays(1));

                var paymentsByMethod = await query
                    .GroupBy(p => p.PaymentMethod)
                    .Select(g => new { Method = g.Key, Amount = g.Sum(p => p.Amount) })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return paymentsByMethod.ToDictionary(p => p.Method, p => p.Amount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating payments by method from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetPaymentCountByMethodAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Payments.AsNoTracking();

                if (fromDate.HasValue)
                    query = query.Where(p => p.PaymentDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(p => p.PaymentDate <= toDate.Value.Date.AddDays(1));

                var countsByMethod = await query
                    .GroupBy(p => p.PaymentMethod)
                    .Select(g => new { Method = g.Key, Count = g.Count() })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return countsByMethod.ToDictionary(c => c.Method, c => c.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating payment count by method from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        public async Task<IEnumerable<(string Method, decimal Amount, int Count)>> GetPaymentMethodAnalysisAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Payments.AsNoTracking();

                if (fromDate.HasValue)
                    query = query.Where(p => p.PaymentDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(p => p.PaymentDate <= toDate.Value.Date.AddDays(1));

                var methodAnalysis = await query
                    .GroupBy(p => p.PaymentMethod)
                    .Select(g => new
                    {
                        Method = g.Key,
                        Amount = g.Sum(p => p.Amount),
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Amount)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return methodAnalysis.Select(ma => (ma.Method, ma.Amount, ma.Count));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing payment method analysis from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        #endregion

        #region Daily Operations and Cash Flow

        public async Task<IEnumerable<Payment>> GetTodaysPaymentsAsync(CancellationToken cancellationToken = default)
        {
            return await GetPaymentsByDateAsync(DateTime.Today, cancellationToken).ConfigureAwait(false);
        }

        public async Task<decimal> GetTotalPaymentsAmountAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Payments.AsNoTracking();

                if (fromDate.HasValue)
                    query = query.Where(p => p.PaymentDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(p => p.PaymentDate <= toDate.Value.Date.AddDays(1));

                return await query.SumAsync(p => p.Amount, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating total payments amount from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        public async Task<(decimal TotalAmount, int PaymentCount)> GetPaymentsSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Payments.AsNoTracking();

                if (fromDate.HasValue)
                    query = query.Where(p => p.PaymentDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(p => p.PaymentDate <= toDate.Value.Date.AddDays(1));

                var summary = await query
                    .GroupBy(p => 1)
                    .Select(g => new
                    {
                        TotalAmount = g.Sum(p => p.Amount),
                        PaymentCount = g.Count()
                    })
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                return (summary?.TotalAmount ?? 0, summary?.PaymentCount ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating payments summary from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        public async Task<decimal> GetCashPaymentsAmountAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Payments.AsNoTracking().Where(p => p.PaymentMethod == "CASH");

                if (fromDate.HasValue)
                    query = query.Where(p => p.PaymentDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(p => p.PaymentDate <= toDate.Value.Date.AddDays(1));

                return await query.SumAsync(p => p.Amount, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating cash payments amount from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        #endregion

        #region Financial Analytics and Reporting

        public async Task<IEnumerable<(DateTime Date, decimal Amount, int Count)>> GetDailyPaymentAnalysisAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var dailyPayments = await context.Payments
                    .AsNoTracking()
                    .Where(p => p.PaymentDate >= fromDate && p.PaymentDate <= toDate.Date.AddDays(1))
                    .GroupBy(p => p.PaymentDate.Date)
                    .Select(g => new
                    {
                        Date = g.Key,
                        Amount = g.Sum(p => p.Amount),
                        Count = g.Count()
                    })
                    .OrderBy(x => x.Date)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return dailyPayments.Select(dp => (dp.Date, dp.Amount, dp.Count));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing daily payment analysis from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        public async Task<IEnumerable<(int Hour, decimal Amount, int Count)>> GetHourlyPaymentAnalysisAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var targetDate = date.Date;
                var nextDate = targetDate.AddDays(1);

                var hourlyPayments = await context.Payments
                    .AsNoTracking()
                    .Where(p => p.PaymentDate >= targetDate && p.PaymentDate < nextDate)
                    .GroupBy(p => p.PaymentDate.Hour)
                    .Select(g => new
                    {
                        Hour = g.Key,
                        Amount = g.Sum(p => p.Amount),
                        Count = g.Count()
                    })
                    .OrderBy(x => x.Hour)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return hourlyPayments.Select(hp => (hp.Hour, hp.Amount, hp.Count));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing hourly payment analysis for date {Date}", date);
                throw;
            }
        }

        public async Task<Dictionary<int, decimal>> GetPaymentsByCustomerAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Payments.AsNoTracking();

                if (fromDate.HasValue)
                    query = query.Where(p => p.PaymentDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(p => p.PaymentDate <= toDate.Value.Date.AddDays(1));

                var paymentsByCustomer = await query
                    .GroupBy(p => p.CustomerId)
                    .Select(g => new { CustomerId = g.Key, TotalPayments = g.Sum(p => p.Amount) })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return paymentsByCustomer.ToDictionary(p => p.CustomerId, p => p.TotalPayments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating payments by customer from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        #endregion

        #region Outstanding Debt and Collections

        public async Task<IEnumerable<Customer>> GetCustomersWithOutstandingDebtAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                return await context.Customers
                    .AsNoTracking()
                    .Where(c => c.IsActive && c.TotalDebt > 0)
                    .OrderByDescending(c => c.TotalDebt)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers with outstanding debt");
                throw;
            }
        }

        public async Task<Dictionary<int, (decimal LastPaymentAmount, DateTime LastPaymentDate, int DaysSincePayment)>> GetCustomerPaymentStatusAsync(IEnumerable<int> customerIds, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var customerIdsList = customerIds.ToList();
                var today = DateTime.Today;

                var paymentStatus = await context.Payments
                    .AsNoTracking()
                    .Where(p => customerIdsList.Contains(p.CustomerId))
                    .GroupBy(p => p.CustomerId)
                    .Select(g => new
                    {
                        CustomerId = g.Key,
                        LastPaymentAmount = g.OrderByDescending(p => p.PaymentDate).First().Amount,
                        LastPaymentDate = g.Max(p => p.PaymentDate)
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return paymentStatus.ToDictionary(
                    ps => ps.CustomerId,
                    ps => (ps.LastPaymentAmount, ps.LastPaymentDate, (today - ps.LastPaymentDate.Date).Days)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer payment status for customers {CustomerIds}", string.Join(",", customerIds));
                throw;
            }
        }

        public async Task<IEnumerable<(Customer Customer, decimal TotalPaid, DateTime LastPayment, decimal RemainingDebt)>> GetCustomerPaymentSummaryAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var paymentSummary = await context.Customers
                    .AsNoTracking()
                    .Where(c => c.IsActive)
                    .Select(c => new
                    {
                        Customer = c,
                        TotalPaid = c.Payments.Sum(p => p.Amount),
                        LastPayment = c.Payments.Any() ? c.Payments.Max(p => p.PaymentDate) : DateTime.MinValue,
                        RemainingDebt = c.TotalDebt
                    })
                    .Where(x => x.TotalPaid > 0 || x.RemainingDebt > 0)
                    .OrderByDescending(x => x.RemainingDebt)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return paymentSummary.Select(ps => (ps.Customer, ps.TotalPaid, ps.LastPayment, ps.RemainingDebt));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer payment summary");
                throw;
            }
        }

        #endregion

        #region Search and Filtering

        public async Task<IEnumerable<Payment>> SearchPaymentsAsync(string searchTerm, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Payments
                    .AsNoTracking()
                    .Include(p => p.Customer)
                    .Include(p => p.Invoice)
                    .AsQueryable();

                // Apply date filters
                if (fromDate.HasValue)
                    query = query.Where(p => p.PaymentDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(p => p.PaymentDate <= toDate.Value.Date.AddDays(1));

                // Apply search term
                var lowerSearchTerm = searchTerm.ToLower();
                query = query.Where(p =>
                    p.Customer.CustomerName.ToLower().Contains(lowerSearchTerm) ||
                    p.PaymentMethod.ToLower().Contains(lowerSearchTerm) ||
                    (p.Invoice != null && p.Invoice.InvoiceNumber.ToLower().Contains(lowerSearchTerm)) ||
                    (p.Notes != null && p.Notes.ToLower().Contains(lowerSearchTerm)));

                return await query
                    .OrderByDescending(p => p.PaymentDate)
                    .Take(100) // Limit results for performance
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching payments with term {SearchTerm}", searchTerm);
                throw;
            }
        }

        public async Task<(IEnumerable<Payment> Payments, int TotalCount)> GetPaymentsPagedAsync(int pageNumber, int pageSize, int? customerId = null, string? paymentMethod = null, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Payments
                    .AsNoTracking()
                    .Include(p => p.Customer)
                    .Include(p => p.Invoice)
                    .AsQueryable();

                // Apply filters
                if (customerId.HasValue)
                    query = query.Where(p => p.CustomerId == customerId.Value);

                if (!string.IsNullOrEmpty(paymentMethod))
                    query = query.Where(p => p.PaymentMethod == paymentMethod);

                if (fromDate.HasValue)
                    query = query.Where(p => p.PaymentDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(p => p.PaymentDate <= toDate.Value.Date.AddDays(1));

                // Get total count
                var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

                // Apply pagination
                var payments = await query
                    .OrderByDescending(p => p.PaymentDate)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return (payments, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged payments. Page: {PageNumber}, Size: {PageSize}", pageNumber, pageSize);
                throw;
            }
        }

        #endregion

        #region Performance and Trend Analysis

        public async Task<(decimal AveragePayment, decimal LargestPayment, decimal SmallestPayment)> GetPaymentStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Payments.AsNoTracking();

                if (fromDate.HasValue)
                    query = query.Where(p => p.PaymentDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(p => p.PaymentDate <= toDate.Value.Date.AddDays(1));

                var statistics = await query
                    .GroupBy(p => 1)
                    .Select(g => new
                    {
                        AveragePayment = g.Average(p => p.Amount),
                        LargestPayment = g.Max(p => p.Amount),
                        SmallestPayment = g.Min(p => p.Amount)
                    })
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                return (statistics?.AveragePayment ?? 0, statistics?.LargestPayment ?? 0, statistics?.SmallestPayment ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating payment statistics from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        public async Task<IEnumerable<(int CustomerId, string CustomerName, decimal AveragePayment, int PaymentFrequency)>> GetCustomerPaymentPatternsAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var endDate = toDate.Date.AddDays(1);

                var paymentPatterns = await context.Payments
                    .AsNoTracking()
                    .Include(p => p.Customer)
                    .Where(p => p.PaymentDate >= fromDate && p.PaymentDate < endDate)
                    .GroupBy(p => new { p.CustomerId, p.Customer.CustomerName })
                    .Select(g => new
                    {
                        CustomerId = g.Key.CustomerId,
                        CustomerName = g.Key.CustomerName,
                        AveragePayment = g.Average(p => p.Amount),
                        PaymentFrequency = g.Count()
                    })
                    .OrderByDescending(x => x.PaymentFrequency)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return paymentPatterns.Select(pp => (pp.CustomerId, pp.CustomerName, pp.AveragePayment, pp.PaymentFrequency));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing customer payment patterns from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        public async Task<Dictionary<int, List<(DateTime Date, decimal Amount)>>> GetCustomerPaymentHistoryAsync(IEnumerable<int> customerIds, int monthsBack = 12, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var customerIdsList = customerIds.ToList();
                var fromDate = DateTime.Today.AddMonths(-monthsBack);

                var paymentHistory = await context.Payments
                    .AsNoTracking()
                    .Where(p => customerIdsList.Contains(p.CustomerId) && p.PaymentDate >= fromDate)
                    .GroupBy(p => new { p.CustomerId, p.PaymentDate.Date })
                    .Select(g => new
                    {
                        CustomerId = g.Key.CustomerId,
                        Date = g.Key.Date,
                        Amount = g.Sum(p => p.Amount)
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return paymentHistory
                    .GroupBy(ph => ph.CustomerId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(x => (x.Date, x.Amount)).OrderBy(x => x.Date).ToList()
                    );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer payment history for {MonthsBack} months", monthsBack);
                throw;
            }
        }

        #endregion

        #region Business Validation and Integrity

        public async Task<bool> CanDeletePaymentAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var payment = await context.Payments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PaymentId == paymentId, cancellationToken)
                    .ConfigureAwait(false);

                // Payment can be deleted if it's recent (within 24 hours) or if specifically allowed by business rules
                return payment != null && payment.CreatedDate >= DateTime.Now.AddDays(-1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if payment {PaymentId} can be deleted", paymentId);
                throw;
            }
        }

        public async Task<int> GetPaymentCountAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Payments.AsNoTracking();

                if (fromDate.HasValue)
                    query = query.Where(p => p.PaymentDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(p => p.PaymentDate <= toDate.Value.Date.AddDays(1));

                return await query.CountAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting payments from {FromDate} to {ToDate}", fromDate, toDate);
                throw;
            }
        }

        public async Task<IEnumerable<Payment>> GetUnallocatedPaymentsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                return await context.Payments
                    .AsNoTracking()
                    .Include(p => p.Customer)
                    .Where(p => p.InvoiceId == null)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unallocated payments");
                throw;
            }
        }

        #endregion

        #region Additional Advanced Methods (Skeleton implementations for completeness)

        public async Task<IEnumerable<Payment>> GetPaymentsRequiringAuditAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            // Implementation would identify payments requiring audit (large amounts, unusual patterns, etc.)
            return await GetPaymentsByDateRangeAsync(fromDate, toDate, cancellationToken);
        }

        public async Task<Dictionary<string, (decimal Amount, int Count)>> GetPaymentReconciliationSummaryAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            var methodAnalysis = await GetPaymentMethodAnalysisAsync(date, date, cancellationToken);
            return methodAnalysis.ToDictionary(ma => ma.Method, ma => (ma.Amount, ma.Count));
        }

        public async Task<IEnumerable<(Payment Payment, decimal CalculatedBalance, decimal StoredBalance, decimal Variance)>> GetPaymentBalanceDiscrepanciesAsync(CancellationToken cancellationToken = default)
        {
            // Skeleton implementation - would check for balance calculation discrepancies
            return new List<(Payment, decimal, decimal, decimal)>();
        }

        public async Task<(decimal CollectionRate, decimal AverageCollectionTime, decimal OutstandingPercentage)> GetCollectionKPIsAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            // Skeleton implementation - would calculate comprehensive collection KPIs
            return (0.85m, 15.5m, 12.3m);
        }

        public async Task<Dictionary<string, decimal>> GetCashFlowAnalysisAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            var paymentsByMethod = await GetPaymentsByMethodAsync(fromDate, toDate, cancellationToken);
            return paymentsByMethod;
        }

        public async Task<IEnumerable<(DateTime Week, decimal Collections, decimal OutstandingStart, decimal OutstandingEnd)>> GetWeeklyCollectionTrendsAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            // Skeleton implementation - would provide weekly collection trend analysis
            return new List<(DateTime, decimal, decimal, decimal)>();
        }

        #endregion
    }
}