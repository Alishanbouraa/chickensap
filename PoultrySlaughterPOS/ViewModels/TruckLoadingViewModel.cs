using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Models;
using PoultrySlaughterPOS.Services;
using PoultrySlaughterPOS.Services.Repositories;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace PoultrySlaughterPOS.ViewModels
{
    /// <summary>
    /// Advanced truck loading view model implementing comprehensive load management operations
    /// with real-time validation, business rule enforcement, and optimistic concurrency handling
    /// </summary>
    public partial class TruckLoadingViewModel : ObservableValidator
    {
        #region Private Fields

        private readonly IUnitOfWork _unitOfWork;
        private readonly IErrorHandlingService _errorHandling;
        private readonly ILogger<TruckLoadingViewModel> _logger;

        #endregion

        #region Observable Properties

        [ObservableProperty]
        private ObservableCollection<Truck> _availableTrucks = new();

        [ObservableProperty]
        private ObservableCollection<TruckLoad> _todaysLoads = new();

        [ObservableProperty]
        private ObservableCollection<string> _validationErrors = new();

        [ObservableProperty]
        private Truck? _selectedTruck;

        [ObservableProperty]
        [Range(0.01, 10000.00, ErrorMessage = "الوزن الإجمالي يجب أن يكون بين 0.01 و 10000 كيلوغرام")]
        private decimal _totalWeight;

        [ObservableProperty]
        [Range(1, 500, ErrorMessage = "عدد الأقفاص يجب أن يكون بين 1 و 500")]
        private int _cagesCount = 1;

        [ObservableProperty]
        [StringLength(500, ErrorMessage = "الملاحظات لا يمكن أن تتجاوز 500 حرف")]
        private string _notes = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isValidationEnabled = true;

        [ObservableProperty]
        private string _loadStatus = "LOADED";

        [ObservableProperty]
        private decimal _averageWeightPerCage;

        [ObservableProperty]
        private decimal _totalLoadedWeight;

        [ObservableProperty]
        private int _totalLoadedTrucks;

        [ObservableProperty]
        private string _statusMessage = "جاري تحميل البيانات...";

        [ObservableProperty]
        private bool _canCreateLoadInternal = true;

        #endregion

        #region Constructor

        public TruckLoadingViewModel(
            IUnitOfWork unitOfWork,
            IErrorHandlingService errorHandling,
            ILogger<TruckLoadingViewModel> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _errorHandling = errorHandling ?? throw new ArgumentNullException(nameof(errorHandling));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize commands
            CreateLoadCommand = new AsyncRelayCommand(CreateLoadAsync, CanCreateLoadMethod);
            RefreshDataCommand = new AsyncRelayCommand(RefreshDataAsync);
            ValidateInputCommand = new RelayCommand(ValidateInput);
            ClearFormCommand = new RelayCommand(ClearForm);
            DeleteLoadCommand = new AsyncRelayCommand<TruckLoad>(DeleteLoadAsync);

            // Subscribe to property changes for real-time validation
            PropertyChanged += OnPropertyChangedHandler;

            // Initialize data
            _ = InitializeAsync();
        }

        #endregion

        #region Commands

        public IAsyncRelayCommand CreateLoadCommand { get; }
        public IAsyncRelayCommand RefreshDataCommand { get; }
        public IRelayCommand ValidateInputCommand { get; }
        public IRelayCommand ClearFormCommand { get; }
        public IAsyncRelayCommand<TruckLoad> DeleteLoadCommand { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes view model data and loads initial truck information
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "جاري تحميل بيانات الشاحنات...";

                await LoadAvailableTrucksAsync();
                await LoadTodaysLoadsAsync();
                await CalculateDailySummaryAsync();

                StatusMessage = "تم تحميل البيانات بنجاح";
                _logger.LogInformation("Truck loading view model initialized successfully");
            }
            catch (Exception ex)
            {
                var (_, userMessage) = await _errorHandling.HandleExceptionAsync(ex, "TruckLoadingViewModel.Initialize");
                StatusMessage = $"خطأ في تحميل البيانات: {userMessage}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Validates current input against business rules and data constraints
        /// </summary>
        public void ValidateInput()
        {
            ValidationErrors.Clear();

            try
            {
                // Validate truck selection
                if (SelectedTruck == null)
                {
                    ValidationErrors.Add("يجب اختيار شاحنة");
                }
                else if (!SelectedTruck.IsActive)
                {
                    ValidationErrors.Add("الشاحنة المختارة غير نشطة");
                }

                // Validate weight input
                if (TotalWeight <= 0)
                {
                    ValidationErrors.Add("يجب إدخال وزن إجمالي صحيح");
                }
                else if (TotalWeight > 10000)
                {
                    ValidationErrors.Add("الوزن الإجمالي لا يمكن أن يتجاوز 10000 كيلوغرام");
                }

                // Validate cages count
                if (CagesCount <= 0)
                {
                    ValidationErrors.Add("يجب إدخال عدد أقفاص صحيح");
                }
                else if (CagesCount > 500)
                {
                    ValidationErrors.Add("عدد الأقفاص لا يمكن أن يتجاوز 500");
                }

                // Business rule validation
                if (TotalWeight > 0 && CagesCount > 0)
                {
                    AverageWeightPerCage = TotalWeight / CagesCount;

                    if (AverageWeightPerCage < 1.0m)
                    {
                        ValidationErrors.Add("متوسط وزن القفص منخفض جداً (أقل من 1 كيلوغرام)");
                    }
                    else if (AverageWeightPerCage > 50.0m)
                    {
                        ValidationErrors.Add("متوسط وزن القفص مرتفع جداً (أكثر من 50 كيلوغرام)");
                    }
                }

                // Update command state
                CanCreateLoadInternal = ValidationErrors.Count == 0 && SelectedTruck != null && !IsLoading;
                CreateLoadCommand.NotifyCanExecuteChanged();

                _logger.LogDebug("Input validation completed. Errors: {ErrorCount}", ValidationErrors.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during input validation");
                ValidationErrors.Add("خطأ في التحقق من صحة البيانات");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Loads available trucks that can be loaded today
        /// </summary>
        private async Task LoadAvailableTrucksAsync()
        {
            try
            {
                var trucks = await _unitOfWork.Trucks.GetTrucksForLoadingAsync();

                AvailableTrucks.Clear();
                foreach (var truck in trucks.OrderBy(t => t.TruckNumber))
                {
                    AvailableTrucks.Add(truck);
                }

                _logger.LogInformation("Loaded {TruckCount} available trucks for loading", AvailableTrucks.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading available trucks");
                throw;
            }
        }

        /// <summary>
        /// Loads today's truck loading records
        /// </summary>
        private async Task LoadTodaysLoadsAsync()
        {
            try
            {
                var loads = await _unitOfWork.TruckLoads.GetTruckLoadsByDateAsync(DateTime.Today);

                TodaysLoads.Clear();
                foreach (var load in loads.OrderByDescending(l => l.CreatedDate))
                {
                    TodaysLoads.Add(load);
                }

                _logger.LogInformation("Loaded {LoadCount} truck loads for today", TodaysLoads.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading today's truck loads");
                throw;
            }
        }

        /// <summary>
        /// Calculates daily loading summary statistics
        /// </summary>
        private async Task CalculateDailySummaryAsync()
        {
            try
            {
                TotalLoadedWeight = await _unitOfWork.TruckLoads.GetTotalLoadWeightByDateAsync(DateTime.Today);
                TotalLoadedTrucks = TodaysLoads.Count;

                _logger.LogDebug("Daily summary calculated - Weight: {Weight}, Trucks: {Trucks}",
                    TotalLoadedWeight, TotalLoadedTrucks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating daily summary");
                TotalLoadedWeight = 0;
                TotalLoadedTrucks = 0;
            }
        }

        /// <summary>
        /// Creates a new truck load with comprehensive validation and transaction handling
        /// </summary>
        private async Task CreateLoadAsync()
        {
            if (!CanCreateLoadMethod()) return;

            try
            {
                IsLoading = true;
                StatusMessage = "جاري حفظ بيانات التحميل...";

                // Final validation
                ValidateInput();
                if (ValidationErrors.Any())
                {
                    StatusMessage = "يرجى تصحيح الأخطاء المعروضة";
                    return;
                }

                // Create new truck load
                var newLoad = new TruckLoad
                {
                    TruckId = SelectedTruck!.TruckId,
                    LoadDate = DateTime.Now,
                    TotalWeight = TotalWeight,
                    CagesCount = CagesCount,
                    Notes = Notes,
                    Status = LoadStatus,
                    CreatedDate = DateTime.Now,
                    UpdatedDate = DateTime.Now
                };

                // Use transaction for data consistency
                await _unitOfWork.BeginTransactionAsync();

                try
                {
                    // Save the load
                    var savedLoad = await _unitOfWork.TruckLoads.AddAsync(newLoad);
                    await _unitOfWork.SaveChangesAsync("TRUCK_LOADING");

                    // Commit transaction
                    await _unitOfWork.CommitTransactionAsync();

                    // Update UI
                    TodaysLoads.Insert(0, savedLoad);
                    await CalculateDailySummaryAsync();

                    // Clear form
                    ClearForm();

                    StatusMessage = $"تم حفظ بيانات تحميل الشاحنة {SelectedTruck.TruckNumber} بنجاح";
                    _logger.LogInformation("Successfully created truck load for truck {TruckNumber} with weight {Weight}",
                        SelectedTruck.TruckNumber, TotalWeight);

                    // Refresh available trucks (remove the loaded one)
                    await LoadAvailableTrucksAsync();
                }
                catch (Exception)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                var (_, userMessage) = await _errorHandling.HandleExceptionAsync(ex, "CreateTruckLoad");
                StatusMessage = $"خطأ في حفظ البيانات: {userMessage}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Deletes a truck load with confirmation and cascade handling
        /// </summary>
        private async Task DeleteLoadAsync(TruckLoad? load)
        {
            if (load == null) return;

            try
            {
                IsLoading = true;
                StatusMessage = "جاري حذف بيانات التحميل...";

                // Check if load can be deleted (no related invoices)
                var hasInvoices = await _unitOfWork.Invoices.ExistsAsync(i => i.TruckId == load.TruckId &&
                    i.InvoiceDate.Date == load.LoadDate.Date);

                if (hasInvoices)
                {
                    StatusMessage = "لا يمكن حذف هذا التحميل لوجود فواتير مرتبطة به";
                    return;
                }

                await _unitOfWork.BeginTransactionAsync();

                try
                {
                    await _unitOfWork.TruckLoads.DeleteAsync(load.LoadId);
                    await _unitOfWork.SaveChangesAsync("TRUCK_LOADING_DELETE");
                    await _unitOfWork.CommitTransactionAsync();

                    // Update UI
                    TodaysLoads.Remove(load);
                    await CalculateDailySummaryAsync();
                    await LoadAvailableTrucksAsync();

                    StatusMessage = "تم حذف بيانات التحميل بنجاح";
                    _logger.LogInformation("Successfully deleted truck load {LoadId}", load.LoadId);
                }
                catch (Exception)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                var (_, userMessage) = await _errorHandling.HandleExceptionAsync(ex, "DeleteTruckLoad");
                StatusMessage = $"خطأ في حذف البيانات: {userMessage}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Refreshes all data from the database
        /// </summary>
        private async Task RefreshDataAsync()
        {
            await InitializeAsync();
        }

        /// <summary>
        /// Clears the form inputs and resets validation state
        /// </summary>
        private void ClearForm()
        {
            SelectedTruck = null;
            TotalWeight = 0;
            CagesCount = 1;
            Notes = string.Empty;
            LoadStatus = "LOADED";
            AverageWeightPerCage = 0;
            ValidationErrors.Clear();
            CanCreateLoadInternal = true;
        }

        /// <summary>
        /// Determines if a new load can be created based on current state
        /// </summary>
        private bool CanCreateLoadMethod()
        {
            return !IsLoading &&
                   SelectedTruck != null &&
                   TotalWeight > 0 &&
                   CagesCount > 0 &&
                   ValidationErrors.Count == 0;
        }

        /// <summary>
        /// Handles property change events for real-time validation
        /// </summary>
        private void OnPropertyChangedHandler(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!IsValidationEnabled) return;

            switch (e.PropertyName)
            {
                case nameof(SelectedTruck):
                case nameof(TotalWeight):
                case nameof(CagesCount):
                case nameof(Notes):
                    ValidateInput();
                    break;
            }
        }

        #endregion
    }
}