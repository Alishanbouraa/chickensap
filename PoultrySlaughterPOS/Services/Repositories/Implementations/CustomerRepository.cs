using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Data;
using PoultrySlaughterPOS.Models;

namespace PoultrySlaughterPOS.Services.Repositories
{
    /// <summary>
    /// Professional customer repository implementation with advanced financial operations,
    /// debt management, and comprehensive customer lifecycle support
    /// </summary>
    public class CustomerRepository : Repository<Customer>, ICustomerRepository
    {
        public CustomerRepository(PoultryDbContext context, ILogger<CustomerRepository> logger)
            : base(context, logger)
        {
        }

        public async Task<IEnumerable<Customer>> SearchCustomersAsync(string searchTerm, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                    return await GetActiveCustomersAsync(cancellationToken).ConfigureAwait(false);

                var term = searchTerm.Trim().ToLower();

                return await _dbSet
                    .Where(c => c.IsActive &&
                               (c.CustomerName.ToLower().Contains(term) ||
                                (c.PhoneNumber != null && c.PhoneNumber.Contains(term)) ||
                                (c.Address != null && c.Address.ToLower().Contains(term))))
                    .OrderBy(c => c.CustomerName)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching customers with term: {SearchTerm}", searchTerm);
                throw;
            }
        }

