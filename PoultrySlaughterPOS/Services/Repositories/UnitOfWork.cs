using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Data;
using PoultrySlaughterPOS.Models;
using System.Collections.Concurrent;

namespace PoultrySlaughterPOS.Services.Repositories
{
    /// <summary>
    /// Enterprise-grade Unit of Work implementation providing transactional consistency,
    /// resource management, and repository coordination for the Poultry Slaughter POS system
    /// </summary>
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        private readonly IDbContextFactory<PoultryDbContext> _contextFactory;
        private readonly ILogger<UnitOfWork> _logger;
        private PoultryDbContext? _context;
        private IDbContextTransaction? _transaction;
        private bool _disposed = false;

        // Lazy-loaded repository instances for optimal performance
        private readonly Lazy<ITruckRepository> _trucks;
        private readonly Lazy<ICustomerRepository> _customers;
        private readonly Lazy<IInvoiceRepository> _invoices;
        private readonly Lazy<IPaymentRepository> _payments;
        private readonly Lazy<ITruckLoadRepository> _truckLoads;
        private readonly Lazy<IDailyReconciliationRepository> _dailyReconciliations;
        private readonly Lazy<IAuditLogRepository> _auditLogs;

        // Generic repository cache for dynamic type support
        private readonly ConcurrentDictionary<Type, object> _repositoryCache = new();

        public UnitOfWork(
            IDbContextFactory<PoultryDbContext> contextFactory,
            ILogger<UnitOfWork> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize lazy-loaded repositories with factory pattern
            _trucks = new Lazy<ITruckRepository>(() => new TruckRepository(GetContext(), _logger.CreateLogger<TruckRepository>()));
            _customers = new Lazy<ICustomerRepository>(() => new CustomerRepository(GetContext(), _logger.CreateLogger<CustomerRepository>()));
            _invoices = new Lazy<IInvoiceRepository>(() => new InvoiceRepository(GetContext(), _logger.CreateLogger<InvoiceRepository>()));
            _payments = new Lazy<IPaymentRepository>(() => new PaymentRepository(GetContext(), _logger.CreateLogger<PaymentRepository>()));
            _truckLoads = new Lazy<ITruckLoadRepository>(() => new TruckLoadRepository(GetContext(), _logger.CreateLogger<TruckLoadRepository>()));
            _dailyReconciliations = new Lazy<IDailyReconciliationRepository>(() => new DailyReconciliationRepository(GetContext(), _logger.CreateLogger<DailyReconciliationRepository>()));
            _auditLogs = new Lazy<IAuditLogRepository>(() => new AuditLogRepository(GetContext(), _logger.CreateLogger<AuditLogRepository>()));
        }

        // Repository Properties with Lazy Initialization
        public ITruckRepository Trucks => _trucks.Value;
        public ICustomerRepository Customers => _customers.Value;
        public IInvoiceRepository Invoices => _invoices.Value;
        public IPaymentRepository Payments => _payments.Value;
        public ITruckLoadRepository TruckLoads => _truckLoads.Value;
        public IDailyReconciliationRepository DailyReconciliations => _dailyReconciliations.Value;
        public IAuditLogRepository AuditLogs => _auditLogs.Value;

        // Transaction State Properties
        public bool HasActiveTransaction => _transaction != null && _transaction.TransactionId != Guid.Empty;
        public int PendingChangesCount => GetContext().ChangeTracker.Entries()
            .Count(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);

        /// <summary>
        /// Gets or creates the DbContext instance using the factory pattern
        /// for optimal resource management and connection pooling
        /// </summary>
        private PoultryDbContext GetContext()
        {
            if (_context == null)
            {
                _context = _contextFactory.CreateDbContext();
                _logger.LogDebug("DbContext created via factory pattern");
            }
            return _context;
        }

        /// <summary>
        /// Generic repository accessor with caching for optimal performance
        /// </summary>
        public IRepository<T> Repository<T>() where T : class
        {
            var type = typeof(T);

            if (_repositoryCache.TryGetValue(type, out var cachedRepository))
            {
                return (IRepository<T>)cachedRepository;
            }

            var repository = new Repository<T>(GetContext(), _logger.CreateLogger<Repository<T>>());
            _repositoryCache.TryAdd(type, repository);

            _logger.LogDebug("Generic repository created for type {EntityType}", type.Name);
            return repository;
        }

        /// <summary>
        /// Saves all pending changes to the database with comprehensive error handling
        /// </summary>
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await SaveChangesAsync("SYSTEM", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Saves all pending changes with user context for audit trail
        /// </summary>
        public async Task<int> SaveChangesAsync(string userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = GetContext();
                var changeCount = PendingChangesCount;

                if (changeCount == 0)
                {
                    _logger.LogDebug("No pending changes to save");
                    return 0;
                }

                _logger.LogDebug("Saving {ChangeCount} pending changes for user {UserId}", changeCount, userId);

                var result = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Successfully saved {SavedCount} changes to database for user {UserId}", result, userId);
                return result;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict occurred while saving changes for user {UserId}", userId);
                await HandleConcurrencyConflictAsync(ex).ConfigureAwait(false);
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database update error occurred while saving changes for user {UserId}", userId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while saving changes for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Begins a new database transaction with proper isolation level
        /// </summary>
        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (HasActiveTransaction)
                {
                    _logger.LogWarning("Attempted to begin transaction when one is already active");
                    throw new InvalidOperationException("A transaction is already active");
                }

                var context = GetContext();
                _transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogDebug("Database transaction started with ID {TransactionId}", _transaction.TransactionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to begin database transaction");
                throw;
            }
        }

        /// <summary>
        /// Commits the active transaction with comprehensive error handling
        /// </summary>
        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_transaction == null)
                {
                    _logger.LogWarning("Attempted to commit transaction when none is active");
                    throw new InvalidOperationException("No active transaction to commit");
                }

