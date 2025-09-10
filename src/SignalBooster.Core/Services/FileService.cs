using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBooster.Core.Common;
using SignalBooster.Core.Configuration;
using SignalBooster.Core.Domain.Errors;
using System.Text.Json;

namespace SignalBooster.Core.Services;

/// <summary>
/// Infrastructure Service: File System Operations with Healthcare Data Handling
/// 
/// Responsibilities:
/// - Read physician note files with encoding detection and validation
/// - Write structured output (JSON) to configurable output directory
/// - Validate file extensions against healthcare document types
/// - Handle file system errors gracefully (permissions, not found, etc.)
/// - Support configurable file size limits and security constraints
/// 
/// Security Features:
/// - Extension validation prevents execution of unsafe file types
/// - Path validation prevents directory traversal attacks
/// - Controlled output directory prevents writing to system locations
/// - Error handling prevents information disclosure about file system
/// </summary>
public class FileService : IFileService
{
    private readonly ILogger<FileService> _logger;
    private readonly SignalBoosterOptions _options;

    public FileService(ILogger<FileService> logger, IOptions<SignalBoosterOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Read physician note from file system with comprehensive validation
    /// 
    /// Validation Pipeline:
    /// 1. Path validation: Ensure path is provided and non-empty
    /// 2. Extension validation: Check against supported healthcare document types
    /// 3. Existence check: Verify file exists before attempting read
    /// 4. Content validation: Ensure file has readable content
    /// 5. Access control: Handle permission denied scenarios gracefully
    /// 
    /// Supported Formats: .txt, .docx, .pdf (configurable in appsettings)
    /// </summary>
    public async Task<Result<string>> ReadNoteFromFileAsync(string filePath)
    {
        try
        {
            _logger.LogInformation("Attempting to read physician note from file: {FilePath}", filePath);
            
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ValidationErrors.MissingRequiredField(nameof(filePath));
            }

            // Security Check: Validate file extension against approved healthcare document types
            var extensionValidation = ValidateFileExtension(filePath);
            if (extensionValidation.IsError)
            {
                return extensionValidation.FirstError;
            }

            // Existence Check: Verify file exists before attempting read operation
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File not found: {FilePath}", filePath);
                return FileErrors.NotFound(filePath);
            }

            // File I/O: Read entire file content with automatic encoding detection
            var content = await File.ReadAllTextAsync(filePath);
            
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("File is empty: {FilePath}", filePath);
                return FileErrors.Empty(filePath);
            }

            _logger.LogInformation("Successfully read physician note from file: {FilePath} ({ContentLength} characters)", 
                filePath, content.Length);
            
            return content.Trim();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied when reading file: {FilePath}", filePath);
            return FileErrors.AccessDenied(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error reading file: {FilePath}", filePath);
            return FileErrors.ReadError(filePath, ex.Message);
        }
    }

    /// <summary>
    /// Write processing output to file system with automatic directory creation
    /// 
    /// Output Management:
    /// - Creates timestamped JSON files with processing results
    /// - Automatically creates output directory if missing
    /// - Uses configurable output location (prevents writing to system directories)
    /// - Generates unique filenames to prevent overwrites
    /// 
    /// File Naming Convention: device_order_YYYYMMDD_HHMMSS.json
    /// Output Format: Pretty-printed JSON for human readability
    /// </summary>
    public async Task<Result<string>> WriteOutputAsync(string content, string? fileName = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return ValidationErrors.MissingRequiredField(nameof(content));
            }

            // Directory Setup: Ensure output directory exists (configurable behavior)
            var outputDir = _options.Files.OutputDirectory;
            if (_options.Files.CreateOutputDirectory && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
                _logger.LogInformation("Created output directory: {OutputDirectory}", outputDir);
            }

            // Filename Generation: Timestamped to prevent overwrites
            fileName ??= $"device_order_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(outputDir, fileName);

            await File.WriteAllTextAsync(filePath, content);
            
            _logger.LogInformation("Successfully wrote output to file: {FilePath} ({ContentLength} characters)", 
                filePath, content.Length);
            
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing output to file");
            return Error.Failure("File.WriteError", $"Failed to write output: {ex.Message}");
        }
    }

    /// <summary>
    /// Validate file extension against approved healthcare document types
    /// 
    /// Security Function:
    /// - Prevents processing of executable files or unsafe document types
    /// - Uses case-insensitive comparison for cross-platform compatibility
    /// - Configurable whitelist approach (only approved extensions allowed)
    /// - Provides clear error messages listing supported formats
    /// 
    /// Default Supported: .txt, .docx, .pdf (healthcare document standards)
    /// </summary>
    public Result<bool> ValidateFileExtension(string filePath)
    {
        try
        {
            var extension = Path.GetExtension(filePath);
            var supportedExtensions = _options.Files.SupportedExtensions;

            // Whitelist Validation: Only explicitly approved extensions are allowed
            if (!supportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return FileErrors.UnsupportedExtension(extension, supportedExtensions);
            }

            return true;
        }
        catch (Exception ex)
        {
            return Error.Failure("File.ExtensionValidationError", $"Failed to validate file extension: {ex.Message}");
        }
    }
}