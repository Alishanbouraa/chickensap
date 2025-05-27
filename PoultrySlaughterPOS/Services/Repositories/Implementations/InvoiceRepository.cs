using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Data;
using PoultrySlaughterPOS.Models;

namespace PoultrySlaughterPOS.Services.Repositories
{
    /// <summary>
    /// Enterprise-level invoice repository implementation providing comprehensive
    /// POS operations, financial calculations, and audit compliance for poultry slaughter operations
    /// </summary>
    public class InvoiceRepository : Repository<Invoice>, IInvoiceRepository
    {
        public InvoiceRepository(PoultryDbContext context, ILogger<InvoiceRepository> logger)
            : base(context, logger)
        {
        }

        public async Task<string> GenerateInvoiceNumberAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var today = DateTime.Today;
                var datePrefix = today.ToString("yyyyMMdd");

                var lastInvoiceNumber = await _dbSet
                    .Where(i => i.InvoiceNumber.StartsWith(datePrefix))
                    .OrderByDescending(i => i.InvoiceNumber)
                    .Select(i => i.InvoiceNumber)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                int sequence = 1;
                if (!string.IsNullOrEmpty(lastInvoiceNumber) && lastInvoiceNumber.Length > 8)
                {
                    var sequencePart = lastInvoiceNumber.Substring(8);
                    if (int.TryParse(sequencePart, out int lastSequence))
                    {
                        sequence = lastSequence + 1;
                    }
                }

                var invoiceNumber = $"{datePrefix}{sequence:D4}";
                _logger.LogDebug("Generated invoice number: {InvoiceNumber}", invoiceNumber);

