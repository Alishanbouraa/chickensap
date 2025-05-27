using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Views;
using System;
using System.ComponentModel;
using System.Windows.Controls;

namespace PoultrySlaughterPOS.Services
{
    /// <summary>
    /// Enterprise-grade navigation service providing centralized view management and routing
    /// for the Poultry Slaughter POS application with comprehensive module support and lifecycle management
    /// </summary>
    public interface INavigationService : INotifyPropertyChanged
    {
        /// <summary>
        /// Event triggered when navigation state changes
        /// </summary>
        event EventHandler<NavigationEventArgs>? NavigationChanged;

        /// <summary>
        /// Current view being displayed in the main content area
        /// </summary>
        UserControl? CurrentView { get; }

        /// <summary>
        /// Name identifier of the currently active view module
        /// </summary>
        string CurrentViewName { get; }

        /// <summary>
        /// Navigation history for back/forward functionality
        /// </summary>
        IReadOnlyList<string> NavigationHistory { get; }

        /// <summary>
        /// Indicates if navigation operations are currently in progress
        /// </summary>
        bool IsNavigating { get; }

        /// <summary>
        /// Navigate to the truck loading management module
        /// </summary>
        Task NavigateToTruckLoadingAsync();

        /// <summary>
        /// Navigate to the point-of-sale transaction module
        /// </summary>
        Task NavigateToPointOfSaleAsync();

        /// <summary>
        /// Navigate to the customer account management module
        /// </summary>
        Task NavigateToCustomerManagementAsync();

        /// <summary>
        /// Navigate to the reporting and analytics module
        /// </summary>
        Task NavigateToReportsAsync();

        /// <summary>
        /// Navigate to the transaction history module
        /// </summary>
        Task NavigateToTransactionHistoryAsync();

        /// <summary>
        /// Navigate to the reconciliation module
        /// </summary>
        Task NavigateToReconciliationAsync();

        /// <summary>
        /// Navigate to the main dashboard overview
        /// </summary>
        Task NavigateToDashboardAsync();

        /// <summary>
        /// Navigate to the system settings module
        /// </summary>
        Task NavigateToSettingsAsync();

        /// <summary>
        /// Navigate back to the previous view in history
        /// </summary>
        Task NavigateBackAsync();

        /// <summary>
        /// Clear current view and return to welcome screen
        /// </summary>
        void ClearNavigation();

        /// <summary>
        /// Check if a specific module can be navigated to based on current state
        /// </summary>
        bool CanNavigateTo(string moduleName);
    }

    /// <summary>
    /// Production navigation service implementation with comprehensive error handling,
    /// performance optimization, and extensive logging for enterprise POS operations
    /// </summary>
    public class NavigationService : INavigationService, IDisposable
    {
        #region Private Fields

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NavigationService> _logger;
        private readonly List<string> _navigationHistory;
        private readonly Dictionary<string, Type> _viewTypeRegistry;
        private readonly Dictionary<string, UserControl> _viewCache;

        private UserControl? _currentView;
        private string _currentViewName = "Welcome";
        private bool _isNavigating;
        private bool _disposed;

        #endregion

        #region Events

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<NavigationEventArgs>? NavigationChanged;

        #endregion

        #region Properties

        public UserControl? CurrentView
        {
            get => _currentView;
            private set
            {
                if (_currentView != value)
                {
                    _currentView = value;
                    OnPropertyChanged(nameof(CurrentView));
                    OnNavigationChanged(_currentViewName, value);
                }
            }
        }

        public string CurrentViewName
        {
            get => _currentViewName;
            private set
            {
                if (_currentViewName != value)
                {
                    _currentViewName = value;
                    OnPropertyChanged(nameof(CurrentViewName));
                }
            }
        }

        public IReadOnlyList<string> NavigationHistory => _navigationHistory.AsReadOnly();

        public bool IsNavigating
        {
            get => _isNavigating;
            private set
            {
                if (_isNavigating != value)
                {
                    _isNavigating = value;
                    OnPropertyChanged(nameof(IsNavigating));
                }
            }
        }

        #endregion

        #region Constructor

        public NavigationService(IServiceProvider serviceProvider, ILogger<NavigationService> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _navigationHistory = new List<string>();
            _viewTypeRegistry = new Dictionary<string, Type>();
            _viewCache = new Dictionary<string, UserControl>();

            InitializeViewRegistry();

            _logger.LogInformation("NavigationService initialized successfully with {ViewCount} registered views",
                _viewTypeRegistry.Count);
        }

