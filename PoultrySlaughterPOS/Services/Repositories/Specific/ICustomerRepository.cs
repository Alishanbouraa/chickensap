using PoultrySlaughterPOS.Models;

namespace PoultrySlaughterPOS.Repositories
{
    /// <summary>
    /// Customer repository interface providing comprehensive customer account management operations.
    /// Implements advanced financial tracking, debt management, and customer analytics for POS operations.
    /// </summary>
    public interface ICustomerRepository : IBaseRepository<Customer, int>
    {
        // Core customer management operations
        Task<IEnumerable<Customer>> GetActiveCustomersAsync(CancellationToken cancellationToken = default);
        Task<Customer?> GetCustomerByNameAsync(string customerName, CancellationToken cancellationToken = default);
        Task<Customer?> GetCustomerByPhoneAsync(string phoneNumber, CancellationToken cancellationToken = default);
        Task<Customer?> GetCustomerWithTransactionsAsync(int customerId, CancellationToken cancellationToken = default);

        // Account balance and debt management
        Task<IEnumerable<Customer>> GetCustomersWithDebtAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Customer>> GetCustomersWithDebtAboveThresholdAsync(decimal threshold, CancellationToken cancellationToken = default);
        Task<decimal> GetCustomerTotalDebtAsync(int customerId, CancellationToken cancellationToken = default);
        Task<bool> UpdateCustomerBalanceAsync(int customerId, decimal amount, CancellationToken cancellationToken = default);
        Task<(decimal TotalDebt, int CustomerCount)> GetDebtSummaryAsync(CancellationToken cancellationToken = default);

        // Transaction history and analytics
        Task<IEnumerable<Invoice>> GetCustomerInvoicesAsync(int customerId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<Payment>> GetCustomerPaymentsAsync(int customerId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
        Task<(decimal TotalSales, decimal TotalPayments, decimal NetBalance)> GetCustomerAccountSummaryAsync(int customerId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);

        // Customer search and filtering
        Task<IEnumerable<Customer>> SearchCustomersAsync(string searchTerm, CancellationToken cancellationToken = default);
        Task<IEnumerable<Customer>> GetCustomersByStatusAsync(bool isActive, CancellationToken cancellationToken = default);
        Task<(IEnumerable<Customer> Customers, int TotalCount)> GetCustomersPagedAsync(int pageNumber, int pageSize, string? searchTerm = null, bool? isActive = null, CancellationToken cancellationToken = default);

        // Business intelligence and reporting
        Task<IEnumerable<(Customer Customer, decimal TotalPurchases, int InvoiceCount)>> GetTopCustomersAsync(int topCount, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<(DateTime Date, decimal Amount)>> GetCustomerPurchaseHistoryAsync(int customerId, int monthsBack = 12, CancellationToken cancellationToken = default);
        Task<Dictionary<int, (decimal LastPayment, DateTime LastPaymentDate)>> GetCustomerLastPaymentInfoAsync(IEnumerable<int> customerIds, CancellationToken cancellationToken = default);

        // Account aging and collections
        Task<IEnumerable<(Customer Customer, int DaysOutstanding, decimal Amount)>> GetAgedReceivablesAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Customer>> GetCustomersWithOverdueAccountsAsync(int daysOverdue, CancellationToken cancellationToken = default);
        Task<Dictionary<string, (int Count, decimal Amount)>> GetDebtAgingAnalysisAsync(CancellationToken cancellationToken = default);

        // Validation and business rules
        Task<bool> CustomerNameExistsAsync(string customerName, int? excludeCustomerId = null, CancellationToken cancellationToken = default);
        Task<bool> PhoneNumberExistsAsync(string phoneNumber, int? excludeCustomerId = null, CancellationToken cancellationToken = default);
        Task<bool> CanDeleteCustomerAsync(int customerId, CancellationToken cancellationToken = default);
        Task<int> GetActiveCustomerCountAsync(CancellationToken cancellationToken = default);

        // Account reconciliation and integrity
        Task<IEnumerable<Customer>> GetCustomersWithBalanceDiscrepanciesAsync(CancellationToken cancellationToken = default);
        Task<bool> RecalculateCustomerBalanceAsync(int customerId, CancellationToken cancellationToken = default);
        Task<Dictionary<int, decimal>> RecalculateAllCustomerBalancesAsync(CancellationToken cancellationToken = default);
    }
}