                return invoiceNumber;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating invoice number");
                throw;
            }
        }

        public async Task<bool> IsInvoiceNumberUniqueAsync(string invoiceNumber, CancellationToken cancellationToken = default)
        {
            try
            {
                return !await _dbSet
                    .AnyAsync(i => i.InvoiceNumber == invoiceNumber, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking invoice number uniqueness: {InvoiceNumber}", invoiceNumber);
                throw;
            }
        }

        public async Task<Invoice?> GetInvoiceByNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Include(i => i.Customer)
                    .Include(i => i.Truck)
                    .Include(i => i.Payments)
                    .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoice by number: {InvoiceNumber}", invoiceNumber);
                throw;
            }
        }

        public async Task<Invoice?> GetInvoiceWithDetailsAsync(int invoiceId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Include(i => i.Customer)
                    .Include(i => i.Truck)
                    .Include(i => i.Payments)
                    .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoice with details: {InvoiceId}", invoiceId);
                throw;
            }
        }

        public async Task<IEnumerable<Invoice>> GetInvoicesWithCustomerAndTruckAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Include(i => i.Customer)
                    .Include(i => i.Truck)
                    .Where(i => i.InvoiceDate >= startDate && i.InvoiceDate <= endDate)
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices with customer and truck between {StartDate} and {EndDate}",
                    startDate, endDate);
                throw;
            }
        }

        public async Task<IEnumerable<Invoice>> GetCustomerInvoicesWithPaymentsAsync(int customerId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Include(i => i.Payments)
                    .Include(i => i.Truck)
                    .Where(i => i.CustomerId == customerId)
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer invoices with payments: {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<decimal> CalculateNetWeightAsync(decimal grossWeight, decimal cagesWeight)
        {
            return await Task.FromResult(Math.Max(0, grossWeight - cagesWeight));
        }

        public async Task<decimal> CalculateTotalAmountAsync(decimal netWeight, decimal unitPrice)
        {
            return await Task.FromResult(netWeight * unitPrice);
        }

        public async Task<decimal> ApplyDiscountCalculationAsync(decimal totalAmount, decimal discountPercentage)
        {
            if (discountPercentage < 0 || discountPercentage > 100)
                throw new ArgumentOutOfRangeException(nameof(discountPercentage), "Discount percentage must be between 0 and 100");

            return await Task.FromResult(totalAmount * (discountPercentage / 100));
        }

        public async Task<decimal> CalculateFinalAmountAsync(decimal totalAmount, decimal discountPercentage)
        {
            var discountAmount = await ApplyDiscountCalculationAsync(totalAmount, discountPercentage);
            return totalAmount - discountAmount;
        }

        public async Task<Invoice> CreateInvoiceWithBalanceUpdateAsync(Invoice invoice, CancellationToken cancellationToken = default)
        {
            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    // Get current customer balance
                    var customer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.CustomerId == invoice.CustomerId, cancellationToken)
                        .ConfigureAwait(false);

                    if (customer == null)
                        throw new InvalidOperationException($"Customer with ID {invoice.CustomerId} not found");

                    // Set previous balance and calculate new balance
                    invoice.PreviousBalance = customer.TotalDebt;
                    invoice.CurrentBalance = customer.TotalDebt + invoice.FinalAmount;

                    // Add invoice
                    var addedInvoice = await AddAsync(invoice, cancellationToken).ConfigureAwait(false);

                    // Update customer balance
                    customer.TotalDebt = invoice.CurrentBalance;
                    customer.UpdatedDate = DateTime.Now;

                    await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation("Created invoice {InvoiceNumber} for customer {CustomerId} with amount {Amount}. Balance updated from {PreviousBalance} to {CurrentBalance}",
                        invoice.InvoiceNumber, invoice.CustomerId, invoice.FinalAmount, invoice.PreviousBalance, invoice.CurrentBalance);

                    return addedInvoice;
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invoice with balance update");
                throw;
            }
        }

        public async Task<bool> UpdateInvoiceWithCustomerBalanceAsync(int invoiceId, decimal newAmount, CancellationToken cancellationToken = default)
        {
            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    var invoice = await GetInvoiceWithDetailsAsync(invoiceId, cancellationToken).ConfigureAwait(false);
                    if (invoice == null)
                        return false;

                    var originalAmount = invoice.FinalAmount;
                    var amountDifference = newAmount - originalAmount;

                    // Update invoice
                    invoice.FinalAmount = newAmount;
                    invoice.CurrentBalance = invoice.PreviousBalance + newAmount;
                    invoice.UpdatedDate = DateTime.Now;

                    // Update customer balance
                    var customer = invoice.Customer;
                    customer.TotalDebt += amountDifference;
                    customer.UpdatedDate = DateTime.Now;

                    await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation("Updated invoice {InvoiceId} amount from {OriginalAmount} to {NewAmount}. Customer balance adjusted by {Difference}",
                        invoiceId, originalAmount, newAmount, amountDifference);

                    return true;
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating invoice with customer balance: {InvoiceId}", invoiceId);
                throw;
            }
        }

        public async Task<bool> VoidInvoiceWithBalanceReversalAsync(int invoiceId, string voidReason, CancellationToken cancellationToken = default)
        {
            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    var invoice = await GetInvoiceWithDetailsAsync(invoiceId, cancellationToken).ConfigureAwait(false);
                    if (invoice == null)
                        return false;

                    // Reverse customer balance
                    var customer = invoice.Customer;
                    customer.TotalDebt -= invoice.FinalAmount;
                    customer.UpdatedDate = DateTime.Now;

                    // Mark invoice as voided (you might want to add a Status field)
                    // For now, we'll set amounts to zero and add a note
                    invoice.FinalAmount = 0;
                    invoice.CurrentBalance = invoice.PreviousBalance;
                    invoice.UpdatedDate = DateTime.Now;

                    await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                    _logger.LogWarning("Voided invoice {InvoiceId} for reason: {VoidReason}. Customer balance reversed.",
                        invoiceId, voidReason);

                    return true;
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error voiding invoice with balance reversal: {InvoiceId}", invoiceId);
                throw;
            }
        }

        public async Task<IEnumerable<Invoice>> GetDailyInvoicesAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                var targetDate = date.Date;
                return await _dbSet
                    .Include(i => i.Customer)
                    .Include(i => i.Truck)
                    .Where(i => i.InvoiceDate.Date == targetDate)
                    .OrderBy(i => i.InvoiceNumber)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving daily invoices for date: {Date}", date);
                throw;
            }
        }

        public async Task<IEnumerable<Invoice>> GetInvoicesByTruckAndDateAsync(int truckId, DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                var targetDate = date.Date;
                return await _dbSet
                    .Include(i => i.Customer)
                    .Where(i => i.TruckId == truckId && i.InvoiceDate.Date == targetDate)
                    .OrderBy(i => i.InvoiceDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices for truck {TruckId} on date {Date}", truckId, date);
                throw;
            }
        }

        public async Task<decimal> GetDailySalesTotalByTruckAsync(int truckId, DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                var targetDate = date.Date;
                return await _dbSet
                    .Where(i => i.TruckId == truckId && i.InvoiceDate.Date == targetDate)
                    .SumAsync(i => i.FinalAmount, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving daily sales total for truck {TruckId} on date {Date}", truckId, date);
                throw;
            }
        }

        public async Task<decimal> GetDailyNetWeightByTruckAsync(int truckId, DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                var targetDate = date.Date;
                return await _dbSet
                    .Where(i => i.TruckId == truckId && i.InvoiceDate.Date == targetDate)
                    .SumAsync(i => i.NetWeight, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving daily net weight for truck {TruckId} on date {Date}", truckId, date);
                throw;
            }
        }

        public async Task<Dictionary<int, decimal>> GetTruckSalesPerformanceAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Where(i => i.InvoiceDate >= startDate && i.InvoiceDate <= endDate)
                    .GroupBy(i => i.TruckId)
                    .ToDictionaryAsync(g => g.Key, g => g.Sum(i => i.FinalAmount), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving truck sales performance between {StartDate} and {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<Dictionary<int, decimal>> GetCustomerPurchaseVolumeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Where(i => i.InvoiceDate >= startDate && i.InvoiceDate <= endDate)
                    .GroupBy(i => i.CustomerId)
                    .ToDictionaryAsync(g => g.Key, g => g.Sum(i => i.FinalAmount), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer purchase volume between {StartDate} and {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<(decimal TotalSales, decimal TotalWeight, int InvoiceCount)> GetSalesSummaryAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var invoices = await _dbSet
                    .Where(i => i.InvoiceDate >= startDate && i.InvoiceDate <= endDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return (
                    invoices.Sum(i => i.FinalAmount),
                    invoices.Sum(i => i.NetWeight),
                    invoices.Count
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sales summary between {StartDate} and {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<IEnumerable<Invoice>> SearchInvoicesAsync(string searchTerm, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                    return await GetInvoicesWithCustomerAndTruckAsync(startDate ?? DateTime.Today.AddMonths(-1), endDate ?? DateTime.Today, cancellationToken);

                var term = searchTerm.Trim().ToLower();
                var query = _dbSet
                    .Include(i => i.Customer)
                    .Include(i => i.Truck)
                    .Where(i => i.InvoiceNumber.ToLower().Contains(term) ||
                               i.Customer.CustomerName.ToLower().Contains(term) ||
                               i.Truck.TruckNumber.ToLower().Contains(term));

                if (startDate.HasValue)
                    query = query.Where(i => i.InvoiceDate >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(i => i.InvoiceDate <= endDate.Value);

                return await query
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching invoices with term: {SearchTerm}", searchTerm);
                throw;
            }
        }

        public async Task<IEnumerable<Invoice>> GetInvoicesByAmountRangeAsync(decimal minAmount, decimal maxAmount, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Include(i => i.Customer)
                    .Include(i => i.Truck)
                    .Where(i => i.FinalAmount >= minAmount && i.FinalAmount <= maxAmount)
                    .OrderByDescending(i => i.FinalAmount)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices by amount range: {MinAmount} - {MaxAmount}", minAmount, maxAmount);
                throw;
            }
        }

        public async Task<IEnumerable<Invoice>> GetLargeInvoicesAsync(decimal thresholdAmount, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Include(i => i.Customer)
                    .Include(i => i.Truck)
                    .Where(i => i.FinalAmount >= thresholdAmount)
                    .OrderByDescending(i => i.FinalAmount)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving large invoices with threshold: {ThresholdAmount}", thresholdAmount);
                throw;
            }
        }

        public async Task<bool> ValidateInvoiceIntegrityAsync(int invoiceId, CancellationToken cancellationToken = default)
        {
            try
            {
                var invoice = await GetByIdAsync(invoiceId, cancellationToken).ConfigureAwait(false);
                if (invoice == null)
                    return false;

                // Validate calculations
                var expectedNetWeight = await CalculateNetWeightAsync(invoice.GrossWeight, invoice.CagesWeight);
                var expectedTotalAmount = await CalculateTotalAmountAsync(expectedNetWeight, invoice.UnitPrice);
                var expectedFinalAmount = await CalculateFinalAmountAsync(expectedTotalAmount, invoice.DiscountPercentage);

                return Math.Abs(invoice.NetWeight - expectedNetWeight) < 0.01m &&
                       Math.Abs(invoice.TotalAmount - expectedTotalAmount) < 0.01m &&
                       Math.Abs(invoice.FinalAmount - expectedFinalAmount) < 0.01m;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating invoice integrity: {InvoiceId}", invoiceId);
                throw;
            }
        }

        public async Task<IEnumerable<Invoice>> GetInvoicesWithDiscrepanciesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var invoices = await GetAllAsync(cancellationToken).ConfigureAwait(false);
                var discrepancies = new List<Invoice>();

                foreach (var invoice in invoices)
                {
                    if (!await ValidateInvoiceIntegrityAsync(invoice.InvoiceId, cancellationToken))
                    {
                        discrepancies.Add(invoice);
                    }
                }

                return discrepancies;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices with discrepancies");
                throw;
            }
        }

        public async Task<bool> RecalculateInvoiceTotalsAsync(int invoiceId, CancellationToken cancellationToken = default)
        {
            try
            {
                var invoice = await GetByIdAsync(invoiceId, cancellationToken).ConfigureAwait(false);
                if (invoice == null)
                    return false;

                invoice.NetWeight = await CalculateNetWeightAsync(invoice.GrossWeight, invoice.CagesWeight);
                invoice.TotalAmount = await CalculateTotalAmountAsync(invoice.NetWeight, invoice.UnitPrice);
                invoice.FinalAmount = await CalculateFinalAmountAsync(invoice.TotalAmount, invoice.DiscountPercentage);
                invoice.UpdatedDate = DateTime.Now;

                _logger.LogDebug("Recalculated totals for invoice {InvoiceId}", invoiceId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recalculating invoice totals: {InvoiceId}", invoiceId);
                throw;
            }
        }

        public async Task<IEnumerable<Invoice>> GetModifiedInvoicesAsync(DateTime since, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .Include(i => i.Customer)
                    .Include(i => i.Truck)
                    .Where(i => i.UpdatedDate >= since)
                    .OrderByDescending(i => i.UpdatedDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving modified invoices since: {Since}", since);
                throw;
            }
        }

        public async Task<Dictionary<string, object>> GetInvoiceAuditTrailAsync(int invoiceId, CancellationToken cancellationToken = default)
        {
            try
            {
                var auditLogs = await _context.AuditLogs
                    .Where(a => a.TableName == "Invoice" && a.NewValues!.Contains($"\"InvoiceId\":{invoiceId}"))
                    .OrderBy(a => a.CreatedDate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return new Dictionary<string, object>
                {
                    ["InvoiceId"] = invoiceId,
                    ["AuditEntries"] = auditLogs,
                    ["ModificationCount"] = auditLogs.Count,
                    ["LastModified"] = auditLogs.LastOrDefault()?.CreatedDate ?? DateTime.MinValue
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit trail for invoice: {InvoiceId}", invoiceId);
                throw;
            }
        }

        public async Task<IEnumerable<Invoice>> CreateInvoiceBatchAsync(IEnumerable<Invoice> invoices, CancellationToken cancellationToken = default)
        {
            try
            {
                var invoiceList = invoices.ToList();
                await AddRangeAsync(invoiceList, cancellationToken).ConfigureAwait(false);

                _logger.LogDebug("Created batch of {Count} invoices", invoiceList.Count);
                return invoiceList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invoice batch");
                throw;
            }
        }

        public async Task<int> UpdateInvoiceStatusBatchAsync(IEnumerable<int> invoiceIds, string status, CancellationToken cancellationToken = default)
        {
            try
            {
                var invoices = await _dbSet
                    .Where(i => invoiceIds.Contains(i.InvoiceId))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                foreach (var invoice in invoices)
                {
                    // Note: You might want to add a Status field to the Invoice model
                    invoice.UpdatedDate = DateTime.Now;
                }

                _logger.LogDebug("Updated status for {Count} invoices to {Status}", invoices.Count, status);
                return invoices.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating invoice status batch");
                throw;
            }
        }
    }
}