        #endregion

        #region Public Navigation Methods

        public async Task NavigateToTruckLoadingAsync()
        {
            await NavigateToViewAsync("TruckLoading", "إدارة تحميل الشاحنات");
        }

        public async Task NavigateToPointOfSaleAsync()
        {
            await NavigateToViewAsync("PointOfSale", "نقطة البيع");
        }

        public async Task NavigateToCustomerManagementAsync()
        {
            await NavigateToViewAsync("CustomerManagement", "إدارة الزبائن");
        }

        public async Task NavigateToReportsAsync()
        {
            await NavigateToViewAsync("Reports", "التقارير والتحليلات");
        }

        public async Task NavigateToTransactionHistoryAsync()
        {
            await NavigateToViewAsync("TransactionHistory", "تاريخ المعاملات");
        }

        public async Task NavigateToReconciliationAsync()
        {
            await NavigateToViewAsync("Reconciliation", "التسوية اليومية");
        }

        public async Task NavigateToDashboardAsync()
        {
            await NavigateToViewAsync("Dashboard", "لوحة التحكم الرئيسية");
        }

        public async Task NavigateToSettingsAsync()
        {
            await NavigateToViewAsync("Settings", "إعدادات النظام");
        }

        public async Task NavigateBackAsync()
        {
            if (_navigationHistory.Count > 1)
            {
                try
                {
                    _logger.LogDebug("Navigating back from {CurrentView}", CurrentViewName);

                    // Remove current view from history
                    _navigationHistory.RemoveAt(_navigationHistory.Count - 1);

                    // Get previous view
                    var previousViewName = _navigationHistory[_navigationHistory.Count - 1];

                    // Navigate without adding to history
                    await NavigateToViewAsync(previousViewName, GetViewDisplayName(previousViewName), false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during back navigation from {CurrentView}", CurrentViewName);
                    throw;
                }
            }
            else
            {
                _logger.LogDebug("Cannot navigate back - no previous views in history");
            }
        }

        public void ClearNavigation()
        {
            try
            {
                _logger.LogDebug("Clearing navigation and returning to welcome screen");

                CurrentView = null;
                CurrentViewName = "Welcome";
                _navigationHistory.Clear();

                // Clear view cache to free memory
                foreach (var cachedView in _viewCache.Values)
                {
                    if (cachedView is IDisposable disposableView)
                    {
                        disposableView.Dispose();
                    }
                }
                _viewCache.Clear();

                _logger.LogInformation("Navigation cleared successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing navigation");
            }
        }

        public bool CanNavigateTo(string moduleName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(moduleName) || IsNavigating)
                    return false;

                // Check if view is registered
                if (!_viewTypeRegistry.ContainsKey(moduleName))
                {
                    _logger.LogWarning("Cannot navigate to unregistered module: {ModuleName}", moduleName);
                    return false;
                }

                // Add additional business logic here if needed
                // For example, check user permissions, system state, etc.

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking navigation capability for module {ModuleName}", moduleName);
                return false;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Initializes the view type registry with all available application views
        /// </summary>
        private void InitializeViewRegistry()
        {
            try
            {
                // Register all available views with their corresponding types
                _viewTypeRegistry["TruckLoading"] = typeof(TruckLoadingView);

                // TODO: Register additional views as they are implemented
                // _viewTypeRegistry["PointOfSale"] = typeof(PointOfSaleView);
                // _viewTypeRegistry["CustomerManagement"] = typeof(CustomerManagementView);
                // _viewTypeRegistry["Reports"] = typeof(ReportsView);
                // _viewTypeRegistry["TransactionHistory"] = typeof(TransactionHistoryView);
                // _viewTypeRegistry["Reconciliation"] = typeof(ReconciliationView);
                // _viewTypeRegistry["Dashboard"] = typeof(DashboardView);
                // _viewTypeRegistry["Settings"] = typeof(SettingsView);

                _logger.LogDebug("View registry initialized with {Count} view types", _viewTypeRegistry.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing view registry");
                throw;
            }
        }

        /// <summary>
        /// Core navigation method that handles view creation, caching, and lifecycle management
        /// </summary>
        private async Task NavigateToViewAsync(string viewName, string displayName, bool addToHistory = true)
        {
            if (!CanNavigateTo(viewName))
            {
                _logger.LogWarning("Navigation to {ViewName} blocked by CanNavigateTo check", viewName);
                return;
            }

            try
            {
                IsNavigating = true;
                _logger.LogInformation("Starting navigation to {ViewName} ({DisplayName})", viewName, displayName);

                // Get or create view instance
                var view = await GetOrCreateViewAsync(viewName);

                if (view == null)
                {
                    _logger.LogError("Failed to create view instance for {ViewName}", viewName);
                    return;
                }

                // Update navigation state
                CurrentView = view;
                CurrentViewName = viewName;

                // Manage navigation history
                if (addToHistory)
                {
                    // Remove duplicate entries from history
                    _navigationHistory.RemoveAll(h => h == viewName);
                    _navigationHistory.Add(viewName);

                    // Limit history size to prevent memory issues
                    if (_navigationHistory.Count > 50)
                    {
                        _navigationHistory.RemoveAt(0);
                    }
                }

                _logger.LogInformation("Successfully navigated to {ViewName}. History depth: {HistoryDepth}",
                    viewName, _navigationHistory.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error navigating to view {ViewName}", viewName);

                // Reset navigation state on error
                IsNavigating = false;
                throw;
            }
            finally
            {
                IsNavigating = false;
            }
        }

        /// <summary>
        /// Retrieves view from cache or creates new instance using dependency injection
        /// </summary>
        private async Task<UserControl?> GetOrCreateViewAsync(string viewName)
        {
            try
            {
                // Check cache first for performance optimization
                if (_viewCache.TryGetValue(viewName, out var cachedView))
                {
                    _logger.LogDebug("Retrieved {ViewName} from view cache", viewName);
                    return cachedView;
                }

                // Get view type from registry
                if (!_viewTypeRegistry.TryGetValue(viewName, out var viewType))
                {
                    _logger.LogError("View type not found in registry for {ViewName}", viewName);
                    return null;
                }

                // Create view instance using dependency injection
                var view = _serviceProvider.GetService(viewType) as UserControl;

                if (view == null)
                {
                    _logger.LogError("Failed to create view instance for type {ViewType}", viewType.Name);
                    return null;
                }

                // Cache the view for future use (singleton pattern per view type)
                _viewCache[viewName] = view;

                _logger.LogDebug("Created and cached new view instance for {ViewName}", viewName);

                // Simulate async initialization if needed
                await Task.Delay(1); // Placeholder for actual async initialization

                return view;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating view instance for {ViewName}", viewName);
                return null;
            }
        }

        /// <summary>
        /// Gets localized display name for view identifiers
        /// </summary>
        private string GetViewDisplayName(string viewName)
        {
            return viewName switch
            {
                "TruckLoading" => "إدارة تحميل الشاحنات",
                "PointOfSale" => "نقطة البيع",
                "CustomerManagement" => "إدارة الزبائن",
                "Reports" => "التقارير والتحليلات",
                "TransactionHistory" => "تاريخ المعاملات",
                "Reconciliation" => "التسوية اليومية",
                "Dashboard" => "لوحة التحكم الرئيسية",
                "Settings" => "إعدادات النظام",
                _ => viewName
            };
        }

        /// <summary>
        /// Raises PropertyChanged event for MVVM binding support
        /// </summary>
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Raises NavigationChanged event for UI synchronization
        /// </summary>
        private void OnNavigationChanged(string viewName, UserControl? view)
        {
            try
            {
                var args = new NavigationEventArgs(viewName, view, GetViewDisplayName(viewName));
                NavigationChanged?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising NavigationChanged event");
            }
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    // Dispose cached views
                    foreach (var view in _viewCache.Values)
                    {
                        if (view is IDisposable disposableView)
                        {
                            disposableView.Dispose();
                        }
                    }

                    _viewCache.Clear();
                    _navigationHistory.Clear();
                    _viewTypeRegistry.Clear();

                    _logger.LogDebug("NavigationService disposed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during NavigationService disposal");
                }
                finally
                {
                    _disposed = true;
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Enhanced navigation event arguments providing comprehensive context information
    /// </summary>
    public class NavigationEventArgs : EventArgs
    {
        public string ViewName { get; }
        public UserControl? View { get; }
        public string DisplayName { get; }
        public DateTime NavigationTime { get; }

        public NavigationEventArgs(string viewName, UserControl? view, string displayName)
        {
            ViewName = viewName ?? throw new ArgumentNullException(nameof(viewName));
            View = view;
            DisplayName = displayName ?? viewName;
            NavigationTime = DateTime.Now;
        }
    }
}