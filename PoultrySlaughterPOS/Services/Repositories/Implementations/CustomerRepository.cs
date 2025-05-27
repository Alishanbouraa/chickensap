using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Data;
using PoultrySlaughterPOS.Models;
using System.Linq.Expressions;

namespace PoultrySlaughterPOS.Repositories
{
    /// <summary>
    /// Customer repository implementation providing comprehensive customer account management.
    /// Implements sophisticated financial tracking, debt management, and business intelligence operations
    /// optimized for high-performance POS environments with concurrent access patterns.
    /// </summary>
    public class CustomerRepository : BaseRepository<Customer, int>, ICustomerRepository
    {
        public CustomerRepository(IDbContextFactory<PoultryDbContext> contextFactory, ILogger<CustomerRepository> logger)
            : base(contextFactory, logger)
        {
        }

        protected override DbSet<Customer> GetDbSet(PoultryDbContext context) => context.Customers;

        protected override Expression<Func<Customer, bool>> GetByIdPredicate(int id) => customer => customer.CustomerId == id;

        #region Core Customer Management

        public async Task<IEnumerable<Customer>> GetActiveCustomersAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                return await context.Customers
                    .AsNoTracking()
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.CustomerName)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active customers");
                throw;
            }
        }

        public async Task<Customer?> GetCustomerByNameAsync(string customerName, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                return await context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CustomerName.ToLower() == customerName.ToLower(), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer by name {CustomerName}", customerName);
                throw;
            }
        }

        public async Task<Customer?> GetCustomerByPhoneAsync(string phoneNumber, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                return await context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer by phone {PhoneNumber}", phoneNumber);
                throw;
            }
        }

        public async Task<Customer?> GetCustomerWithTransactionsAsync(int customerId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                return await context.Customers
                    .AsNoTracking()
                    .Include(c => c.Invoices.OrderByDescending(i => i.InvoiceDate).Take(50))
                    .Include(c => c.Payments.OrderByDescending(p => p.PaymentDate).Take(50))
                    .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer {CustomerId} with transactions", customerId);
                throw;
            }
        }

        #endregion

        #region Account Balance and Debt Management

        public async Task<IEnumerable<Customer>> GetCustomersWithDebtAsync(CancellationToken cancellationToken = default)
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
                _logger.LogError(ex, "Error retrieving customers with debt");
                throw;
            }
        }

        public async Task<IEnumerable<Customer>> GetCustomersWithDebtAboveThresholdAsync(decimal threshold, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                return await context.Customers
                    .AsNoTracking()
                    .Where(c => c.IsActive && c.TotalDebt >= threshold)
                    .OrderByDescending(c => c.TotalDebt)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers with debt above threshold {Threshold}", threshold);
                throw;
            }
        }

        public async Task<decimal> GetCustomerTotalDebtAsync(int customerId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var customer = await context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
                    .ConfigureAwait(false);

                return customer?.TotalDebt ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving total debt for customer {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<bool> UpdateCustomerBalanceAsync(int customerId, decimal amount, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var customer = await context.Customers
                    .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
                    .ConfigureAwait(false);

                if (customer == null)
                {
                    _logger.LogWarning("Customer {CustomerId} not found for balance update", customerId);
                    return false;
                }

                customer.TotalDebt += amount;
                customer.UpdatedDate = DateTime.Now;

                var rowsAffected = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Updated balance for customer {CustomerId} by {Amount}. New balance: {NewBalance}",
                    customerId, amount, customer.TotalDebt);

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating balance for customer {CustomerId} by amount {Amount}", customerId, amount);
                throw;
            }
        }

        public async Task<(decimal TotalDebt, int CustomerCount)> GetDebtSummaryAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var debtSummary = await context.Customers
                    .AsNoTracking()
                    .Where(c => c.IsActive && c.TotalDebt > 0)
                    .GroupBy(c => 1)
                    .Select(g => new { TotalDebt = g.Sum(c => c.TotalDebt), CustomerCount = g.Count() })
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                return (debtSummary?.TotalDebt ?? 0, debtSummary?.CustomerCount ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving debt summary");
                throw;
            }
        }

        #endregion

        #region Transaction History and Analytics

        public async Task<IEnumerable<Invoice>> GetCustomerInvoicesAsync(int customerId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Invoices
                    .AsNoTracking()
                    .Where(i => i.CustomerId == customerId);

                if (fromDate.HasValue)
                    query = query.Where(i => i.InvoiceDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(i => i.InvoiceDate <= toDate.Value.Date.AddDays(1));

                return await query
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices for customer {CustomerId} from {FromDate} to {ToDate}",
                    customerId, fromDate, toDate);
                throw;
            }
        }

        public async Task<IEnumerable<Payment>> GetCustomerPaymentsAsync(int customerId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Payments
                    .AsNoTracking()
                    .Where(p => p.CustomerId == customerId);

                if (fromDate.HasValue)
                    query = query.Where(p => p.PaymentDate >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(p => p.PaymentDate <= toDate.Value.Date.AddDays(1));

                return await query
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

        public async Task<(decimal TotalSales, decimal TotalPayments, decimal NetBalance)> GetCustomerAccountSummaryAsync(int customerId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                // Build base queries
                var invoiceQuery = context.Invoices.AsNoTracking().Where(i => i.CustomerId == customerId);
                var paymentQuery = context.Payments.AsNoTracking().Where(p => p.CustomerId == customerId);

                // Apply date filters
                if (fromDate.HasValue)
                {
                    invoiceQuery = invoiceQuery.Where(i => i.InvoiceDate >= fromDate.Value);
                    paymentQuery = paymentQuery.Where(p => p.PaymentDate >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    var endDate = toDate.Value.Date.AddDays(1);
                    invoiceQuery = invoiceQuery.Where(i => i.InvoiceDate <= endDate);
                    paymentQuery = paymentQuery.Where(p => p.PaymentDate <= endDate);
                }

                // Execute queries concurrently
                var totalSalesTask = invoiceQuery.SumAsync(i => i.FinalAmount, cancellationToken);
                var totalPaymentsTask = paymentQuery.SumAsync(p => p.Amount, cancellationToken);

                await Task.WhenAll(totalSalesTask, totalPaymentsTask).ConfigureAwait(false);

                var totalSales = await totalSalesTask.ConfigureAwait(false);
                var totalPayments = await totalPaymentsTask.ConfigureAwait(false);
                var netBalance = totalSales - totalPayments;

                return (totalSales, totalPayments, netBalance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving account summary for customer {CustomerId} from {FromDate} to {ToDate}",
                    customerId, fromDate, toDate);
                throw;
            }
        }

        #endregion

        #region Customer Search and Filtering

        public async Task<IEnumerable<Customer>> SearchCustomersAsync(string searchTerm, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var lowerSearchTerm = searchTerm.ToLower();

                return await context.Customers
                    .AsNoTracking()
                    .Where(c => c.IsActive &&
                               (c.CustomerName.ToLower().Contains(lowerSearchTerm) ||
                                (c.PhoneNumber != null && c.PhoneNumber.Contains(searchTerm)) ||
                                (c.Address != null && c.Address.ToLower().Contains(lowerSearchTerm))))
                    .OrderBy(c => c.CustomerName)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching customers with term {SearchTerm}", searchTerm);
                throw;
            }
        }

        public async Task<IEnumerable<Customer>> GetCustomersByStatusAsync(bool isActive, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                return await context.Customers
                    .AsNoTracking()
                    .Where(c => c.IsActive == isActive)
                    .OrderBy(c => c.CustomerName)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers by status {IsActive}", isActive);
                throw;
            }
        }

        public async Task<(IEnumerable<Customer> Customers, int TotalCount)> GetCustomersPagedAsync(int pageNumber, int pageSize, string? searchTerm = null, bool? isActive = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Customers.AsNoTracking();

                // Apply filters
                if (isActive.HasValue)
                    query = query.Where(c => c.IsActive == isActive.Value);

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var lowerSearchTerm = searchTerm.ToLower();
                    query = query.Where(c => c.CustomerName.ToLower().Contains(lowerSearchTerm) ||
                                           (c.PhoneNumber != null && c.PhoneNumber.Contains(searchTerm)) ||
                                           (c.Address != null && c.Address.ToLower().Contains(lowerSearchTerm)));
                }

                // Get total count
                var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

                // Apply pagination
                var customers = await query
                    .OrderBy(c => c.CustomerName)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return (customers, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged customers. Page: {PageNumber}, Size: {PageSize}, Search: {SearchTerm}, Active: {IsActive}",
                    pageNumber, pageSize, searchTerm, isActive);
                throw;
            }
        }

        #endregion

        #region Business Intelligence and Reporting

        public async Task<IEnumerable<(Customer Customer, decimal TotalPurchases, int InvoiceCount)>> GetTopCustomersAsync(int topCount, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Customers
                    .AsNoTracking()
                    .Where(c => c.IsActive);

                var invoiceQuery = context.Invoices.AsNoTracking();

                if (fromDate.HasValue)
                    invoiceQuery = invoiceQuery.Where(i => i.InvoiceDate >= fromDate.Value);

                if (toDate.HasValue)
                    invoiceQuery = invoiceQuery.Where(i => i.InvoiceDate <= toDate.Value.Date.AddDays(1));

                var topCustomers = await query
                    .GroupJoin(invoiceQuery, c => c.CustomerId, i => i.CustomerId, (c, invoices) => new
                    {
                        Customer = c,
                        TotalPurchases = invoices.Sum(i => i.FinalAmount),
                        InvoiceCount = invoices.Count()
                    })
                    .Where(x => x.InvoiceCount > 0)
                    .OrderByDescending(x => x.TotalPurchases)
                    .Take(topCount)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return topCustomers.Select(tc => (tc.Customer, tc.TotalPurchases, tc.InvoiceCount));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top {TopCount} customers from {FromDate} to {ToDate}",
                    topCount, fromDate, toDate);
                throw;
            }
        }

        public async Task<IEnumerable<(DateTime Date, decimal Amount)>> GetCustomerPurchaseHistoryAsync(int customerId, int monthsBack = 12, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var fromDate = DateTime.Today.AddMonths(-monthsBack);

                var purchaseHistory = await context.Invoices
                    .AsNoTracking()
                    .Where(i => i.CustomerId == customerId && i.InvoiceDate >= fromDate)
                    .GroupBy(i => new { Year = i.InvoiceDate.Year, Month = i.InvoiceDate.Month })
                    .Select(g => new
                    {
                        Date = new DateTime(g.Key.Year, g.Key.Month, 1),
                        Amount = g.Sum(i => i.FinalAmount)
                    })
                    .OrderBy(x => x.Date)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return purchaseHistory.Select(ph => (ph.Date, ph.Amount));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving purchase history for customer {CustomerId} for {MonthsBack} months",
                    customerId, monthsBack);
                throw;
            }
        }

        public async Task<Dictionary<int, (decimal LastPayment, DateTime LastPaymentDate)>> GetCustomerLastPaymentInfoAsync(IEnumerable<int> customerIds, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var customerIdsList = customerIds.ToList();

                var lastPayments = await context.Payments
                    .AsNoTracking()
                    .Where(p => customerIdsList.Contains(p.CustomerId))
                    .GroupBy(p => p.CustomerId)
                    .Select(g => new
                    {
                        CustomerId = g.Key,
                        LastPayment = g.OrderByDescending(p => p.PaymentDate).First().Amount,
                        LastPaymentDate = g.Max(p => p.PaymentDate)
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return lastPayments.ToDictionary(lp => lp.CustomerId, lp => (lp.LastPayment, lp.LastPaymentDate));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving last payment info for customers {CustomerIds}", string.Join(",", customerIds));
                throw;
            }
        }

        #endregion

        #region Account Aging and Collections

        public async Task<IEnumerable<(Customer Customer, int DaysOutstanding, decimal Amount)>> GetAgedReceivablesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var today = DateTime.Today;

                var agedReceivables = await context.Customers
                    .AsNoTracking()
                    .Where(c => c.IsActive && c.TotalDebt > 0)
                    .Select(c => new
                    {
                        Customer = c,
                        LastInvoiceDate = c.Invoices.Max(i => i.InvoiceDate),
                        Amount = c.TotalDebt
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return agedReceivables.Select(ar => (
                    ar.Customer,
                    DaysOutstanding: (today - ar.LastInvoiceDate).Days,
                    ar.Amount
                )).OrderByDescending(x => x.DaysOutstanding);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving aged receivables");
                throw;
            }
        }

        public async Task<IEnumerable<Customer>> GetCustomersWithOverdueAccountsAsync(int daysOverdue, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var cutoffDate = DateTime.Today.AddDays(-daysOverdue);

                return await context.Customers
                    .AsNoTracking()
                    .Where(c => c.IsActive &&
                               c.TotalDebt > 0 &&
                               c.Invoices.Any(i => i.InvoiceDate <= cutoffDate))
                    .OrderByDescending(c => c.TotalDebt)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers with overdue accounts ({DaysOverdue} days)", daysOverdue);
                throw;
            }
        }

        public async Task<Dictionary<string, (int Count, decimal Amount)>> GetDebtAgingAnalysisAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var today = DateTime.Today;
                var customersWithDebt = await context.Customers
                    .AsNoTracking()
                    .Where(c => c.IsActive && c.TotalDebt > 0)
                    .Select(c => new
                    {
                        Customer = c,
                        LastInvoiceDate = c.Invoices.Max(i => i.InvoiceDate),
                        Debt = c.TotalDebt
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var agingAnalysis = new Dictionary<string, (int Count, decimal Amount)>
                {
                    ["Current (0-30 days)"] = (0, 0),
                    ["31-60 days"] = (0, 0),
                    ["61-90 days"] = (0, 0),
                    ["Over 90 days"] = (0, 0)
                };

                foreach (var customer in customersWithDebt)
                {
                    var daysOutstanding = (today - customer.LastInvoiceDate).Days;
                    string category;

                    if (daysOutstanding <= 30)
                        category = "Current (0-30 days)";
                    else if (daysOutstanding <= 60)
                        category = "31-60 days";
                    else if (daysOutstanding <= 90)
                        category = "61-90 days";
                    else
                        category = "Over 90 days";

                    agingAnalysis[category] = (
                        agingAnalysis[category].Count + 1,
                        agingAnalysis[category].Amount + customer.Debt
                    );
                }

                return agingAnalysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing debt aging analysis");
                throw;
            }
        }

        #endregion

        #region Validation and Business Rules

        public async Task<bool> CustomerNameExistsAsync(string customerName, int? excludeCustomerId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Customers.AsNoTracking()
                    .Where(c => c.CustomerName.ToLower() == customerName.ToLower());

                if (excludeCustomerId.HasValue)
                    query = query.Where(c => c.CustomerId != excludeCustomerId.Value);

                return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if customer name {CustomerName} exists", customerName);
                throw;
            }
        }

        public async Task<bool> PhoneNumberExistsAsync(string phoneNumber, int? excludeCustomerId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var query = context.Customers.AsNoTracking()
                    .Where(c => c.PhoneNumber == phoneNumber);

                if (excludeCustomerId.HasValue)
                    query = query.Where(c => c.CustomerId != excludeCustomerId.Value);

                return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if phone number {PhoneNumber} exists", phoneNumber);
                throw;
            }
        }

        public async Task<bool> CanDeleteCustomerAsync(int customerId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var hasTransactions = await context.Invoices
                    .AsNoTracking()
                    .AnyAsync(i => i.CustomerId == customerId, cancellationToken)
                    .ConfigureAwait(false);

                var hasPayments = await context.Payments
                    .AsNoTracking()
                    .AnyAsync(p => p.CustomerId == customerId, cancellationToken)
                    .ConfigureAwait(false);

                return !hasTransactions && !hasPayments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if customer {CustomerId} can be deleted", customerId);
                throw;
            }
        }

        public async Task<int> GetActiveCustomerCountAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                return await context.Customers
                    .AsNoTracking()
                    .CountAsync(c => c.IsActive, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting active customers");
                throw;
            }
        }

        #endregion

        #region Account Reconciliation and Integrity

        public async Task<IEnumerable<Customer>> GetCustomersWithBalanceDiscrepanciesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var customersWithDiscrepancies = await context.Customers
                    .AsNoTracking()
                    .Select(c => new
                    {
                        Customer = c,
                        CalculatedBalance = c.Invoices.Sum(i => i.FinalAmount) - c.Payments.Sum(p => p.Amount),
                        StoredBalance = c.TotalDebt
                    })
                    .Where(x => Math.Abs(x.CalculatedBalance - x.StoredBalance) > 0.01m)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return customersWithDiscrepancies.Select(cwd => cwd.Customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers with balance discrepancies");
                throw;
            }
        }

        public async Task<bool> RecalculateCustomerBalanceAsync(int customerId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var customer = await context.Customers
                    .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
                    .ConfigureAwait(false);

                if (customer == null)
                {
                    _logger.LogWarning("Customer {CustomerId} not found for balance recalculation", customerId);
                    return false;
                }

                var totalInvoices = await context.Invoices
                    .AsNoTracking()
                    .Where(i => i.CustomerId == customerId)
                    .SumAsync(i => i.FinalAmount, cancellationToken)
                    .ConfigureAwait(false);

                var totalPayments = await context.Payments
                    .AsNoTracking()
                    .Where(p => p.CustomerId == customerId)
                    .SumAsync(p => p.Amount, cancellationToken)
                    .ConfigureAwait(false);

                var calculatedBalance = totalInvoices - totalPayments;

                if (Math.Abs(customer.TotalDebt - calculatedBalance) > 0.01m)
                {
                    var oldBalance = customer.TotalDebt;
                    customer.TotalDebt = calculatedBalance;
                    customer.UpdatedDate = DateTime.Now;

                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation("Recalculated balance for customer {CustomerId}. Old: {OldBalance}, New: {NewBalance}",
                        customerId, oldBalance, calculatedBalance);

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recalculating balance for customer {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<Dictionary<int, decimal>> RecalculateAllCustomerBalancesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

                var allCustomers = await context.Customers.ToListAsync(cancellationToken).ConfigureAwait(false);
                var adjustments = new Dictionary<int, decimal>();

                foreach (var customer in allCustomers)
                {
                    var totalInvoices = await context.Invoices
                        .AsNoTracking()
                        .Where(i => i.CustomerId == customer.CustomerId)
                        .SumAsync(i => i.FinalAmount, cancellationToken)
                        .ConfigureAwait(false);

                    var totalPayments = await context.Payments
                        .AsNoTracking()
                        .Where(p => p.CustomerId == customer.CustomerId)
                        .SumAsync(p => p.Amount, cancellationToken)
                        .ConfigureAwait(false);

                    var calculatedBalance = totalInvoices - totalPayments;

                    if (Math.Abs(customer.TotalDebt - calculatedBalance) > 0.01m)
                    {
                        adjustments[customer.CustomerId] = calculatedBalance - customer.TotalDebt;
                        customer.TotalDebt = calculatedBalance;
                        customer.UpdatedDate = DateTime.Now;
                    }
                }

                if (adjustments.Any())
                {
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Recalculated balances for {Count} customers", adjustments.Count);
                }

                return adjustments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recalculating all customer balances");
                throw;
            }
        }

        #endregion
    }
}