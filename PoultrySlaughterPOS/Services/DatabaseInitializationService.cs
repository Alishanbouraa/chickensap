using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Data;

namespace PoultrySlaughterPOS.Services
{
    public interface IDatabaseInitializationService
    {
        Task InitializeAsync();
        Task<bool> TestConnectionAsync();
        Task<bool> EnsureSqlServerInstanceAsync();
    }

    public class DatabaseInitializationService : IDatabaseInitializationService
    {
        private readonly IDbContextFactory<PoultryDbContext> _contextFactory;
        private readonly ILogger<DatabaseInitializationService> _logger;

        public DatabaseInitializationService(
            IDbContextFactory<PoultryDbContext> contextFactory,
            ILogger<DatabaseInitializationService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Starting database initialization for SQL Server...");

                // Verify SQL Server instance is accessible
                if (!await EnsureSqlServerInstanceAsync())
                {
                    throw new InvalidOperationException("SQL Server instance 'posserver' is not accessible. Please ensure SQL Server is running.");
                }

                // Test connection
                if (await TestConnectionAsync())
                {
                    _logger.LogInformation("SQL Server connection successful.");

                    using var context = await _contextFactory.CreateDbContextAsync();

                    // Create database if it doesn't exist
                    await context.Database.EnsureCreatedAsync();
                    _logger.LogInformation("Database 'chicken' created/verified successfully.");

                    // Apply any pending migrations
                    var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                    if (pendingMigrations.Any())
                    {
                        _logger.LogInformation($"Applying {pendingMigrations.Count()} pending migrations...");
                        await context.Database.MigrateAsync();
                        _logger.LogInformation("Migrations applied successfully.");
                    }

                    // Verify table structure
                    await VerifyDatabaseSchemaAsync();

                    _logger.LogInformation("Database initialization completed successfully.");
                }
                else
                {
                    throw new InvalidOperationException("Cannot connect to SQL Server. Please verify connection string and server availability.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database initialization failed: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                _logger.LogInformation("Testing SQL Server connection...");
                using var context = await _contextFactory.CreateDbContextAsync();
                await context.Database.OpenConnectionAsync();
                await context.Database.CloseConnectionAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SQL Server connection test failed: {Message}", ex.Message);
                return false;
            }
        }

        public async Task<bool> EnsureSqlServerInstanceAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var connectionString = context.Database.GetConnectionString();
                _logger.LogInformation("Verifying SQL Server instance with connection: {ConnectionString}",
                    connectionString?.Replace("Password=", "Password=***"));

                // Test server connectivity
                await context.Database.CanConnectAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SQL Server instance verification failed: {Message}", ex.Message);
                return false;
            }
        }

        private async Task VerifyDatabaseSchemaAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Verify all tables exist and get record counts
                var trucksCount = await context.Trucks.CountAsync();
                var customersCount = await context.Customers.CountAsync();
                var truckLoadsCount = await context.TruckLoads.CountAsync();
                var invoicesCount = await context.Invoices.CountAsync();
                var paymentsCount = await context.Payments.CountAsync();
                var reconciliationsCount = await context.DailyReconciliations.CountAsync();
                var auditLogsCount = await context.AuditLogs.CountAsync();

                _logger.LogInformation("Database schema verification completed:");
                _logger.LogInformation("- TRUCKS: {TrucksCount} records", trucksCount);
                _logger.LogInformation("- CUSTOMERS: {CustomersCount} records", customersCount);
                _logger.LogInformation("- TRUCK_LOADS: {TruckLoadsCount} records", truckLoadsCount);
                _logger.LogInformation("- INVOICES: {InvoicesCount} records", invoicesCount);
                _logger.LogInformation("- PAYMENTS: {PaymentsCount} records", paymentsCount);
                _logger.LogInformation("- DAILY_RECONCILIATION: {ReconciliationsCount} records", reconciliationsCount);
                _logger.LogInformation("- AUDIT_LOGS: {AuditLogsCount} records", auditLogsCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database schema verification failed: {Message}", ex.Message);
                throw;
            }
        }
    }
}