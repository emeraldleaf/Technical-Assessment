namespace SignalBooster.Core.Configuration;

public sealed class SignalBoosterOptions
{
    public const string SectionName = "SignalBooster";
    
    public ApiOptions Api { get; set; } = new();
    public FileOptions Files { get; set; } = new();
    public LoggingOptions Logging { get; set; } = new();
}

public sealed class ApiOptions
{
    public string BaseUrl { get; set; } = "https://alert-api.com";
    public string ExtractEndpoint { get; set; } = "/DrExtract";
    public int TimeoutSeconds { get; set; } = 30;
    public int RetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 2;
}

public sealed class FileOptions
{
    public string DefaultInputPath { get; set; } = "../../assignment/physician_note1.txt";
    public string OutputDirectory { get; set; } = "output";
    public bool CreateOutputDirectory { get; set; } = true;
    public string[] SupportedExtensions { get; set; } = [".txt", ".json"];
}

public sealed class LoggingOptions
{
    public string LogLevel { get; set; } = "Information";
    public string LogOutputPath { get; set; } = "logs/signalbooster-.log";
    public bool EnableConsoleLogging { get; set; } = true;
    public bool EnableFileLogging { get; set; } = true;
    public bool EnableStructuredLogging { get; set; } = true;
    public int RetainedFileCountLimit { get; set; } = 10;
}