        public async Task<Customer?> GetCustomerByPhoneAsync(string phoneNumber, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber && c.IsActive, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer by phone: {PhoneNumber}", phoneNumber);
                throw;
            }
        }

        public async Task<IEnumerable<Customer>> GetActiveCustomersAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
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

        public async Task<IEnumerable<Customer>> GetCustomersWithDebtAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
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

        public async Task<decimal> GetCustomerBalanceAsync(int customerId, CancellationToken cancellationToken = default)
        {
            try
            {
                var customer = await _dbSet
                    .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
                    .ConfigureAwait(false);

                return customer?.TotalDebt ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving balance for customer {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<bool> UpdateCustomerBalanceAsync(int customerId, decimal amount, string operation, CancellationToken cancellationToken = default)
        {
            try
            {
                var customer = await _dbSet
                    .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
                    .ConfigureAwait(false);

                if (customer == null)
                {
                    _logger.LogWarning("Customer {CustomerId} not found for balance update", customerId);
                    return false;
                }

                var oldBalance = customer.TotalDebt;

                customer.TotalDebt = operation.ToUpper() switch
                {
                    "ADD" => customer.TotalDebt + amount,
                    "SUBTRACT" => customer.TotalDebt - amount,
                    "SET" => amount,
                    _ => throw new ArgumentException($"Invalid operation: {operation}")
                };

                customer.UpdatedDate = DateTime.Now;

                _logger.LogDebug("Updated customer {CustomerId} balance from {OldBalance} to {NewBalance} (Operation: {Operation})",
                    customerId, oldBalance, customer.TotalDebt, operation);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating balance for customer {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<bool> UpdateCustomerBalanceWithConcurrencyAsync(int customerId, decimal newBalance, decimal expectedCurrentBalance, CancellationToken cancellationToken = default)
        {
            try
            {
                var customer = await _dbSet
                    .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
                    .ConfigureAwait(false);

                if (customer == null)
                {
                    _logger.LogWarning("Customer {CustomerId} not found for concurrency balance update", customerId);
                    return false;
                }

                if (Math.Abs(customer.TotalDebt - expectedCurrentBalance) > 0.01m)
                {
                    _logger.LogWarning("Concurrency conflict for customer {CustomerId}: Expected {Expected}, Actual {Actual}",
                        customerId, expectedCurrentBalance, customer.TotalDebt);
                    return false;
                }

                customer.TotalDebt = newBalance;
                customer.UpdatedDate = DateTime.Now;

                _logger.LogDebug("Successfully updated customer {CustomerId} balance to {NewBalance} with concurrency check",
                    customerId, newBalance);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating balance with concurrency for customer {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<IEnumerable<Invoice>> GetCustomerInvoicesAsync(int customerId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var query = _context.Invoices
                    .Include(i => i.Truck)
                    .Where(i => i.CustomerId == customerId);

                if (startDate.HasValue)
                    query = query.Where(i => i.InvoiceDate >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(i => i.InvoiceDate <= endDate.Value);

                return await query
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices for customer {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<IEnumerable<Payment>> GetCustomerPaymentsAsync(int customerId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var query = _context.Payments
                    .Include(p => p.Invoice)
                    .Where(p => p.CustomerId == customerId);

                if (startDate.HasValue)
                    query = query.Where(p => p.PaymentDate >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(p => p.PaymentDate <= endDate.Value);

                return await query
                    .OrderByDescending(p => p.PaymentDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payments for customer {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<(decimal TotalSales, decimal TotalPayments, decimal CurrentBalance)> GetCustomerFinancialSummaryAsync(int customerId, CancellationToken cancellationToken = default)
        {
            try
            {
                var totalSales = await _context.Invoices
                    .Where(i => i.CustomerId == customerId)
                    .SumAsync(i => i.FinalAmount, cancellationToken)
                    .ConfigureAwait(false);

                var totalPayments = await _context.Payments
                    .Where(p => p.CustomerId == customerId)
                    .SumAsync(p => p.Amount, cancellationToken)
                    .ConfigureAwait(false);

                var currentBalance = await GetCustomerBalanceAsync(customerId, cancellationToken)
                    .ConfigureAwait(false);

                return (totalSales, totalPayments, currentBalance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving financial summary for customer {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<IEnumerable<Customer>> GetTopCustomersByVolumeAsync(int topCount, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Include(c => c.Invoices.Where(i => i.InvoiceDate >= startDate && i.InvoiceDate <= endDate))
                    .Where(c => c.IsActive && c.Invoices.Any(i => i.InvoiceDate >= startDate && i.InvoiceDate <= endDate))
                    .OrderByDescending(c => c.Invoices.Sum(i => i.FinalAmount))
                    .Take(topCount)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top {TopCount} customers by volume between {StartDate} and {EndDate}",
                    topCount, startDate, endDate);
                throw;
            }
        }

        public async Task<IEnumerable<Customer>> GetCustomersWithOverduePaymentsAsync(int dayThreshold, CancellationToken cancellationToken = default)
        {
            try
            {
                var cutoffDate = DateTime.Today.AddDays(-dayThreshold);

                return await _dbSet
                    .Include(c => c.Invoices)
                    .Include(c => c.Payments)
                    .Where(c => c.IsActive &&
                               c.TotalDebt > 0 &&
                               c.Invoices.Any(i => i.InvoiceDate <= cutoffDate) &&
                               (!c.Payments.Any() || c.Payments.Max(p => p.PaymentDate) <= cutoffDate))
                    .OrderByDescending(c => c.TotalDebt)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers with overdue payments (threshold: {DayThreshold} days)", dayThreshold);
                throw;
            }
        }

        public async Task<Dictionary<int, decimal>> GetCustomerDebtAgingReportAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Where(c => c.IsActive && c.TotalDebt > 0)
                    .ToDictionaryAsync(c => c.CustomerId, c => c.TotalDebt, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating customer debt aging report");
                throw;
            }
        }

        public async Task<bool> IsCustomerActiveAsync(int customerId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .AnyAsync(c => c.CustomerId == customerId && c.IsActive, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if customer {CustomerId} is active", customerId);
                throw;
            }
        }

        public async Task<bool> HasOutstandingInvoicesAsync(int customerId, CancellationToken cancellationToken = default)
        {
            try
            {
                var customer = await _dbSet
                    .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
                    .ConfigureAwait(false);

                return customer != null && customer.TotalDebt > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking outstanding invoices for customer {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<decimal> GetCreditLimitUtilizationAsync(int customerId, decimal creditLimit, CancellationToken cancellationToken = default)
        {
            try
            {
                var currentDebt = await GetCustomerBalanceAsync(customerId, cancellationToken)
                    .ConfigureAwait(false);

                if (creditLimit <= 0)
                    return 0;

                return (currentDebt / creditLimit) * 100;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating credit limit utilization for customer {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<Dictionary<int, decimal>> GetMultipleCustomerBalancesAsync(IEnumerable<int> customerIds, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Where(c => customerIds.Contains(c.CustomerId))
                    .ToDictionaryAsync(c => c.CustomerId, c => c.TotalDebt, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving multiple customer balances");
                throw;
            }
        }

        public async Task<int> UpdateMultipleCustomerStatusAsync(IEnumerable<int> customerIds, bool isActive, CancellationToken cancellationToken = default)
        {
            try
            {
                var customers = await _dbSet
                    .Where(c => customerIds.Contains(c.CustomerId))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                foreach (var customer in customers)
                {
                    customer.IsActive = isActive;
                    customer.UpdatedDate = DateTime.Now;
                }

                _logger.LogDebug("Updated status for {CustomerCount} customers to {Status}",
                    customers.Count, isActive ? "Active" : "Inactive");

                return customers.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating multiple customer status");
                throw;
            }
        }

        public async Task<bool> ArchiveInactiveCustomerAsync(int customerId, int inactivityDayThreshold, CancellationToken cancellationToken = default)
        {
            try
            {
                var cutoffDate = DateTime.Today.AddDays(-inactivityDayThreshold);

                var customer = await _dbSet
                    .Include(c => c.Invoices)
                    .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
                    .ConfigureAwait(false);

                if (customer == null)
                    return false;

                var hasRecentActivity = customer.Invoices.Any(i => i.InvoiceDate >= cutoffDate);
                var hasOutstandingDebt = customer.TotalDebt > 0;

                if (!hasRecentActivity && !hasOutstandingDebt)
                {
                    customer.IsActive = false;
                    customer.UpdatedDate = DateTime.Now;

                    _logger.LogInformation("Archived inactive customer {CustomerId} (no activity for {Days} days)",
                        customerId, inactivityDayThreshold);

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving inactive customer {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<IEnumerable<Customer>> GetCustomersForRetentionCampaignAsync(decimal minimumDebt, int daysSinceLastPurchase, CancellationToken cancellationToken = default)
        {
            try
            {
                var cutoffDate = DateTime.Today.AddDays(-daysSinceLastPurchase);

                return await _dbSet
                    .Include(c => c.Invoices)
                    .Where(c => c.IsActive &&
                               c.TotalDebt >= minimumDebt &&
                               c.Invoices.Any() &&
                               c.Invoices.Max(i => i.InvoiceDate) <= cutoffDate)
                    .OrderByDescending(c => c.TotalDebt)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers for retention campaign");
                throw;
            }
        }
    }
}