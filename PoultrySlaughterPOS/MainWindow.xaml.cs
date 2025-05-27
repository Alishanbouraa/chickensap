using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Data;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace PoultrySlaughterPOS
{
    public partial class MainWindow : Window
    {
        private readonly IDbContextFactory<PoultryDbContext> _contextFactory;
        private readonly ILogger<MainWindow> _logger;

        // Constructor for Dependency Injection
        public MainWindow(IDbContextFactory<PoultryDbContext> contextFactory, ILogger<MainWindow> logger)
        {
            InitializeComponent();
            _contextFactory = contextFactory;
            _logger = logger;

            Title = "نظام إدارة مسلخ الدجاج - Poultry Slaughter POS";
            WindowState = WindowState.Maximized;

            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("Main window loaded successfully");

                // Test database connection using factory pattern
                using var context = await _contextFactory.CreateDbContextAsync();
                var trucksCount = await context.Trucks.CountAsync();
                var customersCount = await context.Customers.CountAsync();

                StatusTextBlock.Text = $"قاعدة البيانات متصلة بنجاح\nالشاحنات: {trucksCount} | الزبائن: {customersCount}";
                _logger.LogInformation("Database connection verified from main window - Trucks: {TrucksCount}, Customers: {CustomersCount}",
                    trucksCount, customersCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading main window");
                StatusTextBlock.Text = $"خطأ في الاتصال بقاعدة البيانات: {ex.Message}";
                MessageBox.Show($"خطأ في تحميل البيانات:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
