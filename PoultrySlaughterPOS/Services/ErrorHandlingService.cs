using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient; // Fixed: Changed from System.Data.SqlClient
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace PoultrySlaughterPOS.Services
{
    /// <summary>
    /// Centralized error handling service providing comprehensive exception management,
    /// user-friendly messaging, and diagnostic logging for the POS system
    /// </summary>
    public interface IErrorHandlingService
    {
        Task<(bool Success, string UserMessage)> HandleExceptionAsync(Exception exception, string context = "");
        string GetUserFriendlyMessage(Exception exception);
        bool IsRetryableException(Exception exception);
        Task LogCriticalErrorAsync(Exception exception, string context, Dictionary<string, object>? additionalData = null);
    }

    public class ErrorHandlingService : IErrorHandlingService
    {
        private readonly ILogger<ErrorHandlingService> _logger;

        public ErrorHandlingService(ILogger<ErrorHandlingService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<(bool Success, string UserMessage)> HandleExceptionAsync(Exception exception, string context = "")
        {
            try
            {
                await LogExceptionDetailsAsync(exception, context).ConfigureAwait(false);

                var userMessage = GetUserFriendlyMessage(exception);
                var isRetryable = IsRetryableException(exception);

                return (false, userMessage);
            }
            catch (Exception loggingEx)
            {
                // Fallback logging to prevent infinite loops
                _logger.LogCritical(loggingEx, "Critical error in error handling service");
                return (false, "حدث خطأ غير متوقع في النظام. يرجى المحاولة لاحقاً.");
            }
        }

        public string GetUserFriendlyMessage(Exception exception)
        {
            return exception switch
            {
                SqlException sqlEx => GetSqlExceptionMessage(sqlEx),
                DbUpdateException dbEx => GetDbUpdateExceptionMessage(dbEx),
                TimeoutException => "انتهت مهلة الاتصال بقاعدة البيانات. يرجى المحاولة مرة أخرى.",
                UnauthorizedAccessException => "ليس لديك صلاحية للوصول إلى هذه العملية.",
                ArgumentException argEx => $"بيانات غير صحيحة: {argEx.ParamName}",
                InvalidOperationException => "العملية غير مسموحة في الوقت الحالي.",
                NotImplementedException => "هذه الميزة غير متوفرة حالياً.",
                _ => "حدث خطأ غير متوقع. يرجى التواصل مع الدعم الفني."
            };
        }

        public bool IsRetryableException(Exception exception)
        {
            return exception switch
            {
                SqlException sqlEx => IsRetryableSqlException(sqlEx),
                TimeoutException => true,
                DbUpdateConcurrencyException => true,
                TaskCanceledException => true,
                _ => false
            };
        }

        public async Task LogCriticalErrorAsync(Exception exception, string context, Dictionary<string, object>? additionalData = null)
        {
            try
            {
                var errorData = new Dictionary<string, object>
                {
                    ["Context"] = context,
                    ["ExceptionType"] = exception.GetType().Name,
                    ["Message"] = exception.Message,
                    ["StackTrace"] = exception.StackTrace ?? "No stack trace available",
                    ["InnerException"] = exception.InnerException?.Message ?? "None",
                    ["Timestamp"] = DateTime.Now,
                    ["MachineName"] = Environment.MachineName,
                    ["UserName"] = Environment.UserName
                };

                if (additionalData != null)
                {
                    foreach (var kvp in additionalData)
                    {
                        errorData[kvp.Key] = kvp.Value;
                    }
                }

                _logger.LogCritical(exception, "Critical error in context: {Context}. Data: {@ErrorData}", context, errorData);

                // In production, this could also send alerts, write to separate error database, etc.
                await WriteErrorToFileAsync(errorData).ConfigureAwait(false);
            }
            catch (Exception loggingEx)
            {
                _logger.LogError(loggingEx, "Failed to log critical error");
            }
        }

        private async Task LogExceptionDetailsAsync(Exception exception, string context)
        {
            var errorId = Guid.NewGuid().ToString("N")[..8];

            _logger.LogError(exception,
                "Error {ErrorId} in {Context}: {ExceptionType} - {Message}",
                errorId, context, exception.GetType().Name, exception.Message);

            if (exception.InnerException != null)
            {
                _logger.LogError("Inner exception for {ErrorId}: {InnerType} - {InnerMessage}",
                    errorId, exception.InnerException.GetType().Name, exception.InnerException.Message);
            }
        }

        private string GetSqlExceptionMessage(SqlException sqlEx)
        {
            return sqlEx.Number switch
            {
                2 => "لا يمكن الاتصال بخادم قاعدة البيانات. تأكد من تشغيل SQL Server.",
                18 => "فشل في تسجيل الدخول إلى قاعدة البيانات.",
                547 => "لا يمكن حذف هذا السجل لأنه مرتبط ببيانات أخرى.",
                2627 => "البيانات المدخلة موجودة بالفعل.",
                8152 => "البيانات المدخلة أطول من المسموح.",
                -2 => "انتهت مهلة العملية. يرجى المحاولة مرة أخرى.",
                1205 => "تعارض في قاعدة البيانات. يرجى المحاولة مرة أخرى.",
                _ => $"خطأ في قاعدة البيانات (رقم الخطأ: {sqlEx.Number}). يرجى التواصل مع الدعم الفني."
            };
        }

        private string GetDbUpdateExceptionMessage(DbUpdateException dbEx)
        {
            if (dbEx.InnerException is SqlException sqlEx)
            {
                return GetSqlExceptionMessage(sqlEx);
            }

            return dbEx switch
            {
                DbUpdateConcurrencyException => "تم تعديل البيانات من مستخدم آخر. يرجى تحديث الصفحة والمحاولة مرة أخرى.",
                _ => "فشل في حفظ التغييرات. يرجى المحاولة مرة أخرى."
            };
        }

        private bool IsRetryableSqlException(SqlException sqlEx)
        {
            // Retryable SQL Server error numbers
            var retryableErrors = new[] { -2, 1205, 1222, 8645, 8651 };
            return retryableErrors.Contains(sqlEx.Number);
        }

        private async Task WriteErrorToFileAsync(Dictionary<string, object> errorData)
        {
            try
            {
                var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "logs", "errors");
                Directory.CreateDirectory(logsDir);

                var fileName = $"error_{DateTime.Now:yyyyMMdd}.log";
                var filePath = Path.Combine(logsDir, fileName);

                var errorJson = System.Text.Json.JsonSerializer.Serialize(errorData, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.AppendAllTextAsync(filePath, $"{errorJson}\n---\n").ConfigureAwait(false);
            }
            catch
            {
                // Silently fail to prevent recursive errors
            }
        }
    }
}