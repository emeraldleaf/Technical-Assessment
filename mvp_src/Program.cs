using System.Text.Json;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using SignalBooster.Mvp.Configuration;
using SignalBooster.Mvp.Models;
using SignalBooster.Mvp.Services;

namespace SignalBooster.Mvp;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var configuration = BuildConfiguration();
        
        // Configure Serilog with Application Insights
        var loggerConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.Console()
            .WriteTo.File("logs/signal-booster-.txt", rollingInterval: RollingInterval.Day);
        
        var appInsightsConnectionString = configuration.GetValue<string>("ApplicationInsights:ConnectionString");
        if (!string.IsNullOrEmpty(appInsightsConnectionString))
        {
            loggerConfig.WriteTo.ApplicationInsights(
                new TelemetryConfiguration { ConnectionString = appInsightsConnectionString },
                TelemetryConverter.Traces);
        }
        
        Log.Logger = loggerConfig.CreateLogger();

        try
        {
            Log.Information("Starting Signal Booster application with enhanced features");
            
            var host = CreateHost(configuration);
            
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var extractor = host.Services.GetRequiredService<DeviceExtractor>();
            var options = host.Services.GetRequiredService<IOptions<SignalBoosterOptions>>().Value;
            
            // Use configuration for default file path
            var filePath = args.Length > 0 ? args[0] : options.Files.DefaultInputPath;
            
            logger.LogInformation("Processing physician note: {FilePath} with LLM integration: {HasOpenAI}", 
                filePath, !string.IsNullOrEmpty(options.OpenAI.ApiKey));
            
            var deviceOrder = await extractor.ProcessNoteAsync(filePath);
            
            var outputObject = CreateOutputObject(deviceOrder);
            var output = JsonSerializer.Serialize(outputObject, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true
            });
            
            Console.WriteLine("Device order extracted:");
            Console.WriteLine(output);
            
            await File.WriteAllTextAsync("output.json", output);
            logger.LogInformation("Output saved to output.json");
            
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
    
    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables("SIGNALBOOSTER_")
            .Build();
    }
    
    private static IHost CreateHost(IConfiguration configuration)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                // Configuration
                services.Configure<SignalBoosterOptions>(
                    configuration.GetSection(SignalBoosterOptions.SectionName));
                
                // HTTP Client with configured base URL
                services.AddHttpClient<IApiClient, ApiClient>((serviceProvider, client) =>
                {
                    var options = serviceProvider.GetRequiredService<IOptions<SignalBoosterOptions>>();
                    client.BaseAddress = new Uri(options.Value.Api.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(options.Value.Api.TimeoutSeconds);
                });
                
                // Core Services
                services.AddScoped<IFileReader, FileReader>();
                services.AddScoped<ITextParser, TextParser>();
                services.AddScoped<DeviceExtractor>();
                
                // Application Insights
                var appInsightsConnectionString = configuration.GetValue<string>("ApplicationInsights:ConnectionString");
                if (!string.IsNullOrEmpty(appInsightsConnectionString))
                {
                    services.AddApplicationInsightsTelemetry(appInsightsConnectionString);
                }
            })
            .UseSerilog()
            .Build();
    }

    
    private static object CreateOutputObject(DeviceOrder deviceOrder)
    {
        var output = new Dictionary<string, object>();
        
        output["device"] = deviceOrder.Device;
        
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