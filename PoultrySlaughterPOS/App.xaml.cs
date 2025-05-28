using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PoultrySlaughterPOS.Data;
using PoultrySlaughterPOS.Repositories;
using PoultrySlaughterPOS.Services;
using PoultrySlaughterPOS.Services.Repositories;
using PoultrySlaughterPOS.Services.Repositories.Implementations;
using PoultrySlaughterPOS.ViewModels;
using PoultrySlaughterPOS.Views;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace PoultrySlaughterPOS
{
    /// <summary>
    /// Main application class providing comprehensive dependency injection configuration,
    /// logging setup, and lifecycle management for the Poultry Slaughter POS system
    /// </summary>
    public partial class App : Application
    {
        #region Private Fields

        private IHost? _host;
        private IServiceProvider? _serviceProvider;

        #endregion

        #region Properties

        /// <summary>
        /// Global service provider accessible throughout the application
        /// </summary>
        public IServiceProvider Services => _serviceProvider ?? throw new InvalidOperationException("Services not initialized");

        #endregion

        #region Application Lifecycle

        /// <summary>
        /// Application startup with comprehensive initialization and error handling
        /// </summary>
        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Configure Serilog for comprehensive logging
                ConfigureLogging();

                Log.Information("=== Poultry Slaughter POS Application Starting ===");
                Log.Information("Application Version: {Version}", GetApplicationVersion());
                Log.Information("Operating System: {OS}", Environment.OSVersion);
                Log.Information("Machine Name: {MachineName}", Environment.MachineName);
                Log.Information("User: {UserName}", Environment.UserName);

                // Build configuration from multiple sources
                var configuration = BuildConfiguration();

                // Configure and build the dependency injection container
                _host = ConfigureHost(configuration);

                // Start the host services
                await _host.StartAsync();
                _serviceProvider = _host.Services;

                Log.Information("Host services started successfully");

                // Initialize critical application components
                await InitializeApplicationAsync();

                // Create and display the main window
                await ShowMainWindowAsync();

                Log.Information("Application startup completed successfully");
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Critical error during application startup");
                HandleStartupFailure(ex);
            }
        }

        /// <summary>
        /// Application shutdown with proper cleanup and resource disposal
        /// </summary>
        protected override async void OnExit(ExitEventArgs e)
        {
            try
            {
                Log.Information("=== Application Shutdown Initiated ===");

                if (_host != null)
                {
                    Log.Information("Stopping host services...");
                    await _host.StopAsync(TimeSpan.FromSeconds(30));
                    _host.Dispose();
                    Log.Information("Host services stopped successfully");
                }

                Log.Information("=== Application Shutdown Completed ===");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during application shutdown");
            }
            finally
            {
                Log.CloseAndFlush();
                base.OnExit(e);
            }
        }

        #endregion

        #region Configuration Methods

        /// <summary>
        /// Configures Serilog logging with file rotation and structured logging
        /// </summary>
        private void ConfigureLogging()
        {
            var logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logsDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentName()
                .Enrich.WithEnvironmentUserName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .WriteTo.File(
                    path: Path.Combine(logsDirectory, "pos-log-.txt"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    fileSizeLimitBytes: 50 * 1024 * 1024, // 50MB
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .WriteTo.File(
                    path: Path.Combine(logsDirectory, "pos-errors-.txt"),
                    restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 90)
                .CreateLogger();
        }

        /// <summary>
        /// Builds application configuration from multiple sources with environment-specific overrides
        /// </summary>
        private IConfiguration BuildConfiguration()
        {
            try
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json",
                                optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables();

                var configuration = builder.Build();

                Log.Information("Configuration loaded successfully from {ConfigPath}",
                    Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"));

                return configuration;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to build application configuration");
                throw;
            }
        }

        /// <summary>
        /// Configures the host with comprehensive dependency injection and service registration
        /// </summary>
        private IHost ConfigureHost(IConfiguration configuration)
        {
            try
            {
                return Host.CreateDefaultBuilder()
                    .UseSerilog()
                    .ConfigureServices((context, services) =>
                    {
                        ConfigureServices(services, configuration);
                    })
                    .Build();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to configure application host");
                throw;
            }
        }

        /// <summary>
        /// Comprehensive service registration with proper lifetime management and dependency resolution
        /// </summary>
        private void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            try
            {
                Log.Information("Configuring application services...");

                // Register configuration as singleton
                services.AddSingleton(configuration);

                // Configure Entity Framework DbContext with optimized settings
                services.AddDbContext<PoultryDbContext>(options =>
                {
                    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"), sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                        sqlOptions.CommandTimeout(30);
                    });

                    options.EnableSensitiveDataLogging(false);
                    options.EnableServiceProviderCaching(true);
                    options.EnableDetailedErrors(true);

#if DEBUG
                    options.LogTo(message => Log.Debug("EF Core: {Message}", message), LogLevel.Information);
#endif
                }, ServiceLifetime.Scoped);

                // Configure DbContextFactory for repository pattern with connection pooling
                services.AddDbContextFactory<PoultryDbContext>(options =>
                {
                    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"), sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                        sqlOptions.CommandTimeout(30);
                    });

                    options.EnableSensitiveDataLogging(false);
                    options.EnableServiceProviderCaching(false); // Important for factory pattern
                    options.EnableDetailedErrors(true);
                }, ServiceLifetime.Scoped);

                // Register Repository Pattern with comprehensive interface implementations
                RegisterRepositories(services);

                // Register Unit of Work pattern for transaction management
                RegisterUnitOfWork(services);

                // Register Application Services with proper lifecycle management
                RegisterApplicationServices(services);

                // Register MVVM ViewModels with transient lifetime for proper isolation
                RegisterViewModels(services);

                // Register Views with transient lifetime for dynamic creation
                RegisterViews(services);

                // Configure Logging with Serilog integration
                services.AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddSerilog();
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                // Register performance and health check services
                RegisterHealthChecks(services);

                Log.Information("Application services configured successfully. Total services: {ServiceCount}",
                    services.Count);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Critical error during service configuration");
                throw;
            }
        }

        /// <summary>
        /// Registers all repository implementations with proper interface bindings
        /// </summary>
        private void RegisterRepositories(IServiceCollection services)
        {
            services.AddScoped<ITruckRepository, TruckRepository>();
            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<IInvoiceRepository, InvoiceRepository>();
            services.AddScoped<IPaymentRepository, PaymentRepository>();
            services.AddScoped<ITruckLoadRepository, TruckLoadRepository>();
            services.AddScoped<IDailyReconciliationRepository, DailyReconciliationRepository>();
            services.AddScoped<IAuditLogRepository, AuditLogRepository>();

            Log.Debug("Repository services registered successfully");
        }

        /// <summary>
        /// Registers Unit of Work pattern with proper factory injection
        /// </summary>
        private void RegisterUnitOfWork(IServiceCollection services)
        {
            services.AddScoped<IUnitOfWork>(serviceProvider =>
            {
                var context = serviceProvider.GetRequiredService<PoultryDbContext>();
                var contextFactory = serviceProvider.GetRequiredService<IDbContextFactory<PoultryDbContext>>();
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

                return new UnitOfWork(context, contextFactory, loggerFactory);
            });

            Log.Debug("Unit of Work pattern registered successfully");
        }

        /// <summary>
        /// Registers core application services with appropriate lifetimes
        /// </summary>
        private void RegisterApplicationServices(IServiceCollection services)
        {
            // Database and infrastructure services
            services.AddTransient<IDatabaseInitializationService, DatabaseInitializationService>();
            services.AddScoped<IErrorHandlingService, ErrorHandlingService>();

            // Navigation service with singleton pattern for application-wide state
            services.AddSingleton<INavigationService, NavigationService>();

            // Business logic services (add as they are implemented)
            // services.AddScoped<IInvoiceService, InvoiceService>();
            // services.AddScoped<ICustomerService, CustomerService>();
            // services.AddScoped<IReportService, ReportService>();

            Log.Debug("Application services registered successfully");
        }

        /// <summary>
        /// Registers MVVM ViewModels with transient lifetime for proper isolation
        /// </summary>
        private void RegisterViewModels(IServiceCollection services)
        {
            services.AddTransient<TruckLoadingViewModel>();

            // Add additional ViewModels as they are implemented
            // services.AddTransient<PointOfSaleViewModel>();
            // services.AddTransient<CustomerManagementViewModel>();
            // services.AddTransient<ReportsViewModel>();
            // services.AddTransient<TransactionHistoryViewModel>();
            // services.AddTransient<ReconciliationViewModel>();
            // services.AddTransient<DashboardViewModel>();

            Log.Debug("ViewModel services registered successfully");
        }

        /// <summary>
        /// Registers WPF Views with transient lifetime for dynamic creation
        /// </summary>
        private void RegisterViews(IServiceCollection services)
        {
            // Main application window as singleton
            services.AddSingleton<MainWindow>();

            // Module views as transient for multiple instances
            services.AddTransient<TruckLoadingView>();

            // Add additional Views as they are implemented
            // services.AddTransient<PointOfSaleView>();
            // services.AddTransient<CustomerManagementView>();
            // services.AddTransient<ReportsView>();
            // services.AddTransient<TransactionHistoryView>();
            // services.AddTransient<ReconciliationView>();
            // services.AddTransient<DashboardView>();

            Log.Debug("View services registered successfully");
        }

        /// <summary>
        /// Registers health check services for monitoring application state
        /// </summary>
        private void RegisterHealthChecks(IServiceCollection services)
        {
            services.AddHealthChecks()
                .AddDbContextCheck<PoultryDbContext>("database", tags: new[] { "database", "sqlserver" });

            Log.Debug("Health check services registered successfully");
        }

        #endregion

        #region Initialization Methods

        /// <summary>
        /// Initializes critical application components and verifies system readiness
        /// </summary>
        private async Task InitializeApplicationAsync()
        {
            try
            {
                Log.Information("Initializing critical application components...");

                using var scope = Services.CreateScope();

                // Initialize database with comprehensive error handling
                await InitializeDatabaseAsync(scope.ServiceProvider);

                // Verify service dependencies
                await VerifyServiceDependenciesAsync(scope.ServiceProvider);

                // Perform application health checks
                await PerformHealthChecksAsync(scope.ServiceProvider);

                Log.Information("Application initialization completed successfully");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Critical error during application initialization");
                throw;
            }
        }

        /// <summary>
        /// Initializes database schema and performs connection verification
        /// </summary>
        private async Task InitializeDatabaseAsync(IServiceProvider serviceProvider)
        {
            try
            {
                Log.Information("Initializing database...");

                var dbInitService = serviceProvider.GetRequiredService<IDatabaseInitializationService>();
                await dbInitService.InitializeAsync();

                Log.Information("Database initialization completed successfully");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Database initialization failed");
                throw;
            }
        }

        /// <summary>
        /// Verifies that all critical service dependencies are properly resolved
        /// </summary>
        private async Task VerifyServiceDependenciesAsync(IServiceProvider serviceProvider)
        {
            try
            {
                // Verify critical services can be resolved
                var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
                var navigationService = serviceProvider.GetRequiredService<INavigationService>();
                var errorHandling = serviceProvider.GetRequiredService<IErrorHandlingService>();

                // Verify database connectivity
                using var context = await serviceProvider.GetRequiredService<IDbContextFactory<PoultryDbContext>>()
                    .CreateDbContextAsync();
                var canConnect = await context.Database.CanConnectAsync();

                if (!canConnect)
                {
                    throw new InvalidOperationException("Database connectivity verification failed");
                }

                Log.Information("Service dependency verification completed successfully");
                await Task.CompletedTask; // Satisfy async requirement
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Service dependency verification failed");
                throw;
            }
        }

        /// <summary>
        /// Performs comprehensive application health checks
        /// </summary>
        private async Task PerformHealthChecksAsync(IServiceProvider serviceProvider)
        {
            try
            {
                // Verify system resources and readiness
                var healthCheckService = serviceProvider.GetService<HealthCheckService>();

                if (healthCheckService != null)
                {
                    var healthResult = await healthCheckService.CheckHealthAsync();
                    Log.Information("Health check status: {Status}", healthResult.Status);

                    if (healthResult.Status != HealthStatus.Healthy)
                    {
                        Log.Warning("Application health check indicates potential issues");
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Health check execution failed - continuing with startup");
            }
        }

        /// <summary>
        /// Creates and displays the main application window
        /// </summary>
        private async Task ShowMainWindowAsync()
        {
            try
            {
                Log.Information("Creating main application window...");

                var mainWindow = Services.GetRequiredService<MainWindow>();
                mainWindow.Show();

                Log.Information("Main window displayed successfully");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to create or display main window");
                throw;
            }
        }

        #endregion

        #region Error Handling

        /// <summary>
        /// Handles critical startup failures with user notification and graceful shutdown
        /// </summary>
        private void HandleStartupFailure(Exception ex)
        {
            try
            {
                var errorMessage = "فشل في تشغيل البرنامج. يرجى التأكد من:\n" +
                                 "1. تشغيل SQL Server\n" +
                                 "2. صحة اتصال قاعدة البيانات\n" +
                                 "3. صلاحيات النظام المطلوبة\n\n" +
                                 $"تفاصيل الخطأ: {ex.Message}";

                MessageBox.Show(errorMessage, "خطأ في بدء التشغيل",
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK,
                    MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            }
            catch
            {
                // Fallback error message if Arabic display fails
                MessageBox.Show($"Application startup failed:\n{ex.Message}", "Startup Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Global application exception handler for unhandled exceptions
        /// </summary>
        private void Application_DispatcherUnhandledException(object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                Log.Fatal(e.Exception, "Unhandled application exception occurred");

                var errorService = Services?.GetService<IErrorHandlingService>();
                if (errorService != null)
                {
                    var (_, userMessage) = errorService.HandleExceptionAsync(e.Exception, "Application.DispatcherUnhandledException").Result;

                    MessageBox.Show($"حدث خطأ غير متوقع:\n{userMessage}", "خطأ في النظام",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show("حدث خطأ غير متوقع في النظام.", "خطأ",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Error in global exception handler");
                Environment.Exit(1);
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Gets the current application version for logging and diagnostics
        /// </summary>
        private string GetApplicationVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        #endregion
    }
}