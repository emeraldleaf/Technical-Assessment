using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using SignalBooster.Core.Configuration;

namespace SignalBooster.Core.Infrastructure.Logging;

public static class LoggingExtensions
{
    public static IServiceCollection AddStructuredLogging(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Configure Serilog from configuration
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.WithProperty("Application", "SignalBooster")
            .Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .Enrich.WithProperty("ProcessId", Environment.ProcessId)
            .Enrich.WithThreadId()
            .CreateLogger();

        // Add Serilog to the service collection
        services.AddSerilog(Log.Logger);

        return services;
    }

    public static IServiceCollection AddFileLogging(
        this IServiceCollection services,
        LoggingOptions options)
    {
        if (!options.EnableFileLogging)
            return services;

        // Ensure logs directory exists
        var logDirectory = Path.GetDirectoryName(options.LogOutputPath);
        if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(ParseLogLevel(options.LogLevel))
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.WithProperty("Application", "SignalBooster")
            .Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .Enrich.WithProperty("ProcessId", Environment.ProcessId)
            .Enrich.WithThreadId()
            .WriteTo.Console(
                formatter: options.EnableStructuredLogging 
                    ? new CompactJsonFormatter() 
                    : null)
            .WriteTo.File(
                path: options.LogOutputPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: options.RetainedFileCountLimit,
                formatter: options.EnableStructuredLogging 
                    ? new CompactJsonFormatter() 
                    : null)
            .CreateLogger();

        services.AddSerilog(Log.Logger);
        
        return services;
    }

    public static IHostBuilder UseSerilogLogging(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseSerilog();
    }

    private static LogEventLevel ParseLogLevel(string logLevel)
    {
        return logLevel?.ToUpperInvariant() switch
        {
            "VERBOSE" => LogEventLevel.Verbose,
            "DEBUG" => LogEventLevel.Debug,
            "INFORMATION" => LogEventLevel.Information,
            "WARNING" => LogEventLevel.Warning,
            "ERROR" => LogEventLevel.Error,
            "FATAL" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
}

public static class LoggerExtensions
{
    public static IDisposable BeginScopeWithCorrelationId(this Microsoft.Extensions.Logging.ILogger logger, string? correlationId = null)
    {
        correlationId ??= Guid.NewGuid().ToString();
        return logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });
    }

    public static void LogProcessingStart(this Microsoft.Extensions.Logging.ILogger logger, string operation, string filePath)
    {
        logger.LogInformation("Starting {Operation} for file: {FilePath}", operation, filePath);
    }

    public static void LogProcessingComplete(this Microsoft.Extensions.Logging.ILogger logger, string operation, string filePath, TimeSpan elapsed)
    {
        logger.LogInformation("Completed {Operation} for file: {FilePath} in {ElapsedMs}ms", 
            operation, filePath, elapsed.TotalMilliseconds);
    }

    public static void LogProcessingError(this Microsoft.Extensions.Logging.ILogger logger, string operation, string filePath, Exception exception)
    {
        logger.LogError(exception, "Failed {Operation} for file: {FilePath}", operation, filePath);
    }
}