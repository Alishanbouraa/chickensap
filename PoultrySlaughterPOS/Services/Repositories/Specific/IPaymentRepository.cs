using PoultrySlaughterPOS.Models;

namespace PoultrySlaughterPOS.Services.Repositories
{
    /// <summary>
    /// Professional-grade repository interface for payment processing and financial transaction management
    /// with comprehensive audit trails and multi-currency support capabilities
    /// </summary>
    public interface IPaymentRepository : IRepository<Payment>
    {
        // Payment Processing and Validation
        Task<Payment> ProcessPaymentAsync(Payment payment, CancellationToken cancellationToken = default);
        Task<bool> ValidatePaymentIntegrityAsync(int paymentId, CancellationToken cancellationToken = default);
        Task<bool> IsPaymentReversibleAsync(int paymentId, CancellationToken cancellationToken = default);
        Task<Payment?> ReversePaymentAsync(int paymentId, string reversalReason, CancellationToken cancellationToken = default);

        // Customer Payment History
        Task<IEnumerable<Payment>> GetCustomerPaymentHistoryAsync(int customerId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
        Task<decimal> GetCustomerTotalPaymentsAsync(int customerId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
        Task<Payment?> GetLastCustomerPaymentAsync(int customerId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Payment>> GetCustomerPaymentsByMethodAsync(int customerId, string paymentMethod, CancellationToken cancellationToken = default);

        // Invoice-Specific Payment Operations
        Task<IEnumerable<Payment>> GetInvoicePaymentsAsync(int invoiceId, CancellationToken cancellationToken = default);
        Task<decimal> GetInvoiceTotalPaymentsAsync(int invoiceId, CancellationToken cancellationToken = default);
        Task<decimal> GetInvoiceOutstandingBalanceAsync(int invoiceId, CancellationToken cancellationToken = default);
        Task<bool> IsInvoiceFullyPaidAsync(int invoiceId, CancellationToken cancellationToken = default);

        // Advanced Financial Analytics
        Task<Dictionary<string, decimal>> GetPaymentMethodDistributionAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<decimal> GetDailyPaymentTotalAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<Dictionary<DateTime, decimal>> GetPaymentTrendAnalysisAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<(decimal AveragePayment, decimal MedianPayment, decimal TotalPayments)> GetPaymentStatisticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        // Cash Flow Management
        Task<Dictionary<int, decimal>> GetCustomerCashFlowAsync(IEnumerable<int> customerIds, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<Payment>> GetLargePaymentsAsync(decimal thresholdAmount, DateTime? startDate = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<Payment>> GetRecentPaymentsAsync(int hours = 24, CancellationToken cancellationToken = default);

        // Payment Method Analysis and Optimization
        Task<Dictionary<string, (int Count, decimal Total)>> GetPaymentMethodPerformanceAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<Payment>> GetPaymentsByMethodAndDateRangeAsync(string paymentMethod, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<bool> UpdatePaymentMethodAsync(int paymentId, string newPaymentMethod, string changeReason, CancellationToken cancellationToken = default);

        // Advanced Search and Filtering
        Task<IEnumerable<Payment>> SearchPaymentsAsync(string searchTerm, CancellationToken cancellationToken = default);
        Task<IEnumerable<Payment>> GetPaymentsByAmountRangeAsync(decimal minAmount, decimal maxAmount, CancellationToken cancellationToken = default);
        Task<IEnumerable<Payment>> GetUnallocatedPaymentsAsync(CancellationToken cancellationToken = default);

        // Reconciliation and Audit Support
        Task<IEnumerable<Payment>> GetPaymentsForReconciliationAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<bool> MarkPaymentAsReconciledAsync(int paymentId, string reconciliationReference, CancellationToken cancellationToken = default);
        Task<IEnumerable<Payment>> GetUnreconciledPaymentsAsync(int dayThreshold, CancellationToken cancellationToken = default);

        // Bulk Operations for Performance
        Task<int> ProcessPaymentBatchAsync(IEnumerable<Payment> payments, CancellationToken cancellationToken = default);
        Task<int> AllocatePaymentsBatchAsync(Dictionary<int, int> paymentInvoiceMappings, CancellationToken cancellationToken = default);
        Task<IEnumerable<Payment>> GetCustomerPaymentsBatchAsync(IEnumerable<int> customerIds, CancellationToken cancellationToken = default);

        // Payment Validation and Fraud Detection
        Task<bool> DetectDuplicatePaymentAsync(Payment payment, TimeSpan duplicateWindow, CancellationToken cancellationToken = default);
        Task<IEnumerable<Payment>> GetSuspiciousPaymentsAsync(decimal unusualAmountThreshold, CancellationToken cancellationToken = default);
        Task<bool> ValidatePaymentConsistencyAsync(int customerId, CancellationToken cancellationToken = default);
    }
}