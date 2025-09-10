using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SignalBooster.Core.Configuration;
using SignalBooster.Core.Features;
using SignalBooster.Core.Features.ProcessNote;
using SignalBooster.Core.Infrastructure.Logging;
using static SignalBooster.Core.Infrastructure.Logging.ApplicationInsightsLoggerExtensions;
using SignalBooster.Core.Models;
using SignalBooster.Core.Services;
using SignalBooster.Core.Validation;
using System.Diagnostics;
using System.Text.Json;

namespace SignalBooster.Core;

/// <summary>
/// Signal Booster - Enterprise-grade DME device order processor
/// 
/// Architecture Overview:
/// - Vertical Slice Architecture: Features are self-contained with their own models, handlers, and validation
/// - Clean Architecture: Dependencies point inward toward domain models
/// - Result Pattern: No exceptions thrown for business logic failures
/// - Dependency Injection: All services registered in ConfigureServices()
/// - Configuration Management: appsettings.json with strongly-typed options
/// - Structured Logging: Serilog with Application Insights support
/// 
/// Key Components:
/// - ProcessNoteHandler: Main business logic orchestrator (Feature slice)
/// - Services: File reading, note parsing, API communication (Infrastructure)
/// - Models: DeviceOrder, PhysicianNote (Domain)
/// - Validation: FluentValidation rules for all inputs (Cross-cutting)
/// </summary>
class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static async Task<int> Main(string[] args)
    {
        // 1. Configuration: Load from appsettings.json with environment overrides
        var configuration = BuildConfiguration();
        
        // 2. Logging: Configure Serilog early for startup diagnostics
        //    - Reads configuration from appsettings.json
        //    - Supports both file logging and Application Insights
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        try
        {
            Log.Information("Starting Signal Booster application");
            
            // 3. Dependency Injection: Create host with all services configured
            //    - Services registered in ConfigureServices()
            //    - Scoped lifetime for request processing
            var host = CreateHost(configuration);
            await using var scope = host.Services.CreateAsyncScope();
            
            // 4. Service Resolution: Get required services from DI container
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            var processNoteHandler = scope.ServiceProvider.GetRequiredService<ProcessNoteHandler>();
            
            // 5. Correlation Tracking: Create correlation context for end-to-end tracing
            using var activity = logger.BeginScopeWithCorrelationId();
            
            // 6. Input Processing: Parse and validate command line arguments
            var (filePath, saveOutput, outputFileName) = ParseCommandLineArgs(args, scope.ServiceProvider);
            
            var request = new ProcessNoteRequest(filePath, saveOutput, outputFileName);
            
            // 7. Tracing Setup: Generate unique IDs for end-to-end request tracking
            //    - OperationId: Single operation tracking
            //    - CorrelationId: Request flow correlation across services
            var operationId = Guid.NewGuid().ToString();
            var correlationId = Guid.NewGuid().ToString();
            
            // 8. Structured Logging: Log processing start for Application Insights
            logger.LogPhysicianNoteProcessingStarted(filePath, operationId, correlationId);
            
            // 9. Performance Monitoring: Track processing time for SLA monitoring
            var stopwatch = Stopwatch.StartNew();
            
            // 10. Business Logic Execution: Delegate to feature handler (Vertical Slice)
            //     - ProcessNoteHandler orchestrates the entire workflow
            //     - Returns Result<T> pattern - no exceptions for business failures
            var result = await processNoteHandler.Handle(request);
            
            stopwatch.Stop();
            
            if (result.IsSuccess)
            {
                var response = result.Value!;
                logger.LogPhysicianNoteProcessingCompleted(
                    filePath, 
                    response.DeviceType, 
                    response.OrderId, 
                    response.Status, 
                    stopwatch.Elapsed, 
                    operationId, 
                    correlationId);
                
                // Display success summary to console
                Console.WriteLine($"\n✅ Processing completed successfully!");
                Console.WriteLine($"📄 File: {response.ProcessedFilePath}");
                Console.WriteLine($"🏥 Device Type: {response.DeviceType}");
                Console.WriteLine($"🆔 Order ID: {response.OrderId}");
                Console.WriteLine($"📊 Status: {response.Status}");
                Console.WriteLine($"⏱️  Processing Time: {stopwatch.ElapsedMilliseconds}ms");
                
                if (!string.IsNullOrEmpty(response.OutputFilePath))
                {
                    Console.WriteLine($"💾 Output saved to: {response.OutputFilePath}");
                }
                
                return 0;
            }
            else
            {
                var error = result.FirstError;
                logger.LogPhysicianNoteProcessingFailed(
                    filePath,
                    error.Code,
                    error.Description,
                    stopwatch.Elapsed,
                    operationId,
                    correlationId);
                
                // Display error summary to console
                Console.WriteLine($"\n❌ Processing failed!");
                Console.WriteLine($"🚨 Error: {error.Code}");
                Console.WriteLine($"📝 Description: {error.Description}");
                Console.WriteLine($"⏱️  Processing Time: {stopwatch.ElapsedMilliseconds}ms");
                
                return 1;
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            Console.WriteLine($"\n💥 Fatal error: {ex.Message}");
            return 1;
        }
        finally
        {
            Log.Information("Signal Booster application shutting down");
            Log.CloseAndFlush();
        }
    }
    
    /// <summary>
    /// Configuration Builder: Hierarchical configuration loading
    /// 1. Base: appsettings.json (required)
    /// 2. Override: Environment variables with SIGNALBOOSTER_ prefix
    /// 3. Support: Hot-reload for development scenarios
    /// </summary>
    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables("SIGNALBOOSTER_")  // Override with env vars: SIGNALBOOSTER_SignalBooster__Api__BaseUrl
            .Build();
    }
    
    private static IHost CreateHost(IConfiguration configuration)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) => ConfigureServices(services, configuration))
            .UseSerilog()
            .Build();
    }
        
    /// <summary>
    /// Dependency Injection Configuration: Register all services with appropriate lifetimes
    /// 
    /// Service Lifetimes:
    /// - Scoped: Per-request lifetime (most business services)
    /// - Singleton: Application lifetime (expensive resources)
    /// - Transient: Per-use lifetime (lightweight services)
    /// </summary>
    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Configuration: Strongly-typed configuration binding
        // Maps appsettings.json "SignalBooster" section to SignalBoosterOptions
        services.Configure<SignalBoosterOptions>(configuration.GetSection(SignalBoosterOptions.SectionName));
        
        // Logging: Application Insights or file-based structured logging
        // Automatically chooses based on configuration availability
        services.AddApplicationInsightsLogging(configuration);
        
        // HTTP Infrastructure: Named HttpClient with connection pooling
        // Prevents socket exhaustion, includes retry policies
        services.AddHttpClient<IApiService, ApiService>();
        
        // Core Services (Infrastructure Layer):
        // - FileService: File I/O operations with validation
        // - NoteParser: Business logic for extracting device orders from notes
        // - ApiService: HTTP client for external API communication
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<INoteParser, NoteParser>();
        services.AddScoped<IApiService, ApiService>();
        
        // Feature Handlers (Application Layer):
        // Vertical slice orchestrators - each feature has its own handler
        services.AddScoped<ProcessNoteHandler>();
        
        // Validation (Cross-cutting):
        // FluentValidation for all input models - registered by interface
        services.AddScoped<IValidator<ProcessNoteRequest>, ProcessNoteRequestValidator>();
        services.AddScoped<IValidator<PhysicianNote>, PhysicianNoteValidator>();
        services.AddScoped<IValidator<DeviceOrder>, DeviceOrderValidator>();
        
        // Add FluentValidation
        services.AddValidatorsFromAssemblyContaining<Program>();
    }
    
    private static (string filePath, bool saveOutput, string? outputFileName) ParseCommandLineArgs(
        string[] args, 
        IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SignalBoosterOptions>>().Value;
        
        var filePath = args.Length > 0 ? args[0] : options.Files.DefaultInputPath;
        var saveOutput = args.Contains("--save-output") || args.Contains("-s");
        
        string? outputFileName = null;
        var outputIndex = Array.FindIndex(args, arg => arg == "--output" || arg == "-o");
        if (outputIndex >= 0 && outputIndex + 1 < args.Length)
        {
            outputFileName = args[outputIndex + 1];
        }
        
        return (filePath, saveOutput, outputFileName);
    }
}
