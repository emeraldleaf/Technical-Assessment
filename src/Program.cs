using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using SignalBooster.Configuration;
using SignalBooster.Models;
using SignalBooster.Services;

namespace SignalBooster;

/// <summary>
/// SignalBooster MVP - DME (Durable Medical Equipment) Processing Application
/// 
/// Architecture Overview:
/// - Layered Service Architecture with separation of concerns
/// - Dependency Injection for loose coupling and testability
/// - Configuration-driven behavior for flexibility
/// - Structured logging with Application Insights integration
/// - Both single-file and batch processing modes
/// 
/// Application Flow:
/// 1. Configuration setup and logging initialization
/// 2. Dependency injection container setup
/// 3. Mode detection (single file vs batch processing)
/// 4. Text extraction and LLM processing
/// 5. API submission and output generation
/// </summary>
class Program
{
    /// <summary>
    /// Application entry point implementing enterprise-grade error handling and logging
    /// </summary>
    static async Task<int> Main(string[] args)
    {
        // Step 1: Configuration Setup - builds hierarchical configuration from multiple sources
        var configuration = BuildConfiguration();
        
        // Step 2: Logging Infrastructure Setup 
        // Pattern: Structured Logging with multiple sinks for observability
        var loggerConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)  // Uses appsettings.json Serilog section
            .WriteTo.Console()                      // Local development visibility
            .WriteTo.File("logs/signal-booster-.txt", rollingInterval: RollingInterval.Day); // Persistent logs
        
        // Optional Application Insights integration for production telemetry
        var appInsightsConnectionString = configuration.GetValue<string>("ApplicationInsights:ConnectionString");
        if (!string.IsNullOrEmpty(appInsightsConnectionString))
        {
            loggerConfig.WriteTo.ApplicationInsights(
                new TelemetryConfiguration { ConnectionString = appInsightsConnectionString },
                TelemetryConverter.Traces);
        }
        
        // Initialize global static logger (Serilog pattern)
        Log.Logger = loggerConfig.CreateLogger();

        try
        {
            Log.Information("Starting Signal Booster application with enhanced features");
            
            // Step 3: Dependency Injection Container Setup
            // Pattern: Composition Root - all dependencies configured in one place
            var host = CreateHost(configuration);
            
            // Step 4: Service Resolution from DI Container
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var extractor = host.Services.GetRequiredService<DeviceExtractor>(); // Main business logic service
            var options = host.Services.GetRequiredService<IOptions<SignalBoosterOptions>>().Value; // Strongly-typed config
            
            // Step 5: Processing Mode Decision - Strategy Pattern
            // Allows switching between single file and batch processing without code changes
            if (options.Files.BatchProcessingMode)
            {
                // Batch Processing Strategy: Process all files in directory
                logger.LogInformation("Batch processing mode enabled. Processing all files in {InputDirectory} with LLM integration: {HasOpenAI}", 
                    options.Files.BatchInputDirectory, !string.IsNullOrEmpty(options.OpenAI.ApiKey));
                
                var batchResults = await extractor.ProcessAllNotesAsync();
                
                // User-friendly console output for batch results
                Console.WriteLine($"Batch processing completed:");
                Console.WriteLine($"Successfully processed {batchResults.Count} files");
                
                foreach (var (fileName, result) in batchResults)
                {
                    Console.WriteLine($"  ✓ {fileName} → {result.Device} for {result.PatientName}");
                }
                
                logger.LogInformation("Batch processing completed. Processed {ProcessedCount} files successfully", batchResults.Count);
            }
            else
            {
                // Single File Processing Strategy: Traditional one-file-at-a-time processing
                var filePath = args.Length > 0 ? args[0] : options.Files.DefaultInputPath;
                
                logger.LogInformation("Processing physician note: {FilePath} with LLM integration: {HasOpenAI}", 
                    filePath, !string.IsNullOrEmpty(options.OpenAI.ApiKey));
                
                // Core business logic execution
                var deviceOrder = await extractor.ProcessNoteAsync(filePath);
                
                // Output formatting with JSON serialization
                var outputObject = CreateOutputObject(deviceOrder); // Transforms to clean output format
                var output = JsonSerializer.Serialize(outputObject, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, // snake_case for API compatibility
                    WriteIndented = true // Human-readable formatting
                });
                
                // User feedback and file output
                Console.WriteLine("Device order extracted:");
                Console.WriteLine(output);
                
                await File.WriteAllTextAsync("output.json", output);
                logger.LogInformation("Output saved to output.json");
            }
            
