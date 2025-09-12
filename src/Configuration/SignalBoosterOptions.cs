namespace SignalBooster.Mvp.Configuration;

/// <summary>
/// Root configuration class implementing Options Pattern
/// 
/// SOLID Principles Applied:
/// - Single Responsibility: Each options class handles one configuration area
/// - Open/Closed: Easy to extend with new configuration sections
/// - Interface Segregation: Services only depend on configuration they need
/// 
/// Configuration Pattern Benefits:
/// - Strongly-typed configuration access
/// - Compile-time validation of setting names
/// - IntelliSense support for developers
/// - Hierarchical organization of related settings
/// </summary>
public class SignalBoosterOptions
{
    /// <summary>Configuration section name for appsettings.json binding</summary>
    public const string SectionName = "SignalBooster";
    
    /// <summary>External API configuration</summary>
    public ApiOptions Api { get; set; } = new();
    
    /// <summary>File processing and batch operation settings</summary>
    public FileOptions Files { get; set; } = new();
    
    /// <summary>OpenAI LLM integration configuration</summary>
    public OpenAIOptions OpenAI { get; set; } = new();
}

/// <summary>
/// External API configuration for device order submission
/// Follows Configuration Object pattern for HTTP client setup
/// </summary>
public class ApiOptions
{
    /// <summary>Base URL for external device order API</summary>
    public string BaseUrl { get; set; } = "https://alert-api.com";
    
    /// <summary>API endpoint path for device order submission</summary>
    public string Endpoint { get; set; } = "/device-orders";
    
    /// <summary>HTTP client timeout in seconds</summary>
    public int TimeoutSeconds { get; set; } = 30;
    
    /// <summary>Number of retry attempts for failed API calls</summary>
    public int RetryCount { get; set; } = 3;
    
    /// <summary>Enable API posting (set to false in test environments)</summary>
    public bool EnableApiPosting { get; set; } = true;
}

/// <summary>
/// File processing configuration supporting both single and batch modes
/// Implements Strategy Pattern configuration for processing modes
/// </summary>
public class FileOptions
{
    /// <summary>Default input file when no command line argument provided</summary>
    public string DefaultInputPath { get; set; } = "physician_note.txt";
    
    /// <summary>File extensions to process in batch mode</summary>
    public string[] SupportedExtensions { get; set; } = { ".txt", ".json" };
    
    /// <summary>Enable batch processing of all files in directory</summary>
    public bool BatchProcessingMode { get; set; } = false;
    
    /// <summary>Directory to scan for input files in batch mode</summary>
    public string BatchInputDirectory { get; set; } = "test_notes";
    
    /// <summary>Directory for batch processing output files</summary>
    public string BatchOutputDirectory { get; set; } = "test_outputs";
    
    /// <summary>Delete existing *_actual.json files before batch run</summary>
    public bool CleanupActualFiles { get; set; } = true;
}

/// <summary>
/// OpenAI LLM configuration for advanced text parsing
/// Implements External Service Configuration pattern
/// </summary>
public class OpenAIOptions
{
    /// <summary>OpenAI API key (use environment variables for security)</summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>LLM model to use for text extraction</summary>
    public string Model { get; set; } = "gpt-4o";
    
    /// <summary>Maximum tokens in LLM response</summary>
    public int MaxTokens { get; set; } = 1000;
    
    /// <summary>LLM temperature (0.0 = deterministic, 1.0 = creative)</summary>
    public float Temperature { get; set; } = 0.1f;
}