using PoultrySlaughterPOS.Models;

namespace PoultrySlaughterPOS.Services.Repositories
{
    /// <summary>
    /// Enterprise-grade repository interface for invoice management
    /// supporting complex POS operations, financial calculations, and audit compliance
    /// </summary>
    public interface IInvoiceRepository : IRepository<Invoice>
    {
        // Invoice Generation and Numbering
        Task<string> GenerateInvoiceNumberAsync(CancellationToken cancellationToken = default);
        Task<bool> IsInvoiceNumberUniqueAsync(string invoiceNumber, CancellationToken cancellationToken = default);
        Task<Invoice?> GetInvoiceByNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default);

        // Comprehensive Invoice Retrieval with Relationships
        Task<Invoice?> GetInvoiceWithDetailsAsync(int invoiceId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Invoice>> GetInvoicesWithCustomerAndTruckAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<Invoice>> GetCustomerInvoicesWithPaymentsAsync(int customerId, CancellationToken cancellationToken = default);

        // Advanced Financial Operations
        Task<decimal> CalculateNetWeightAsync(decimal grossWeight, decimal cagesWeight);
        Task<decimal> CalculateTotalAmountAsync(decimal netWeight, decimal unitPrice);
        Task<decimal> ApplyDiscountCalculationAsync(decimal totalAmount, decimal discountPercentage);
        Task<decimal> CalculateFinalAmountAsync(decimal totalAmount, decimal discountPercentage);

        // Transaction Processing with Atomicity
        Task<Invoice> CreateInvoiceWithBalanceUpdateAsync(Invoice invoice, CancellationToken cancellationToken = default);
        Task<bool> UpdateInvoiceWithCustomerBalanceAsync(int invoiceId, decimal newAmount, CancellationToken cancellationToken = default);
        Task<bool> VoidInvoiceWithBalanceReversalAsync(int invoiceId, string voidReason, CancellationToken cancellationToken = default);

        // Daily Operations and Reconciliation Support
        Task<IEnumerable<Invoice>> GetDailyInvoicesAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<IEnumerable<Invoice>> GetInvoicesByTruckAndDateAsync(int truckId, DateTime date, CancellationToken cancellationToken = default);
        Task<decimal> GetDailySalesTotalByTruckAsync(int truckId, DateTime date, CancellationToken cancellationToken = default);
        Task<decimal> GetDailyNetWeightByTruckAsync(int truckId, DateTime date, CancellationToken cancellationToken = default);

        // Performance Analytics and Reporting
        Task<Dictionary<int, decimal>> GetTruckSalesPerformanceAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<Dictionary<int, decimal>> GetCustomerPurchaseVolumeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<(decimal TotalSales, decimal TotalWeight, int InvoiceCount)> GetSalesSummaryAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        // Advanced Search and Filtering
        Task<IEnumerable<Invoice>> SearchInvoicesAsync(string searchTerm, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<Invoice>> GetInvoicesByAmountRangeAsync(decimal minAmount, decimal maxAmount, CancellationToken cancellationToken = default);
        Task<IEnumerable<Invoice>> GetLargeInvoicesAsync(decimal thresholdAmount, CancellationToken cancellationToken = default);

        // Data Integrity and Validation
        Task<bool> ValidateInvoiceIntegrityAsync(int invoiceId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Invoice>> GetInvoicesWithDiscrepanciesAsync(CancellationToken cancellationToken = default);
        Task<bool> RecalculateInvoiceTotalsAsync(int invoiceId, CancellationToken cancellationToken = default);

        // Audit and Compliance Support
        Task<IEnumerable<Invoice>> GetModifiedInvoicesAsync(DateTime since, CancellationToken cancellationToken = default);
        Task<Dictionary<string, object>> GetInvoiceAuditTrailAsync(int invoiceId, CancellationToken cancellationToken = default);

        // Batch Operations for Performance Optimization
        Task<IEnumerable<Invoice>> CreateInvoiceBatchAsync(IEnumerable<Invoice> invoices, CancellationToken cancellationToken = default);
        Task<int> UpdateInvoiceStatusBatchAsync(IEnumerable<int> invoiceIds, string status, CancellationToken cancellationToken = default);
    }
}