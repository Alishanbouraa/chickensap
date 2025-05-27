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

            // Configure DbContext with factory pattern for Unit of Work
            services.AddDbContext<PoultryDbContext>(options =>
            {
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
                options.EnableSensitiveDataLogging(false);
                options.LogTo(Console.WriteLine, LogLevel.Warning);
            });

            // Keep the factory pattern for other components that need it
            services.AddDbContextFactory<PoultryDbContext>(options =>
            {
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
                options.EnableSensitiveDataLogging(false);
                options.LogTo(Console.WriteLine, LogLevel.Warning);
                options.EnableServiceProviderCaching(false);
            });

            // Register repositories
            services.AddScoped<ITruckRepository, TruckRepository>();
            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<IInvoiceRepository, InvoiceRepository>();
            services.AddScoped<IPaymentRepository, PaymentRepository>();
            services.AddScoped<ITruckLoadRepository, TruckLoadRepository>();
            services.AddScoped<IDailyReconciliationRepository, DailyReconciliationRepository>();
            services.AddScoped<IAuditLogRepository, AuditLogRepository>();

            // Register Unit of Work
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Register application services
            services.AddTransient<IDatabaseInitializationService, DatabaseInitializationService>();

            // Register UI components
            services.AddSingleton<MainWindow>();

            // Configure logging
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