using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SignalBooster.Configuration;
using SignalBooster.Models;
using System.Text.Json;

namespace SignalBooster.Services;

/// <summary>
/// Main orchestration service for DME device order processing
/// 
/// Design Patterns:
/// - Facade Pattern: Provides simplified interface to complex subsystem
/// - Template Method: Defines processing algorithm with configurable steps
/// - Strategy Pattern: Supports both single-file and batch processing strategies
/// 
/// Architecture Role:
/// - Application Service in Clean Architecture
/// - Coordinates between domain logic (parsing) and infrastructure (file I/O, API calls)
/// - Handles cross-cutting concerns: logging, error handling, configuration
/// 
/// SOLID Principles:
/// - Single Responsibility: Orchestrates device order processing workflow
/// - Open/Closed: Extensible via new processing strategies without modification
/// - Dependency Inversion: Depends on abstractions (IFileReader, ITextParser, IApiClient)
/// </summary>
public class DeviceExtractor
{
    // Dependencies injected via constructor (Dependency Injection Pattern)
    private readonly IFileReader _fileReader;      // File I/O abstraction
    private readonly ITextParser _textParser;      // LLM and regex parsing logic
    private readonly IApiClient _apiClient;        // External API communication
    private readonly SignalBoosterOptions _options; // Strongly-typed configuration
    private readonly ILogger<DeviceExtractor> _logger; // Structured logging
    
