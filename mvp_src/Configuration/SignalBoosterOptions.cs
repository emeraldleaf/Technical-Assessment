namespace SignalBooster.Mvp.Configuration;

public class SignalBoosterOptions
{
    public const string SectionName = "SignalBooster";
    
    public ApiOptions Api { get; set; } = new();
    public FileOptions Files { get; set; } = new();
    public OpenAIOptions OpenAI { get; set; } = new();
}

public class ApiOptions
{
    public string BaseUrl { get; set; } = "https://alert-api.com";
    public string Endpoint { get; set; } = "/device-orders";
    public int TimeoutSeconds { get; set; } = 30;
    public int RetryCount { get; set; } = 3;
}

public class FileOptions
{
    public string DefaultInputPath { get; set; } = "physician_note.txt";
    public string[] SupportedExtensions { get; set; } = { ".txt", ".json" };
    public bool BatchProcessingMode { get; set; } = false;
    public string BatchInputDirectory { get; set; } = "test_notes";
    public string BatchOutputDirectory { get; set; } = "test_outputs";
    public bool CleanupActualFiles { get; set; } = true;
}

public class OpenAIOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-3.5-turbo";
    public int MaxTokens { get; set; } = 1000;
    public float Temperature { get; set; } = 0.1f;
}