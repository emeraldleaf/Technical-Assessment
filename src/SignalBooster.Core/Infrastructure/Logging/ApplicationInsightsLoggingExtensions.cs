using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SignalBooster.Core.Infrastructure.Logging;

public static class ApplicationInsightsLoggingExtensions
{
    public static IServiceCollection AddApplicationInsightsLogging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var instrumentationKey = configuration["ApplicationInsights:InstrumentationKey"];
        var connectionString = configuration["ApplicationInsights:ConnectionString"];
        
        if (string.IsNullOrEmpty(instrumentationKey) && string.IsNullOrEmpty(connectionString))
        {
            // Fallback to structured file logging if no Application Insights configured
            return services.AddStructuredLogging(configuration);
        }

        // Configure Serilog for Application Insights with custom telemetry
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.WithProperty("Application", "SignalBooster")
            .Enrich.WithProperty("Version", GetAssemblyVersion())
            .Enrich.WithProperty("Environment", GetEnvironmentName())
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .Enrich.WithProperty("ProcessId", Environment.ProcessId)
            .Enrich.WithProperty("SessionId", Guid.NewGuid().ToString("N")[..8])
            .Enrich.WithThreadId()
            .WriteTo.ApplicationInsights(
                connectionString ?? $"InstrumentationKey={instrumentationKey}",
                new TraceTelemetryConverter())
            .WriteTo.Console(outputTemplate: 
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .CreateLogger();

        services.AddSerilog(Log.Logger);
        return services;
    }

    private static string GetAssemblyVersion()
    {
        return typeof(ApplicationInsightsLoggingExtensions).Assembly
            .GetName().Version?.ToString() ?? "1.0.0";
    }

    private static string GetEnvironmentName()
    {
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
               ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") 
               ?? "Production";
    }
}

/// <summary>
/// Extension methods for structured logging that's optimized for Application Insights queries
/// </summary>
public static class ApplicationInsightsLoggerExtensions
{
    /// <summary>
    /// Logs the start of a physician note processing operation with structured properties
    /// </summary>
    public static void LogPhysicianNoteProcessingStarted(
        this ILogger logger, 
        string filePath, 
        string operationId,
        string? correlationId = null)
    {
        logger.LogInformation("PhysicianNoteProcessing started for {FileName} | OperationId: {OperationId} | CorrelationId: {CorrelationId} | FilePath: {FilePath}",
            Path.GetFileName(filePath),
            operationId,
            correlationId ?? Guid.NewGuid().ToString(),
            filePath);
    }

    /// <summary>
    /// Logs successful completion of physician note processing with metrics
    /// </summary>
    public static void LogPhysicianNoteProcessingCompleted(
        this ILogger logger,
        string filePath,
        string deviceType,
        string orderId,
        string status,
        TimeSpan processingTime,
        string operationId,
        string? correlationId = null)
    {
        logger.LogInformation("PhysicianNoteProcessing completed successfully | " +
                            "FileName: {FileName} | " +
                            "DeviceType: {DeviceType} | " +
                            "OrderId: {OrderId} | " +
                            "Status: {Status} | " +
                            "ProcessingTimeMs: {ProcessingTimeMs} | " +
                            "OperationId: {OperationId} | " +
                            "CorrelationId: {CorrelationId} | " +
                            "FilePath: {FilePath} | " +
                            "EventName: ProcessingSuccess",
            Path.GetFileName(filePath),
            deviceType,
            orderId,
            status,
            processingTime.TotalMilliseconds,
            operationId,
            correlationId,
            filePath);
    }

    /// <summary>
    /// Logs physician note processing failure with error details
    /// </summary>
    public static void LogPhysicianNoteProcessingFailed(
        this ILogger logger,
        string filePath,
        string errorCode,
        string errorDescription,
        TimeSpan processingTime,
        string operationId,
        string? correlationId = null,
        Exception? exception = null)
    {
        logger.LogError(exception, "PhysicianNoteProcessing failed | " +
                                 "FileName: {FileName} | " +
                                 "ErrorCode: {ErrorCode} | " +
                                 "ErrorDescription: {ErrorDescription} | " +
                                 "ProcessingTimeMs: {ProcessingTimeMs} | " +
                                 "OperationId: {OperationId} | " +
                                 "CorrelationId: {CorrelationId} | " +
                                 "FilePath: {FilePath} | " +
                                 "EventName: ProcessingFailure",
            Path.GetFileName(filePath),
            errorCode,
            errorDescription,
            processingTime.TotalMilliseconds,
            operationId,
            correlationId,
            filePath);
    }

