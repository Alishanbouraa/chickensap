using PoultrySlaughterPOS.Models;

namespace PoultrySlaughterPOS.Services.Repositories
{
    /// <summary>
    /// Invoice repository interface providing comprehensive sales transaction management.
    /// Implements high-performance invoice processing, financial calculations, and business intelligence
    /// optimized for concurrent multi-terminal POS operations with transactional integrity.
    /// </summary>
    public interface IInvoiceRepository : IRepository<Invoice>
    {
        // Core invoice operations for POS transactions
        Task<Invoice?> GetInvoiceByNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default);
        Task<Invoice?> GetInvoiceWithDetailsAsync(int invoiceId, CancellationToken cancellationToken = default);
        Task<string> GenerateInvoiceNumberAsync(CancellationToken cancellationToken = default);
        Task<Invoice> CreateInvoiceWithTransactionAsync(Invoice invoice, CancellationToken cancellationToken = default);

        // Daily operations and sales management
        Task<IEnumerable<Invoice>> GetInvoicesByDateAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<IEnumerable<Invoice>> GetInvoicesByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<Invoice>> GetTodaysInvoicesAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Invoice>> GetInvoicesByTruckAsync(int truckId, DateTime? date = null, CancellationToken cancellationToken = default);

        // Customer-specific invoice operations
        Task<IEnumerable<Invoice>> GetCustomerInvoicesAsync(int customerId, int? limit = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<Invoice>> GetCustomerOutstandingInvoicesAsync(int customerId, CancellationToken cancellationToken = default);
        Task<Invoice?> GetCustomerLastInvoiceAsync(int customerId, CancellationToken cancellationToken = default);

        // Financial analytics and reporting
        Task<decimal> GetTotalSalesAmountAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
        Task<(decimal TotalAmount, decimal TotalWeight, int InvoiceCount)> GetSalesSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
        Task<Dictionary<int, decimal>> GetSalesByTruckAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
        Task<Dictionary<int, decimal>> GetSalesByCustomerAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);

        // Weight and quantity analytics for reconciliation
        Task<decimal> GetTotalWeightSoldAsync(int? truckId = null, DateTime? date = null, CancellationToken cancellationToken = default);
        Task<Dictionary<int, decimal>> GetWeightSoldByTruckAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<(decimal TotalGrossWeight, decimal TotalNetWeight, int TotalCages)> GetWeightSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);

        // Performance and trend analysis
        Task<IEnumerable<(DateTime Date, decimal Amount, int Count)>> GetDailySalesAnalysisAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<(int Hour, decimal Amount, int Count)>> GetHourlySalesAnalysisAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<Dictionary<string, decimal>> GetSalesByPaymentMethodAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);

        // Price and margin analysis
        Task<(decimal MinPrice, decimal MaxPrice, decimal AvgPrice)> GetPriceAnalysisAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
        Task<decimal> GetAverageDiscountPercentageAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<(decimal UnitPrice, int InvoiceCount, decimal TotalAmount)>> GetPriceDistributionAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);

        // Search and filtering operations
        Task<IEnumerable<Invoice>> SearchInvoicesAsync(string searchTerm, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
        Task<(IEnumerable<Invoice> Invoices, int TotalCount)> GetInvoicesPagedAsync(int pageNumber, int pageSize, int? customerId = null, int? truckId = null, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);

        // Business validation and integrity
        Task<bool> InvoiceNumberExistsAsync(string invoiceNumber, int? excludeInvoiceId = null, CancellationToken cancellationToken = default);
        Task<bool> CanDeleteInvoiceAsync(int invoiceId, CancellationToken cancellationToken = default);
        Task<int> GetInvoiceCountAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);

        // Reconciliation and audit support
        Task<IEnumerable<Invoice>> GetUnreconciledInvoicesAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<Dictionary<int, (decimal LoadedWeight, decimal SoldWeight, decimal Variance)>> GetTruckReconciliationDataAsync(DateTime date, CancellationToken cancellationToken = default);
        Task<IEnumerable<Invoice>> GetInvoicesRequiringAuditAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

        // Advanced querying for business intelligence
        Task<IEnumerable<(int TruckId, DateTime Date, decimal Efficiency)>> GetTruckEfficiencyAnalysisAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
        Task<Dictionary<int, List<(DateTime Date, decimal Amount)>>> GetCustomerPurchasePatternAsync(IEnumerable<int> customerIds, int monthsBack = 6, CancellationToken cancellationToken = default);
        Task<(decimal TotalRevenue, decimal AverageInvoiceValue, decimal LargestInvoice)> GetRevenueKPIsAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
    }
}