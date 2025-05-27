using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Data;
using PoultrySlaughterPOS.Services;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PoultrySlaughterPOS
{
    /// <summary>
    /// Main application window providing comprehensive navigation framework,
    /// system status monitoring, and centralized UI management for the Poultry Slaughter POS system
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Private Fields

        private readonly IDbContextFactory<PoultryDbContext> _contextFactory;
        private readonly ILogger<MainWindow> _logger;
        private readonly INavigationService _navigationService;
        private readonly IServiceProvider _serviceProvider;
        private readonly DispatcherTimer _systemTimer;
        private readonly DispatcherTimer _databaseHealthTimer;

        private Button? _activeNavigationButton;
        private bool _isInitialized = false;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes the main window with comprehensive dependency injection support
        /// and establishes core system monitoring capabilities
        /// </summary>
        public MainWindow(
            IDbContextFactory<PoultryDbContext> contextFactory,
            ILogger<MainWindow> logger,
            IServiceProvider serviceProvider)
        {
            InitializeComponent();

            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            // Initialize navigation service
            _navigationService = _serviceProvider.GetRequiredService<INavigationService>();
            _navigationService.NavigationChanged += OnNavigationChanged;

            // Set up system monitoring timers
            _systemTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _systemTimer.Tick += SystemTimer_Tick;

            _databaseHealthTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _databaseHealthTimer.Tick += DatabaseHealthTimer_Tick;

            // Configure window properties
            ConfigureWindowProperties();

            // Set up event handlers
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            SizeChanged += MainWindow_SizeChanged;

            _logger.LogInformation("MainWindow initialized successfully");
        }

        #endregion

        #region Window Lifecycle Events

        /// <summary>
        /// Handles window loaded event with comprehensive system initialization
        /// </summary>
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isInitialized) return;

                _logger.LogInformation("MainWindow loading initiated");

                // Show loading overlay
                ShowLoadingOverlay(true);

                // Initialize system components
                await InitializeSystemAsync();

                // Start monitoring timers
                _systemTimer.Start();
                _databaseHealthTimer.Start();

                // Set initial navigation state
                SetActiveNavigationButton(DashboardButton);
                UpdateCurrentViewLabel("الصفحة الرئيسية");

                _isInitialized = true;
                _logger.LogInformation("MainWindow loading completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during MainWindow initialization");
                await HandleInitializationError(ex);
            }
            finally
            {
                ShowLoadingOverlay(false);
            }
        }

        /// <summary>
        /// Handles window closing event with proper cleanup and resource disposal
        /// </summary>
        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                _logger.LogInformation("MainWindow closing initiated");

                // Stop monitoring timers
                _systemTimer?.Stop();
                _databaseHealthTimer?.Stop();

                // Cleanup navigation service
                _navigationService.NavigationChanged -= OnNavigationChanged;

                // Dispose navigation service if needed
                if (_navigationService is IDisposable disposableNavigation)
                {
                    disposableNavigation.Dispose();
                }

                _logger.LogInformation("MainWindow cleanup completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during MainWindow cleanup");
            }
        }

        /// <summary>
        /// Handles window size changed event for responsive UI adjustments
        /// </summary>
        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                // Implement responsive UI adjustments based on window size
                if (e.NewSize.Width < 1200)
                {
                    // Compact navigation layout
                    AdjustNavigationForCompactView();
                }
                else
                {
                    // Full navigation layout
                    AdjustNavigationForFullView();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during window size adjustment");
            }
        }

        #endregion

        #region Navigation Event Handlers

        /// <summary>
        /// Handles dashboard navigation with proper button state management
        /// </summary>
        private async void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            await HandleNavigationAsync(async () =>
            {
                _navigationService.ClearNavigation();
                SetActiveNavigationButton(DashboardButton);
                UpdateCurrentViewLabel("الصفحة الرئيسية");
            }, "Dashboard");
        }

        /// <summary>
        /// Handles truck loading module navigation
        /// </summary>
        private async void TruckLoadingButton_Click(object sender, RoutedEventArgs e)
        {
            await HandleNavigationAsync(async () =>
            {
                await _navigationService.NavigateToTruckLoadingAsync();
                SetActiveNavigationButton(TruckLoadingButton);
                UpdateCurrentViewLabel("إدارة تحميل الشاحنات");
            }, "TruckLoading");
        }

        /// <summary>
        /// Handles point of sale module navigation
        /// </summary>
        private async void PointOfSaleButton_Click(object sender, RoutedEventArgs e)
        {
            await HandleNavigationAsync(async () =>
            {
                await _navigationService.NavigateToPointOfSaleAsync();
                SetActiveNavigationButton(PointOfSaleButton);
                UpdateCurrentViewLabel("نقطة البيع");
            }, "PointOfSale");
        }

        /// <summary>
        /// Handles customer management module navigation
        /// </summary>
        private async void CustomerManagementButton_Click(object sender, RoutedEventArgs e)
        {
            await HandleNavigationAsync(async () =>
            {
                await _navigationService.NavigateToCustomerManagementAsync();
                SetActiveNavigationButton(CustomerManagementButton);
                UpdateCurrentViewLabel("إدارة الزبائن");
            }, "CustomerManagement");
        }

        /// <summary>
        /// Handles transaction history module navigation
        /// </summary>
        private async void TransactionHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            await HandleNavigationAsync(async () =>
            {
                await _navigationService.NavigateToTransactionHistoryAsync();
                SetActiveNavigationButton(TransactionHistoryButton);
                UpdateCurrentViewLabel("تاريخ المعاملات");
            }, "TransactionHistory");
        }

        /// <summary>
        /// Handles reports module navigation
        /// </summary>
        private async void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            await HandleNavigationAsync(async () =>
            {
                await _navigationService.NavigateToReportsAsync();
                SetActiveNavigationButton(ReportsButton);
                UpdateCurrentViewLabel("التقارير والتحليلات");
            }, "Reports");
        }

        /// <summary>
        /// Handles reconciliation module navigation
        /// </summary>
        private async void ReconciliationButton_Click(object sender, RoutedEventArgs e)
        {
            await HandleNavigationAsync(async () =>
            {
                await _navigationService.NavigateToReconciliationAsync();
                SetActiveNavigationButton(ReconciliationButton);
                UpdateCurrentViewLabel("التسوية اليومية");
            }, "Reconciliation");
        }

        /// <summary>
        /// Handles settings module navigation
        /// </summary>
        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            await HandleNavigationAsync(async () =>
            {
                await _navigationService.NavigateToSettingsAsync();
                UpdateCurrentViewLabel("إعدادات النظام");
            }, "Settings");
        }

        /// <summary>
        /// Handles application refresh functionality
        /// </summary>
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("Manual application refresh initiated");
                ShowLoadingOverlay(true);

                // Refresh current view data
                await RefreshCurrentViewAsync();

                // Update system status
                await UpdateSystemStatusAsync();

                StatusLabel.Text = "تم التحديث بنجاح";
                _logger.LogInformation("Manual application refresh completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during manual application refresh");
                StatusLabel.Text = "خطأ في التحديث";

                MessageBox.Show("حدث خطأ أثناء تحديث البيانات", "خطأ في التحديث",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                ShowLoadingOverlay(false);
            }
        }

        #endregion

        #region Quick Access Card Events

        /// <summary>
        /// Handles truck loading quick access card click
        /// </summary>
        private async void TruckLoadingCard_Click(object sender, MouseButtonEventArgs e)
        {
            await TruckLoadingButton_Click(TruckLoadingButton, new RoutedEventArgs());
        }

        /// <summary>
        /// Handles point of sale quick access card click
        /// </summary>
        private async void PointOfSaleCard_Click(object sender, MouseButtonEventArgs e)
        {
            await PointOfSaleButton_Click(PointOfSaleButton, new RoutedEventArgs());
        }

        /// <summary>
        /// Handles customer management quick access card click
        /// </summary>
        private async void CustomerManagementCard_Click(object sender, MouseButtonEventArgs e)
        {
            await CustomerManagementButton_Click(CustomerManagementButton, new RoutedEventArgs());
        }

        #endregion

        #region Timer Event Handlers

        /// <summary>
        /// Updates system time display and performs periodic system checks
        /// </summary>
        private void SystemTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var now = DateTime.Now;
                SystemDateText.Text = $"التاريخ: {now:yyyy/MM/dd}";
                SystemTimeText.Text = $"الوقت: {now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error updating system time display");
            }
        }

        /// <summary>
        /// Performs periodic database health monitoring
        /// </summary>
        private async void DatabaseHealthTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                await UpdateDatabaseStatusAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during database health check");
            }
        }

        #endregion

        #region Navigation Management

        /// <summary>
        /// Handles navigation change events with comprehensive UI synchronization
        /// </summary>
        private void OnNavigationChanged(object? sender, NavigationEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    MainContentPresenter.Content = e.View;
                    WelcomeScreen.Visibility = e.View == null ? Visibility.Visible : Visibility.Collapsed;

                    // Apply content fade-in animation
                    if (e.View != null && Resources["ContentFadeIn"] is System.Windows.Media.Animation.Storyboard fadeInStoryboard)
                    {
                        fadeInStoryboard.Begin(MainContentPresenter);
                    }

                    _logger.LogDebug("Navigation completed to view: {ViewName}", e.ViewName);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during navigation change handling");
            }
        }

        /// <summary>
        /// Generic navigation handler with comprehensive error management
        /// </summary>
        private async Task HandleNavigationAsync(Func<Task> navigationAction, string moduleName)
        {
            try
            {
                if (_navigationService.IsNavigating)
                {
                    _logger.LogDebug("Navigation already in progress - ignoring request for {ModuleName}", moduleName);
                    return;
                }

                _logger.LogInformation("Navigation initiated to {ModuleName}", moduleName);
                ShowLoadingOverlay(true);

                await navigationAction();

                StatusLabel.Text = $"تم الانتقال إلى {GetModuleDisplayName(moduleName)}";
                _logger.LogInformation("Navigation to {ModuleName} completed successfully", moduleName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Navigation error for module {ModuleName}", moduleName);
                StatusLabel.Text = "خطأ في التنقل";

                MessageBox.Show($"حدث خطأ أثناء الانتقال إلى {GetModuleDisplayName(moduleName)}",
                    "خطأ في التنقل", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                ShowLoadingOverlay(false);
            }
        }

        /// <summary>
        /// Sets the active navigation button with proper visual state management
        /// </summary>
        private void SetActiveNavigationButton(Button activeButton)
        {
            try
            {
                // Reset all navigation buttons to normal state
                var navigationButtons = new[]
                {
                    DashboardButton, TruckLoadingButton, PointOfSaleButton,
                    CustomerManagementButton, TransactionHistoryButton,
                    ReportsButton, ReconciliationButton
                };

                foreach (var button in navigationButtons)
                {
                    button.Style = Resources["NavigationButtonStyle"] as Style;
                }

                // Set active button style
                if (activeButton != null)
                {
                    activeButton.Style = Resources["ActiveNavigationButtonStyle"] as Style;
                    _activeNavigationButton = activeButton;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error setting active navigation button");
            }
        }

        #endregion

        #region System Status Management

        /// <summary>
        /// Initializes core system components with comprehensive error handling
        /// </summary>
        private async Task InitializeSystemAsync()
        {
            try
            {
                _logger.LogInformation("Initializing system components...");

                // Test database connectivity
                await UpdateDatabaseStatusAsync();

                // Update system status
                await UpdateSystemStatusAsync();

                // Initialize navigation framework
                _navigationService.ClearNavigation();

                _logger.LogInformation("System components initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during system initialization");
                throw;
            }
        }

        /// <summary>
        /// Updates database connectivity status with visual indicators
        /// </summary>
        private async Task UpdateDatabaseStatusAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var canConnect = await context.Database.CanConnectAsync();

                Dispatcher.Invoke(() =>
                {
                    if (canConnect)
                    {
                        DatabaseStatusIndicator.Fill = new SolidColorBrush(Colors.LimeGreen);
                        DatabaseStatusText.Text = "قاعدة البيانات";
                        DatabaseInfoLabel.Text = "متصل";
                    }
                    else
                    {
                        DatabaseStatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                        DatabaseStatusText.Text = "قاعدة البيانات";
                        DatabaseInfoLabel.Text = "غير متصل";
                    }
                });

                // Update detailed database information
                if (canConnect)
                {
                    var trucksCount = await context.Trucks.CountAsync();
                    var customersCount = await context.Customers.CountAsync();

                    Dispatcher.Invoke(() =>
                    {
                        StatusTextBlock.Text = $"قاعدة البيانات متصلة بنجاح - الشاحنات: {trucksCount} | الزبائن: {customersCount}";
                    });

                    _logger.LogDebug("Database connection verified - Trucks: {TrucksCount}, Customers: {CustomersCount}",
                        trucksCount, customersCount);
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        StatusTextBlock.Text = "تعذر الاتصال بقاعدة البيانات";
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating database status");

                Dispatcher.Invoke(() =>
                {
                    DatabaseStatusIndicator.Fill = new SolidColorBrush(Colors.Orange);
                    DatabaseStatusText.Text = "قاعدة البيانات";
                    DatabaseInfoLabel.Text = "خطأ";
                    StatusTextBlock.Text = $"خطأ في الاتصال بقاعدة البيانات: {ex.Message}";
                });
            }
        }

        /// <summary>
        /// Updates comprehensive system status information
        /// </summary>
        private async Task UpdateSystemStatusAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    // Perform system health checks
                    var availableMemory = GC.GetTotalMemory(false);
                    var processorCount = Environment.ProcessorCount;

                    Dispatcher.Invoke(() =>
                    {
                        StatusLabel.Text = "النظام جاهز";
                    });

                    _logger.LogDebug("System status updated - Memory: {Memory}MB, Processors: {ProcessorCount}",
                        availableMemory / (1024 * 1024), processorCount);
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error updating system status");
            }
        }

        #endregion

        #region UI Management

        /// <summary>
        /// Configures window properties and initial appearance
        /// </summary>
        private void ConfigureWindowProperties()
        {
            Title = "نظام إدارة مسلخ الدجاج - Poultry Slaughter POS";
            WindowState = WindowState.Maximized;
            MinWidth = 1000;
            MinHeight = 600;
        }

        /// <summary>
        /// Shows or hides the loading overlay with smooth transitions
        /// </summary>
        private void ShowLoadingOverlay(bool show)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    LoadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error toggling loading overlay");
            }
        }

        /// <summary>
        /// Updates the current view label in the status bar
        /// </summary>
        private void UpdateCurrentViewLabel(string viewName)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    CurrentViewLabel.Text = viewName;
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error updating current view label");
            }
        }

        /// <summary>
        /// Adjusts navigation layout for compact window sizes
        /// </summary>
        private void AdjustNavigationForCompactView()
        {
            // Implementation for responsive navigation layout
            // This can be expanded based on specific UI requirements
        }

        /// <summary>
        /// Adjusts navigation layout for full window sizes
        /// </summary>
        private void AdjustNavigationForFullView()
        {
            // Implementation for full navigation layout
            // This can be expanded based on specific UI requirements
        }

        /// <summary>
        /// Refreshes data in the currently active view
        /// </summary>
        private async Task RefreshCurrentViewAsync()
        {
            try
            {
                if (_navigationService.CurrentView != null)
                {
                    // Check if current view has a refresh method
                    var currentView = _navigationService.CurrentView;

                    // Use reflection to call refresh method if it exists
                    var refreshMethod = currentView.GetType().GetMethod("RefreshAsync");
                    if (refreshMethod != null)
                    {
                        var task = refreshMethod.Invoke(currentView, null) as Task;
                        if (task != null)
                        {
                            await task;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error refreshing current view");
            }
        }

        #endregion

        #region Error Handling

        /// <summary>
        /// Handles critical initialization errors with user notification
        /// </summary>
        private async Task HandleInitializationError(Exception ex)
        {
            try
            {
                var errorMessage = "فشل في تهيئة النظام:\n" +
                                 $"{ex.Message}\n\n" +
                                 "يرجى التأكد من:\n" +
                                 "1. تشغيل SQL Server\n" +
                                 "2. صحة إعدادات قاعدة البيانات\n" +
                                 "3. توفر صلاحيات النظام المطلوبة";

                MessageBox.Show(errorMessage, "خطأ في التهيئة",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                StatusLabel.Text = "خطأ في التهيئة";
                StatusTextBlock.Text = "فشل في تهيئة النظام - يرجى إعادة تشغيل التطبيق";
            }
            catch (Exception handlingEx)
            {
                _logger.LogFatal(handlingEx, "Critical error in error handling");
            }

            await Task.CompletedTask;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Gets localized display name for module identifiers
        /// </summary>
        private string GetModuleDisplayName(string moduleName)
        {
            return moduleName switch
            {
                "Dashboard" => "الصفحة الرئيسية",
                "TruckLoading" => "إدارة تحميل الشاحنات",
                "PointOfSale" => "نقطة البيع",
                "CustomerManagement" => "إدارة الزبائن",
                "TransactionHistory" => "تاريخ المعاملات",
                "Reports" => "التقارير والتحليلات",
                "Reconciliation" => "التسوية اليومية",
                "Settings" => "إعدادات النظام",
                _ => moduleName
            };
        }

        #endregion
    }
}