    /// <summary>
    /// Logs API call attempts with structured data for monitoring
    /// </summary>
    public static void LogApiCallAttempt(
        this ILogger logger,
        string endpoint,
        string deviceType,
        int attemptNumber,
        int maxAttempts,
        string operationId,
        string? correlationId = null)
    {
        logger.LogInformation("API call attempt | " +
                            "Endpoint: {Endpoint} | " +
                            "DeviceType: {DeviceType} | " +
                            "AttemptNumber: {AttemptNumber} | " +
                            "MaxAttempts: {MaxAttempts} | " +
                            "OperationId: {OperationId} | " +
                            "CorrelationId: {CorrelationId} | " +
                            "EventName: ApiCallAttempt",
            endpoint,
            deviceType,
            attemptNumber,
            maxAttempts,
            operationId,
            correlationId);
    }

    /// <summary>
    /// Logs successful API response with timing metrics
    /// </summary>
    public static void LogApiCallSuccess(
        this ILogger logger,
        string endpoint,
        string deviceType,
        string orderId,
        string responseStatus,
        TimeSpan responseTime,
        int attemptNumber,
        string operationId,
        string? correlationId = null)
    {
        logger.LogInformation("API call succeeded | " +
                            "Endpoint: {Endpoint} | " +
                            "DeviceType: {DeviceType} | " +
                            "OrderId: {OrderId} | " +
                            "ResponseStatus: {ResponseStatus} | " +
                            "ResponseTimeMs: {ResponseTimeMs} | " +
                            "AttemptNumber: {AttemptNumber} | " +
                            "OperationId: {OperationId} | " +
                            "CorrelationId: {CorrelationId} | " +
                            "EventName: ApiCallSuccess",
            endpoint,
            deviceType,
            orderId,
            responseStatus,
            responseTime.TotalMilliseconds,
            attemptNumber,
            operationId,
            correlationId);
    }

    /// <summary>
    /// Logs API call failures with detailed error information
    /// </summary>
    public static void LogApiCallFailure(
        this ILogger logger,
        string endpoint,
        string deviceType,
        string errorType,
        string errorMessage,
        TimeSpan responseTime,
        int attemptNumber,
        int maxAttempts,
        string operationId,
        string? correlationId = null,
        Exception? exception = null)
    {
        logger.LogError(exception, "API call failed | " +
                                 "Endpoint: {Endpoint} | " +
                                 "DeviceType: {DeviceType} | " +
                                 "ErrorType: {ErrorType} | " +
                                 "ErrorMessage: {ErrorMessage} | " +
                                 "ResponseTimeMs: {ResponseTimeMs} | " +
                                 "AttemptNumber: {AttemptNumber} | " +
                                 "MaxAttempts: {MaxAttempts} | " +
                                 "OperationId: {OperationId} | " +
                                 "CorrelationId: {CorrelationId} | " +
                                 "EventName: ApiCallFailure",
            endpoint,
            deviceType,
            errorType,
            errorMessage,
            responseTime.TotalMilliseconds,
            attemptNumber,
            maxAttempts,
            operationId,
            correlationId);
    }

    /// <summary>
    /// Logs device parsing results with extracted specifications
    /// </summary>
    public static void LogDeviceParsed(
        this ILogger logger,
        string deviceType,
        string patientName,
        string orderingProvider,
        IDictionary<string, object>? specifications,
        string operationId,
        string? correlationId = null)
    {
        logger.LogInformation("Device parsed from note | " +
                            "DeviceType: {DeviceType} | " +
                            "PatientName: {PatientName} | " +
                            "OrderingProvider: {OrderingProvider} | " +
                            "SpecificationCount: {SpecificationCount} | " +
                            "Specifications: {Specifications} | " +
                            "OperationId: {OperationId} | " +
                            "CorrelationId: {CorrelationId} | " +
                            "EventName: DeviceParsed",
            deviceType,
            patientName,
            orderingProvider,
            specifications?.Count ?? 0,
            specifications != null ? string.Join(", ", specifications.Keys) : "None",
            operationId,
            correlationId);
    }

    /// <summary>
    /// Logs validation failures with detailed field information
    /// </summary>
    public static void LogValidationFailure(
        this ILogger logger,
        string objectType,
        string[] failedFields,
        string[] errorMessages,
        string operationId,
        string? correlationId = null)
    {
        logger.LogWarning("Validation failed | " +
                        "ObjectType: {ObjectType} | " +
                        "FailedFieldCount: {FailedFieldCount} | " +
                        "FailedFields: {FailedFields} | " +
                        "ErrorMessages: {ErrorMessages} | " +
                        "OperationId: {OperationId} | " +
                        "CorrelationId: {CorrelationId} | " +
                        "EventName: ValidationFailure",
            objectType,
            failedFields.Length,
            string.Join(", ", failedFields),
            string.Join("; ", errorMessages),
            operationId,
            correlationId);
    }
}