using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace PoultrySlaughterPOS.Views
{
    /// <summary>
    /// Advanced truck loading interface providing comprehensive load management capabilities
    /// with real-time validation, business intelligence, and optimized data entry workflows
    /// </summary>
    public partial class TruckLoadingView : UserControl
    {
        #region Private Fields

        private readonly ILogger<TruckLoadingView> _logger;
        private readonly TruckLoadingViewModel _viewModel;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes the truck loading view with dependency injection support
        /// </summary>
        public TruckLoadingView()
        {
            InitializeComponent();

            // Obtain services from the application's service provider
            var serviceProvider = ((App)Application.Current).Services;
            _logger = serviceProvider.GetRequiredService<ILogger<TruckLoadingView>>();
            _viewModel = serviceProvider.GetRequiredService<TruckLoadingViewModel>();

            DataContext = _viewModel;

            _logger.LogInformation("TruckLoadingView initialized successfully");

            // Subscribe to view lifecycle events
            Loaded += TruckLoadingView_Loaded;
            Unloaded += TruckLoadingView_Unloaded;
        }

        /// <summary>
        /// Constructor with explicit dependency injection for unit testing
        /// </summary>
        /// <param name="viewModel">Pre-configured view model instance</param>
        /// <param name="logger">Logging service instance</param>
        public TruckLoadingView(TruckLoadingViewModel viewModel, ILogger<TruckLoadingView> logger)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            DataContext = _viewModel;

            Loaded += TruckLoadingView_Loaded;
            Unloaded += TruckLoadingView_Unloaded;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles view loaded event and initializes data
        /// </summary>
        private async void TruckLoadingView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogDebug("TruckLoadingView loaded, initializing data...");

                // Initialize view model data
                await _viewModel.InitializeAsync();

                // Set focus to first input control for optimal user experience
                SetInitialFocus();

                _logger.LogInformation("TruckLoadingView data initialization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during TruckLoadingView load initialization");

                // Show user-friendly error message
                MessageBox.Show(
                    "حدث خطأ أثناء تحميل بيانات الشاحنات. يرجى المحاولة مرة أخرى.",
                    "خطأ في التحميل",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Handles view unloaded event and performs cleanup
        /// </summary>
        private void TruckLoadingView_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogDebug("TruckLoadingView unloaded, performing cleanup...");

                // Dispose of view model resources if needed
                if (_viewModel is IDisposable disposableViewModel)
                {
                    disposableViewModel.Dispose();
                }

                _logger.LogDebug("TruckLoadingView cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during TruckLoadingView cleanup");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Sets initial focus to the first input control for optimal user workflow
        /// </summary>
        private void SetInitialFocus()
        {
            try
            {
                // Find the truck selection ComboBox and set focus
                var truckComboBox = FindName("TruckSelectionComboBox") as ComboBox;
                if (truckComboBox != null && truckComboBox.IsEnabled)
                {
                    truckComboBox.Focus();
                    _logger.LogDebug("Initial focus set to truck selection control");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set initial focus");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Refreshes the view data programmatically
        /// </summary>
        public async Task RefreshAsync()
        {
            try
            {
                _logger.LogDebug("Programmatic refresh requested for TruckLoadingView");
                await _viewModel.RefreshDataCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during programmatic refresh");
                throw;
            }
        }

        /// <summary>
        /// Validates current input state and returns validation results
        /// </summary>
        /// <returns>True if all inputs are valid, false otherwise</returns>
        public bool ValidateInput()
        {
            try
            {
                _viewModel.ValidateInput();
                var isValid = _viewModel.ValidationErrors.Count == 0;

                _logger.LogDebug("Input validation completed. Valid: {IsValid}, Errors: {ErrorCount}",
                    isValid, _viewModel.ValidationErrors.Count);

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during input validation");
                return false;
            }
        }

        /// <summary>
        /// Clears all form inputs and resets the view to initial state
        /// </summary>
        public void ClearForm()
        {
            try
            {
                _logger.LogDebug("Clearing form inputs");
                _viewModel.ClearFormCommand.Execute(null);
                SetInitialFocus();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during form clearing");
            }
        }

        #endregion
    }
}