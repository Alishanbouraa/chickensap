using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Data;
using PoultrySlaughterPOS.Repositories;
using PoultrySlaughterPOS.Services;
using PoultrySlaughterPOS.Services.Repositories;
using PoultrySlaughterPOS.Services.Repositories.Implementations;
using Serilog;
using System.IO;
using System.Windows;

namespace PoultrySlaughterPOS
{
    public partial class App : Application
    {
        private IHost? _host;

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Configure Serilog
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .WriteTo.File("logs/pos-log-.txt", rollingInterval: RollingInterval.Day)
                    .CreateLogger();

                // Build configuration
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                // Configure host with dependency injection
                _host = Host.CreateDefaultBuilder()
                    .UseSerilog()
                    .ConfigureServices((context, services) =>
                    {
                        ConfigureServices(services, configuration);
                    })
                    .Build();

                // Start the host
                await _host.StartAsync();

                // Initialize database
                using (var scope = _host.Services.CreateScope())
                {
                    var dbInitService = scope.ServiceProvider.GetRequiredService<IDatabaseInitializationService>();
                    await dbInitService.InitializeAsync();
                }

                // Create and show main window through DI
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application startup failed");
                MessageBox.Show($"فشل في تشغيل البرنامج:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Add configuration
            services.AddSingleton(configuration);

            // Configure DbContext with scoped lifetime for UnitOfWork pattern
            services.AddDbContext<PoultryDbContext>(options =>
            {
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
                options.EnableSensitiveDataLogging(false);
                options.LogTo(Console.WriteLine, LogLevel.Warning);
            }, ServiceLifetime.Scoped);

            // Configure DbContextFactory for BaseRepository implementations
            services.AddDbContextFactory<PoultryDbContext>(options =>
            {
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
                options.EnableSensitiveDataLogging(false);
                options.LogTo(Console.WriteLine, LogLevel.Warning);
                options.EnableServiceProviderCaching(false);
            }, ServiceLifetime.Scoped);

            // Register repositories with proper dependency injection patterns
            services.AddScoped<ITruckRepository, TruckRepository>();
            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<IInvoiceRepository, InvoiceRepository>();
            services.AddScoped<IPaymentRepository, PaymentRepository>();
            services.AddScoped<ITruckLoadRepository, TruckLoadRepository>();
            services.AddScoped<IDailyReconciliationRepository, DailyReconciliationRepository>();
            services.AddScoped<IAuditLogRepository, AuditLogRepository>();

            // Register Unit of Work with explicit factory registration for proper constructor injection
            services.AddScoped<IUnitOfWork>(serviceProvider =>
            {
                var context = serviceProvider.GetRequiredService<PoultryDbContext>();
                var contextFactory = serviceProvider.GetRequiredService<IDbContextFactory<PoultryDbContext>>();
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

                return new UnitOfWork(context, contextFactory, loggerFactory);
            });

            // Register application services
            services.AddTransient<IDatabaseInitializationService, DatabaseInitializationService>();
            services.AddScoped<IErrorHandlingService, ErrorHandlingService>();

            // Register UI components
            services.AddSingleton<MainWindow>();

            // Configure logging with Serilog integration
            services.AddLogging(builder =>
            {
                builder.AddSerilog();
                builder.SetMinimumLevel(LogLevel.Information);
            });
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }

            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}