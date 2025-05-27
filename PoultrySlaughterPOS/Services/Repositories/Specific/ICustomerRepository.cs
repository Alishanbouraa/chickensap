using PoultrySlaughterPOS.Models;

namespace PoultrySlaughterPOS.Services.Repositories
{
    /// <summary>
    /// Specialized repository interface for customer account management
    /// with advanced financial operations and debt tracking capabilities
    /// </summary>
    public interface ICustomerRepository : IRepository<Customer>
    {
        // Customer Search and Filtering
        Task<IEnumerable<Customer>> SearchCustomersAsync(string searchTerm, CancellationToken cancellationToken = default);
        Task<Customer?> GetCustomerByPhoneAsync(string phoneNumber, CancellationToken cancellationToken = default);
        Task<IEnumerable<Customer>> GetActiveCustomersAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Customer>> GetCustomersWithDebtAsync(CancellationToken cancellationToken = default);

        // Financial Operations with Optimistic Locking
        Task<decimal> GetCustomerBalanceAsync(int customerId, CancellationToken cancellationToken = default);
        Task<bool> UpdateCustomerBalanceAsync(int customerId, decimal amount, string operation, CancellationToken cancellationToken = default);
        Task<bool> UpdateCustomerBalanceWithConcurrencyAsync(int customerId, decimal newBalance, decimal expectedCurrentBalance, CancellationToken cancellationToken = default);

        // Transaction History Integration
        Task<IEnumerable<Invoice>> GetCustomerInvoicesAsync(int customerId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<Payment>> GetCustomerPaymentsAsync(int customerId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
        Task<(decimal TotalSales, decimal TotalPayments, decimal CurrentBalance)> GetCustomerFinancialSummaryAsync(int customerId, CancellationToken cancellationToken = default);

        // Advanced Analytics and Reporting
        Task<IEnumerable<Customer>> GetTopCustomersByVolumeAsync(int topCount, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<Customer>> GetCustomersWithOverduePaymentsAsync(int dayThreshold, CancellationToken cancellationToken = default);
        Task<Dictionary<int, decimal>> GetCustomerDebtAgingReportAsync(CancellationToken cancellationToken = default);

        // Customer Account Validation
        Task<bool> IsCustomerActiveAsync(int customerId, CancellationToken cancellationToken = default);
        Task<bool> HasOutstandingInvoicesAsync(int customerId, CancellationToken cancellationToken = default);
        Task<decimal> GetCreditLimitUtilizationAsync(int customerId, decimal creditLimit, CancellationToken cancellationToken = default);

        // Bulk Operations for Performance
        Task<Dictionary<int, decimal>> GetMultipleCustomerBalancesAsync(IEnumerable<int> customerIds, CancellationToken cancellationToken = default);
        Task<int> UpdateMultipleCustomerStatusAsync(IEnumerable<int> customerIds, bool isActive, CancellationToken cancellationToken = default);

        // Customer Lifecycle Management
        Task<bool> ArchiveInactiveCustomerAsync(int customerId, int inactivityDayThreshold, CancellationToken cancellationToken = default);
        Task<IEnumerable<Customer>> GetCustomersForRetentionCampaignAsync(decimal minimumDebt, int daysSinceLastPurchase, CancellationToken cancellationToken = default);
    }
}