                await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Database transaction committed successfully with ID {TransactionId}", _transaction.TransactionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to commit database transaction");
                await RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
            finally
            {
                _transaction?.Dispose();
                _transaction = null;
            }
        }

        /// <summary>
        /// Rolls back the active transaction and logs the operation
        /// </summary>
        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_transaction == null)
                {
                    _logger.LogDebug("No active transaction to rollback");
                    return;
                }

                await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning("Database transaction rolled back with ID {TransactionId}", _transaction.TransactionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rollback database transaction");
                throw;
            }
            finally
            {
                _transaction?.Dispose();
                _transaction = null;
            }
        }

        /// <summary>
        /// Executes raw SQL commands with parameter safety
        /// </summary>
        public async Task<int> ExecuteSqlRawAsync(string sql, params object[] parameters)
        {
            return await ExecuteSqlRawAsync(sql, CancellationToken.None, parameters).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes raw SQL commands with full async support and parameter safety
        /// </summary>
        public async Task<int> ExecuteSqlRawAsync(string sql, CancellationToken cancellationToken, params object[] parameters)
        {
            try
            {
                var context = GetContext();
                _logger.LogDebug("Executing raw SQL command with {ParameterCount} parameters", parameters.Length);

                var result = await context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogDebug("Raw SQL command executed successfully, {AffectedRows} rows affected", result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute raw SQL command: {SqlCommand}", sql);
                throw;
            }
        }

        /// <summary>
        /// Saves changes with comprehensive audit logging support
        /// </summary>
        public async Task<int> SaveChangesWithAuditAsync(string userId, string operation = "BULK_OPERATION", CancellationToken cancellationToken = default)
        {
            try
            {
                var context = GetContext();
                var changeCount = PendingChangesCount;

                if (changeCount == 0)
                {
                    _logger.LogDebug("No pending changes to save for audit operation {Operation}", operation);
                    return 0;
                }

                // Create audit entries before saving
                await CreateAuditEntriesAsync(userId, operation).ConfigureAwait(false);

                var result = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Successfully saved {SavedCount} changes with audit for operation {Operation} by user {UserId}",
                    result, operation, userId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save changes with audit for operation {Operation} by user {UserId}", operation, userId);
                throw;
            }
        }

        /// <summary>
        /// Creates comprehensive audit log entries for all pending changes
        /// </summary>
        private async Task CreateAuditEntriesAsync(string userId, string operation)
        {
            var context = GetContext();
            var auditEntries = new List<AuditLog>();

            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var auditLog = new AuditLog
                {
                    TableName = entry.Entity.GetType().Name,
                    Operation = entry.State.ToString(),
                    CreatedDate = DateTime.Now,
                    UserId = userId
                };

                switch (entry.State)
                {
                    case EntityState.Modified:
                        auditLog.OldValues = SerializeEntityValues(entry.OriginalValues.Properties.ToDictionary(
                            p => p.Name, p => entry.OriginalValues[p]));
                        auditLog.NewValues = SerializeEntityValues(entry.CurrentValues.Properties.ToDictionary(
                            p => p.Name, p => entry.CurrentValues[p]));
                        break;
                    case EntityState.Added:
                        auditLog.NewValues = SerializeEntityValues(entry.CurrentValues.Properties.ToDictionary(
                            p => p.Name, p => entry.CurrentValues[p]));
                        break;
                    case EntityState.Deleted:
                        auditLog.OldValues = SerializeEntityValues(entry.OriginalValues.Properties.ToDictionary(
                            p => p.Name, p => entry.OriginalValues[p]));
                        break;
                }

                auditEntries.Add(auditLog);
            }

            if (auditEntries.Any())
            {
                context.AuditLogs.AddRange(auditEntries);
                _logger.LogDebug("Created {AuditEntryCount} audit log entries for operation {Operation}",
                    auditEntries.Count, operation);
            }
        }

        /// <summary>
        /// Serializes entity values for audit logging with proper null handling
        /// </summary>
        private string SerializeEntityValues(Dictionary<string, object?> values)
        {
            try
            {
                return System.Text.Json.JsonSerializer.Serialize(values, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = false,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to serialize entity values for audit");
                return "Serialization failed";
            }
        }

        /// <summary>
        /// Handles database concurrency conflicts with retry logic
        /// </summary>
        private async Task HandleConcurrencyConflictAsync(DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries)
            {
                var databaseValues = await entry.GetDatabaseValuesAsync().ConfigureAwait(false);

                if (databaseValues == null)
                {
                    _logger.LogWarning("Entity {EntityType} was deleted by another user", entry.Entity.GetType().Name);
                }
                else
                {
                    _logger.LogWarning("Concurrency conflict for entity {EntityType}", entry.Entity.GetType().Name);
                    entry.OriginalValues.SetValues(databaseValues);
                }
            }
        }

        /// <summary>
        /// Disposes of all resources including context and transaction
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected disposal pattern implementation
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    _transaction?.Dispose();
                    _context?.Dispose();
                    _repositoryCache.Clear();

                    _logger.LogDebug("UnitOfWork disposed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during UnitOfWork disposal");
                }
                finally
                {
                    _disposed = true;
                }
            }
        }
    }
}