            Log.Information("Processing completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
    
    /// <summary>
    /// Builds hierarchical configuration from multiple sources with precedence
    /// 
    /// Configuration Hierarchy (highest to lowest precedence):
    /// 1. Environment Variables (SIGNALBOOSTER_ prefix)
    /// 2. appsettings.Local.json (git-ignored for local overrides)
    /// 3. appsettings.Development.json (environment-specific)
    /// 4. appsettings.json (base configuration)
    /// 
    /// Pattern: Configuration Provider Chain
    /// - Allows different settings per environment without code changes
    /// - Supports secure secret management via environment variables
    /// </summary>
    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)         // Base config
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)   // Local overrides
            .AddEnvironmentVariables("SIGNALBOOSTER_")                                     // Production secrets
            .Build();
    }
    
    /// <summary>
    /// Creates and configures the Dependency Injection container
    /// 
    /// SOLID Principles Applied:
    /// - Dependency Inversion: Depend on abstractions (interfaces), not concretions
    /// - Single Responsibility: Each service has one clear purpose
    /// - Interface Segregation: Services depend only on interfaces they actually use
    /// 
    /// Service Lifetimes:
    /// - Scoped: New instance per request/operation (stateful services)
    /// - Singleton: Single instance for app lifetime (stateless services)
    /// - Transient: New instance every time (rarely used here)
    /// </summary>
    private static IHost CreateHost(IConfiguration configuration)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                // Strongly-typed Configuration Binding
                // Pattern: Options Pattern - type-safe configuration access
                services.Configure<SignalBoosterOptions>(
                    configuration.GetSection(SignalBoosterOptions.SectionName));
                
                // HTTP Client Factory Pattern with pre-configured settings
                // Benefits: Connection pooling, automatic retry, centralized configuration
                services.AddHttpClient<IApiClient, ApiClient>((serviceProvider, client) =>
                {
                    var options = serviceProvider.GetRequiredService<IOptions<SignalBoosterOptions>>();
                    client.BaseAddress = new Uri(options.Value.Api.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(options.Value.Api.TimeoutSeconds);
                });
                
                // File System Abstraction for testability
                services.AddSingleton<IFileSystem, FileSystem>();  // Real file system for production
                
                // Core Business Logic Services (Scoped for request isolation)
                services.AddScoped<IFileReader, FileReader>();     // File I/O operations
                services.AddScoped<ITextParser, TextParser>();     // LLM and regex parsing
                services.AddScoped<IAgenticExtractor, AgenticExtractor>(); // Advanced agentic AI extraction
                services.AddScoped<DeviceExtractor>();             // Main orchestration service
                
                // Optional Application Insights Telemetry
                // Pattern: Conditional Service Registration
                var appInsightsConnectionString = configuration.GetValue<string>("ApplicationInsights:ConnectionString");
                if (!string.IsNullOrEmpty(appInsightsConnectionString))
                {
                    services.AddApplicationInsightsTelemetry(options =>
                    {
                        options.ConnectionString = appInsightsConnectionString;
                    });
                }
            })
            .UseSerilog() // Replace default logging with Serilog
            .Build();
    }

    
    /// <summary>
    /// Transforms domain model to clean output format with null value handling
    /// 
    /// Design Patterns:
    /// - Adapter Pattern: Converts internal model to external API format
    /// - Null Object Pattern: Excludes null/empty values from output
    /// 
    /// Why Dynamic Dictionary vs Fixed Object:
    /// - Flexible output structure (only includes relevant fields)
    /// - Cleaner JSON without null properties
    /// - Easy to extend for new device types
    /// </summary>
    private static object CreateOutputObject(DeviceOrder deviceOrder)
    {
        var output = new Dictionary<string, object>();
        
        // Required fields (always included)
        output["device"] = deviceOrder.Device;
        
        // Optional fields (only included if present)
        if (!string.IsNullOrEmpty(deviceOrder.Liters))
            output["liters"] = deviceOrder.Liters;
        
        if (!string.IsNullOrEmpty(deviceOrder.Usage))
            output["usage"] = deviceOrder.Usage;
        
        if (!string.IsNullOrEmpty(deviceOrder.Diagnosis))
            output["diagnosis"] = deviceOrder.Diagnosis;
        
        output["ordering_provider"] = deviceOrder.OrderingProvider;
        
        if (!string.IsNullOrEmpty(deviceOrder.PatientName))
            output["patient_name"] = deviceOrder.PatientName;
        
        if (!string.IsNullOrEmpty(deviceOrder.Dob))
            output["dob"] = deviceOrder.Dob;
        
        if (!string.IsNullOrEmpty(deviceOrder.MaskType))
            output["mask_type"] = deviceOrder.MaskType;
        
        if (deviceOrder.AddOns != null && deviceOrder.AddOns.Length > 0)
            output["add_ons"] = deviceOrder.AddOns;
        
        if (!string.IsNullOrEmpty(deviceOrder.Qualifier))
            output["qualifier"] = deviceOrder.Qualifier;
        
        return output;
    }
}