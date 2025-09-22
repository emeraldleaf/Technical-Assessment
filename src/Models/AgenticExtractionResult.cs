namespace SignalBooster.Models;

/// <summary>
/// Result of agentic extraction with confidence scores and reasoning
/// </summary>
public record AgenticExtractionResult
{
    public DeviceOrder DeviceOrder { get; init; } = new();
    public double ConfidenceScore { get; init; }
    public ExtractionMetadata Metadata { get; init; } = new();
    public List<AgentStep> ReasoningSteps { get; init; } = new();
    public ValidationResult? ValidationResult { get; init; }
}

/// <summary>
/// Metadata about the extraction process
/// </summary>
public record ExtractionMetadata
{
    public string ExtractorVersion { get; init; } = string.Empty;
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;
    public TimeSpan ProcessingDuration { get; init; }
    public int TokensUsed { get; init; }
    public List<string> AgentsUsed { get; init; } = new();
    public Dictionary<string, object> AdditionalData { get; init; } = new();
}

/// <summary>
/// Individual reasoning step by an agent
/// </summary>
public record AgentStep
{
    public string AgentName { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Reasoning { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public Dictionary<string, object> Outputs { get; init; } = new();
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Context for extraction process
/// </summary>
public record ExtractionContext
{
    public string SourceFile { get; init; } = string.Empty;
    public string DocumentType { get; init; } = "physician_note";
    public Dictionary<string, string> Hints { get; init; } = new();
    public ExtractionMode Mode { get; init; } = ExtractionMode.Standard;
    public bool RequireValidation { get; init; } = true;
}

/// <summary>
/// Extraction processing modes
/// </summary>
public enum ExtractionMode
{
    Fast,       // Single-pass extraction
    Standard,   // Multi-agent with validation
    Thorough    // Comprehensive with multiple validation rounds
}

/// <summary>
/// Validation result for extracted data
/// </summary>
public record ValidationResult
{
    public bool IsValid { get; init; }
    public double ValidationScore { get; init; }
    public List<ValidationIssue> Issues { get; init; } = new();
    public Dictionary<string, double> FieldConfidences { get; init; } = new();
    public List<string> Suggestions { get; init; } = new();
}

/// <summary>
/// Individual validation issue
/// </summary>
public record ValidationIssue
{
    public string Field { get; init; } = string.Empty;
    public string Issue { get; init; } = string.Empty;
    public ValidationSeverity Severity { get; init; }
    public string? SuggestedFix { get; init; }
}

/// <summary>
/// Severity levels for validation issues
/// </summary>
public enum ValidationSeverity
{
    Info,
    Warning,
    Error,
    Critical
}