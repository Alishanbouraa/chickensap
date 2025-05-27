using PoultrySlaughterPOS.Models;

namespace PoultrySlaughterPOS.Services.Repositories
{
    /// <summary>
    /// Unit of Work pattern implementation providing transactional consistency
    /// across multiple repository operations with comprehensive resource management
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        // Repository Properties - Lazy initialization for optimal performance
        ITruckRepository Trucks { get; }
        ICustomerRepository Customers { get; }
        IInvoiceRepository Invoices { get; }
        IPaymentRepository Payments { get; }
        ITruckLoadRepository TruckLoads { get; }
        IDailyReconciliationRepository DailyReconciliations { get; }
        IAuditLogRepository AuditLogs { get; }

        // Generic Repository Access
        IRepository<T> Repository<T>() where T : class;

        // Transaction Management
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync(string userId, CancellationToken cancellationToken = default);

        // Advanced Transaction Operations
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

        // Bulk Operations Support
        Task<int> ExecuteSqlRawAsync(string sql, params object[] parameters);
        Task<int> ExecuteSqlRawAsync(string sql, CancellationToken cancellationToken, params object[] parameters);

        // Performance Monitoring
        bool HasActiveTransaction { get; }
        int PendingChangesCount { get; }

        // Audit Trail Support
        Task<int> SaveChangesWithAuditAsync(string userId, string operation = "BULK_OPERATION", CancellationToken cancellationToken = default);
    }
}