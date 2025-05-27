using PoultrySlaughterPOS.Models;

namespace PoultrySlaughterPOS.Services.Repositories
{
    /// <summary>
    /// Payment repository interface providing comprehensive payment processing and financial management.
    /// Implements secure payment operations, debt settlement tracking, and financial analytics
    /// optimized for multi-terminal POS environments with transactional integrity.
    /// </summary>
    public interface IPaymentRepository : IRepository<Payment>
    {
        // Core payment processing operations
        Task<Payment> CreatePaymentWithTransactionAsync(Payment payment, CancellationToken cancellationToken = default);
        Task<Payment?> GetPaymentWithDetailsAsync(int paymentId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Payment>> GetPaymentsByDateAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<IEnumerable<Payment>> GetPaymentsByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

        // Customer payment management
        Task<IEnumerable<Payment>> GetCustomerPaymentsAsync(int customerId, int? limit = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<Payment>> GetCustomerPaymentsByDateAsync(int customerId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
        Task<Payment?> GetCustomerLastPaymentAsync(int customerId, CancellationToken cancellationToken = default);
        Task<decimal> GetCustomerTotalPaymentsAsync(int customerId, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);

        // Invoice-specific payment operations
        Task<IEnumerable<Payment>> GetInvoicePaymentsAsync(int invoiceId, CancellationToken cancellationToken = default);
        Task<decimal> GetInvoiceTotalPaymentsAsync(int invoiceId, CancellationToken cancellationToken = default);
        Task<decimal> GetInvoiceRemainingBalanceAsync(int invoiceId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Invoice>> GetFullyPaidInvoicesAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);

        // Payment method analytics
        Task<Dictionary<string, decimal>> GetPaymentsByMethodAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
        Task<Dictionary<string, int>> GetPaymentCountByMethodAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<(string Method, decimal Amount, int Count)>> GetPaymentMethodAnalysisAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);

        // Daily operations and cash flow
        Task<IEnumerable<Payment>> GetTodaysPaymentsAsync(CancellationToken cancellationToken = default);
        Task<decimal> GetTotalPaymentsAmountAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
        Task<(decimal TotalAmount, int PaymentCount)> GetPaymentsSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
        Task<decimal> GetCashPaymentsAmountAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);

        // Financial analytics and reporting
        Task<IEnumerable<(DateTime Date, decimal Amount, int Count)>> GetDailyPaymentAnalysisAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<(int Hour, decimal Amount, int Count)>> GetHourlyPaymentAnalysisAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<Dictionary<int, decimal>> GetPaymentsByCustomerAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);

        // Outstanding debt and collections
        Task<IEnumerable<Customer>> GetCustomersWithOutstandingDebtAsync(CancellationToken cancellationToken = default);
        Task<Dictionary<int, (decimal LastPaymentAmount, DateTime LastPaymentDate, int DaysSincePayment)>> GetCustomerPaymentStatusAsync(IEnumerable<int> customerIds, CancellationToken cancellationToken = default);
        Task<IEnumerable<(Customer Customer, decimal TotalPaid, DateTime LastPayment, decimal RemainingDebt)>> GetCustomerPaymentSummaryAsync(CancellationToken cancellationToken = default);

        // Payment search and filtering
        Task<IEnumerable<Payment>> SearchPaymentsAsync(string searchTerm, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
        Task<(IEnumerable<Payment> Payments, int TotalCount)> GetPaymentsPagedAsync(int pageNumber, int pageSize, int? customerId = null, string? paymentMethod = null, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);

        // Performance and trend analysis
        Task<(decimal AveragePayment, decimal LargestPayment, decimal SmallestPayment)> GetPaymentStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<(int CustomerId, string CustomerName, decimal AveragePayment, int PaymentFrequency)>> GetCustomerPaymentPatternsAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
        Task<Dictionary<int, List<(DateTime Date, decimal Amount)>>> GetCustomerPaymentHistoryAsync(IEnumerable<int> customerIds, int monthsBack = 12, CancellationToken cancellationToken = default);

        // Business validation and integrity
        Task<bool> CanDeletePaymentAsync(int paymentId, CancellationToken cancellationToken = default);
        Task<int> GetPaymentCountAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<Payment>> GetUnallocatedPaymentsAsync(CancellationToken cancellationToken = default);

        // Reconciliation and audit support
        Task<IEnumerable<Payment>> GetPaymentsRequiringAuditAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
        Task<Dictionary<string, (decimal Amount, int Count)>> GetPaymentReconciliationSummaryAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<IEnumerable<(Payment Payment, decimal CalculatedBalance, decimal StoredBalance, decimal Variance)>> GetPaymentBalanceDiscrepanciesAsync(CancellationToken cancellationToken = default);

        // Advanced financial reporting
        Task<(decimal CollectionRate, decimal AverageCollectionTime, decimal OutstandingPercentage)> GetCollectionKPIsAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
        Task<Dictionary<string, decimal>> GetCashFlowAnalysisAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<(DateTime Week, decimal Collections, decimal OutstandingStart, decimal OutstandingEnd)>> GetWeeklyCollectionTrendsAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    }
}