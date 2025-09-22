using SignalBooster.Models;

namespace SignalBooster.Services;

/// <summary>
/// Interface for advanced agentic AI extraction with multi-step reasoning and validation
/// </summary>
public interface IAgenticExtractor
{
    /// <summary>
    /// Performs multi-agent extraction with validation and error correction
    /// </summary>
    Task<AgenticExtractionResult> ExtractWithAgentsAsync(string noteText, ExtractionContext context);

    /// <summary>
    /// Validates extracted data using specialized validation agents
    /// </summary>
    Task<ValidationResult> ValidateExtractionAsync(DeviceOrder order, string originalText);

    /// <summary>
    /// Self-corrects extraction errors using feedback loops
    /// </summary>
    Task<DeviceOrder> SelfCorrectAsync(DeviceOrder order, ValidationResult validation, string originalText);
}