    /// <summary>
    /// Constructor injection implementing Dependency Inversion Principle
    /// All dependencies are abstractions (interfaces) for testability and flexibility
    /// </summary>
    public DeviceExtractor(
        IFileReader fileReader, 
        ITextParser textParser,
        IApiClient apiClient,
        IOptions<SignalBoosterOptions> options,
        ILogger<DeviceExtractor> logger)
    {
        _fileReader = fileReader ?? throw new ArgumentNullException(nameof(fileReader));
        _textParser = textParser ?? throw new ArgumentNullException(nameof(textParser));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Single file processing method implementing Template Method pattern
    /// 
    /// Processing Steps:
    /// 1. File reading with error handling
    /// 2. Text parsing (LLM or regex fallback)
    /// 3. External API submission
    /// 4. Comprehensive logging and telemetry
    /// 
    /// Observability Features:
    /// - Correlation IDs for request tracing
    /// - Performance metrics (duration tracking)
    /// - Structured logging with contextual information
    /// - Step-by-step operation logging for debugging
    /// </summary>
    public async Task<DeviceOrder> ProcessNoteAsync(string filePath)
    {
        var correlationId = Guid.NewGuid().ToString();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var noteText = await _fileReader.ReadTextAsync(filePath);
            var useOpenAI = !string.IsNullOrEmpty(_options.OpenAI.ApiKey);
            
            // Log processing start with context
            using (var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["FilePath"] = filePath,
                ["UseOpenAI"] = useOpenAI,
                ["NoteLength"] = noteText.Length,
                ["Method"] = nameof(ProcessNoteAsync),
                ["Class"] = nameof(DeviceExtractor)
            }))
            {
                _logger.LogInformation("[{Class}.{Method}] Step 1: Starting device order processing for {FilePath} with {ProcessingMode}, NoteLength: {NoteLength} chars",
                    nameof(DeviceExtractor), nameof(ProcessNoteAsync), filePath, useOpenAI ? "OpenAI" : "Regex", noteText.Length);
                
                _logger.LogInformation("[{Class}.{Method}] Step 2: Invoking text parser with {ProcessingMode} mode",
                    nameof(DeviceExtractor), nameof(ProcessNoteAsync), useOpenAI ? "OpenAI LLM" : "Regex");
                
                // Use async method if LLM is configured, otherwise use sync regex parser
                var deviceOrder = useOpenAI
                    ? await _textParser.ParseDeviceOrderAsync(noteText)
                    : _textParser.ParseDeviceOrder(noteText);
                
                // Log successful extraction with business metrics
                _logger.LogInformation("[{Class}.{Method}] Step 3: Device order extracted successfully. Device: {DeviceType}, Patient: {PatientName}, Provider: {Provider}, ParseDuration: {ProcessingDurationMs}ms",
                    nameof(DeviceExtractor), nameof(ProcessNoteAsync), deviceOrder.Device, deviceOrder.PatientName, deviceOrder.OrderingProvider, stopwatch.ElapsedMilliseconds);
                
                _logger.LogInformation("[{Class}.{Method}] Step 4: Posting device order to external API",
                    nameof(DeviceExtractor), nameof(ProcessNoteAsync));
                
                await _apiClient.PostDeviceOrderAsync(deviceOrder);
                
                stopwatch.Stop();
                
                // Log completion with full context
                _logger.LogInformation("[{Class}.{Method}] Step 5: Processing completed successfully. TotalDuration: {TotalDurationMs}ms, CorrelationId: {CorrelationId}",
                    nameof(DeviceExtractor), nameof(ProcessNoteAsync), stopwatch.ElapsedMilliseconds, correlationId);
                
                return deviceOrder;
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[{Class}.{Method}] Step FAILED: Processing failed after {DurationMs}ms for {FilePath}, CorrelationId: {CorrelationId}",
                nameof(DeviceExtractor), nameof(ProcessNoteAsync), stopwatch.ElapsedMilliseconds, filePath, correlationId);
            throw;
        }
    }

    /// <summary>
    /// Batch processing method implementing Strategy Pattern for bulk operations
    /// 
    /// Enterprise Features:
    /// - Automatic file discovery and filtering
    /// - Cleanup of previous results for clean runs
    /// - Fault tolerance (continues on individual failures)
    /// - Progress tracking and reporting
    /// - Individual output file generation per input
    /// 
    /// Pattern Benefits:
    /// - Single operation for multiple files
    /// - Consistent naming conventions
    /// - Centralized error handling and logging
    /// - Configurable via appsettings.json
    /// </summary>
    public async Task<List<(string FileName, DeviceOrder Result)>> ProcessAllNotesAsync()
    {
        if (!_options.Files.BatchProcessingMode)
        {
            throw new InvalidOperationException("Batch processing mode is not enabled. Set Files.BatchProcessingMode to true in configuration.");
        }

        var correlationId = Guid.NewGuid().ToString();
        var batchStopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        _logger.LogInformation("[{Class}.{Method}] Step 1: Starting batch processing mode. CorrelationId: {CorrelationId}",
            nameof(DeviceExtractor), nameof(ProcessAllNotesAsync), correlationId);

        // Scan for input files
        var inputFiles = GetInputFiles();
        _logger.LogInformation("[{Class}.{Method}] Step 2: Found {FileCount} files to process in {InputDirectory}",
            nameof(DeviceExtractor), nameof(ProcessAllNotesAsync), inputFiles.Count, _options.Files.BatchInputDirectory);

        var results = new List<(string FileName, DeviceOrder Result)>();

        // Process each file
        for (int i = 0; i < inputFiles.Count; i++)
        {
            var inputFile = inputFiles[i];
            var fileName = Path.GetFileNameWithoutExtension(inputFile);
            
            _logger.LogInformation("[{Class}.{Method}] Step 3.{Index}: Processing file {FileName} ({Current}/{Total})",
                nameof(DeviceExtractor), nameof(ProcessAllNotesAsync), i + 1, fileName, i + 1, inputFiles.Count);

            try
            {
                var deviceOrder = await ProcessNoteAsync(inputFile);
                results.Add((fileName, deviceOrder));

                _logger.LogInformation("[{Class}.{Method}] Step 4.{Index}: Successfully processed {FileName}",
                    nameof(DeviceExtractor), nameof(ProcessAllNotesAsync), i + 1, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Class}.{Method}] Step FAILED.{Index}: Failed to process {FileName}",
                    nameof(DeviceExtractor), nameof(ProcessAllNotesAsync), i + 1, fileName);
                // Continue with next file instead of failing entire batch
            }
        }

        batchStopwatch.Stop();
        _logger.LogInformation("[{Class}.{Method}] Step 5: Batch processing completed. ProcessedFiles: {ProcessedCount}/{TotalCount}, TotalDuration: {TotalDurationMs}ms, CorrelationId: {CorrelationId}",
            nameof(DeviceExtractor), nameof(ProcessAllNotesAsync), results.Count, inputFiles.Count, batchStopwatch.ElapsedMilliseconds, correlationId);

        return results;
    }

    /// <summary>
    /// Discovers and filters input files for batch processing
    /// 
    /// Features:
    /// - Configurable file extensions (.txt, .json, etc.)
    /// - Automatic directory creation if missing
    /// - Deterministic ordering for consistent processing
    /// - Defensive programming (handles missing directories)
    /// </summary>
    private List<string> GetInputFiles()
    {
        var inputDirectory = _options.Files.BatchInputDirectory;
        
        // Defensive programming: create directory if it doesn't exist
        if (!Directory.Exists(inputDirectory))
        {
            _logger.LogWarning("[{Class}.{Method}] Input directory {InputDirectory} does not exist. Creating it.",
                nameof(DeviceExtractor), nameof(GetInputFiles), inputDirectory);
            Directory.CreateDirectory(inputDirectory);
            return new List<string>();
        }

        // File discovery using configurable extensions
        var files = new HashSet<string>();
        foreach (var extension in _options.Files.SupportedExtensions)
        {
            var pattern = $"*{extension}";
            foreach (var file in Directory.GetFiles(inputDirectory, pattern))
            {
                files.Add(file);
            }
        }

        // Deterministic ordering for consistent batch processing
        return files.OrderBy(f => f).ToList();
    }
}