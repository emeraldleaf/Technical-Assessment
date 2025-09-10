using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using SignalBooster.Core.Common;
using SignalBooster.Core.Domain.Errors;
using SignalBooster.Core.Models;

namespace SignalBooster.Core.Services;

/// <summary>
/// Business Logic Service: Physician Note Text Processing and DME Device Extraction
/// 
/// Core Responsibilities:
/// - Parse unstructured physician notes into structured PhysicianNote objects
/// - Extract DME device requirements and specifications using healthcare domain knowledge
/// - Apply device-specific parsing rules (CPAP, Oxygen, Wheelchair, etc.)
/// - Validate parsed data using FluentValidation rules
/// 
/// Implementation Pattern:
/// - Regex-based text extraction with healthcare-specific patterns
/// - Strategy pattern: device type determines extraction strategy
/// - Fail-safe parsing: returns "Unknown" for missing data rather than errors
/// - Extensible: easy to add new device types by adding patterns and extraction methods
/// </summary>
public class NoteParser : INoteParser
{
    private readonly ILogger<NoteParser> _logger;
    private readonly IValidator<PhysicianNote> _noteValidator;
    private readonly IValidator<DeviceOrder> _deviceOrderValidator;

    public NoteParser(
        ILogger<NoteParser> logger, 
        IValidator<PhysicianNote> noteValidator,
        IValidator<DeviceOrder> deviceOrderValidator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _noteValidator = noteValidator ?? throw new ArgumentNullException(nameof(noteValidator));
        _deviceOrderValidator = deviceOrderValidator ?? throw new ArgumentNullException(nameof(deviceOrderValidator));
    }

    /// <summary>
    /// Phase 1: Convert raw text into structured PhysicianNote object
    /// 
    /// Extraction Strategy:
    /// - Uses regex patterns to find structured fields (Patient Name:, DOB:, etc.)
    /// - Falls back to intelligent defaults for missing fields
    /// - Applies business rules (e.g., auto-generate patient ID if missing)
    /// - Validates result using FluentValidation rules
    /// </summary>
    public Result<PhysicianNote> ParseNoteFromText(string noteText)
    {
        try
        {
            _logger.LogInformation("Parsing physician note from text ({Length} characters)", noteText?.Length ?? 0);
            
            if (string.IsNullOrWhiteSpace(noteText))
            {
                return ValidationErrors.MissingRequiredField(nameof(noteText));
            }

            // Extract structured fields using regex patterns
            // Each method handles multiple formats and provides fallback values
            var patientName = ExtractPatientName(noteText);
            var patientId = ExtractPatientId(noteText);
            var dateOfBirth = ExtractDateOfBirth(noteText);
            var diagnosis = ExtractDiagnosis(noteText);
            var prescription = ExtractPrescription(noteText);
            var usage = ExtractUsage(noteText);
            var orderingProvider = ExtractOrderingProvider(noteText);
            var noteDate = ExtractNoteDate(noteText);

            var note = new PhysicianNote(
                patientName,
                dateOfBirth,
                diagnosis,
                prescription,
                usage,
                orderingProvider,
                noteText
            )
            {
                PatientId = patientId,
                NoteDate = noteDate
            };

            // Validate the parsed note
            var validationResult = _noteValidator.Validate(note);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .Select(e => ValidationErrors.InvalidFormat(e.PropertyName, e.ErrorMessage))
                    .ToList();
                
                _logger.LogWarning("Note validation failed: {Errors}", 
                    string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                
                return errors;
            }

            _logger.LogInformation("Successfully parsed physician note for patient: {PatientName}", patientName);
            return note;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing physician note");
            return ValidationErrors.NoteParsingFailed(ex.Message);
        }
    }

    /// <summary>
    /// Phase 2: Extract DME device order from structured physician note
    /// 
    /// Business Logic Flow:
    /// 1. Analyze note text to determine device type (CPAP, Oxygen, Wheelchair, etc.)
    /// 2. Apply device-specific parsing rules to extract specifications
    /// 3. Create DeviceOrder with parsed data and computed properties
    /// 4. Validate order meets healthcare requirements
    /// 
    /// Device Detection: Uses keyword matching against healthcare terminology
    /// Specifications: Each device type has unique parsing logic for relevant specs
    /// </summary>
    public Result<DeviceOrder> ExtractDeviceOrder(PhysicianNote note)
    {
        try
        {
            _logger.LogInformation("Extracting device order from physician note for patient: {PatientName}", note.PatientName);
            
            // Step 1: Device Type Detection using healthcare keyword patterns
            var device = DetermineDeviceType(note.RawText);
            
            // Step 2: Device-Specific Specification Extraction
            var specifications = ExtractDeviceSpecifications(note.RawText, device);
            
            var deviceOrder = new DeviceOrder(
                device,
                null, // Legacy maskType parameter
                null, // Legacy addOns parameter
                null, // Legacy qualifier parameter
                note.OrderingProvider,
                null, // Legacy liters parameter
                null, // Legacy usage parameter
                note.Diagnosis,
                note.PatientName,
                note.DateOfBirth
            )
            {
                PatientId = note.PatientId,
                Specifications = specifications,
                OrderDate = DateTime.UtcNow
            };

            // Validate the device order
            var validationResult = _deviceOrderValidator.Validate(deviceOrder);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .Select(e => ValidationErrors.InvalidFormat(e.PropertyName, e.ErrorMessage))
                    .ToList();
                
                _logger.LogWarning("Device order validation failed: {Errors}", 
                    string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                
                return errors;
            }

            _logger.LogInformation("Successfully extracted device order: {DeviceType} for patient: {PatientName}", 
                device, note.PatientName);
            
            return deviceOrder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting device order from note");
            return ValidationErrors.DeviceOrderValidationFailed(ex.Message);
        }
    }

    private string ExtractPatientName(string text)
    {
        var match = Regex.Match(text, @"(?:Patient Name|Patient):\s*(.+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "Unknown";
    }

    private string ExtractPatientId(string text)
    {
        var match = Regex.Match(text, @"(?:Patient ID|ID):\s*(.+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : Guid.NewGuid().ToString("N")[..8];
    }

    private string ExtractDateOfBirth(string text)
    {
        var match = Regex.Match(text, @"DOB:\s*(.+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "Unknown";
    }

    private string ExtractDiagnosis(string text)
    {
        var match = Regex.Match(text, @"Diagnosis:\s*(.+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "Unknown";
    }

    private string ExtractPrescription(string text)
    {
        var match = Regex.Match(text, @"Prescription:\s*(.+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : text;
    }

    private string ExtractUsage(string text)
    {
        var match = Regex.Match(text, @"Usage:\s*(.+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private string ExtractOrderingProvider(string text)
    {
        var patterns = new[]
        {
            @"Ordered by\s+(.+?)\.?$",
            @"(?:Ordering Physician|Dr\.)[:.]?\s*(.+?)\.?$",
            @"Provider:\s*(.+?)\.?$"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (match.Success)
            {
                var provider = match.Groups[1].Value.Trim().TrimEnd('.');
                if (!string.IsNullOrWhiteSpace(provider))
                {
                    return provider.StartsWith("Dr.") ? provider : $"Dr. {provider}";
                }
            }
        }
        
        return "Dr. Unknown";
    }

    private DateTime ExtractNoteDate(string text)
    {
        var patterns = new[]
        {
            @"Date:\s*(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})",
            @"(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})",
            @"Note Date:\s*(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var date))
            {
                return date;
            }
        }

        return DateTime.UtcNow;
    }

    /// <summary>
    /// Determines the DME device type based on keywords found in physician note text.
    /// 
    /// Adding New Device Types:
    /// 1. Add new device type and keywords to devicePatterns dictionary
    /// 2. Add corresponding case in ExtractDeviceSpecifications method
    /// 3. Create new Extract[DeviceType]Specifications method
    /// 4. Add unit tests for the new device type
    /// </summary>
    /// <param name="text">The raw physician note text</param>
    /// <returns>Device type name or "Unknown" if no matches found</returns>
    private string DetermineDeviceType(string text)
    {
        // Dictionary maps device types to their common keywords/aliases
        // Keywords are checked case-insensitively
        var devicePatterns = new Dictionary<string, string[]>
        {
            ["CPAP"] = ["cpap", "continuous positive airway pressure"],
            ["BiPAP"] = ["bipap", "bilevel", "bi-level"],
            ["Oxygen"] = ["oxygen", "o2", "oxygen tank", "oxygen concentrator"],
            ["Nebulizer"] = ["nebulizer", "breathing treatment", "albuterol"],
            ["Wheelchair"] = ["wheelchair", "mobility device"],
            ["Walker"] = ["walker", "walking aid"],
            ["Hospital Bed"] = ["hospital bed", "adjustable bed"]
        };

        // Check each device type's keywords against the note text
        foreach (var (deviceType, keywords) in devicePatterns)
        {
            if (keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                return deviceType;
            }
        }

        return "Unknown";
    }

    /// <summary>
    /// Extracts device-specific configuration and specifications from physician note text.
    /// 
    /// This method routes to device-specific extraction methods based on device type.
    /// When adding a new device type, add a corresponding case and extraction method.
    /// </summary>
    /// <param name="text">Raw physician note text</param>
    /// <param name="deviceType">The identified device type</param>
    /// <returns>Dictionary of device specifications and configuration details</returns>
    private Dictionary<string, object> ExtractDeviceSpecifications(string text, string deviceType)
    {
        var specifications = new Dictionary<string, object>();

        // Route to device-specific extraction methods
        // Each device type has its own parsing logic for relevant specifications
        switch (deviceType.ToUpperInvariant())
        {
            case "CPAP":
            case "BIPAP":
                ExtractCpapSpecifications(text, specifications);
                break;
            case "OXYGEN":
                ExtractOxygenSpecifications(text, specifications);
                break;
            case "NEBULIZER":
                ExtractNebulizerSpecifications(text, specifications);
                break;
            case "WHEELCHAIR":
                ExtractWheelchairSpecifications(text, specifications);
                break;
            case "WALKER":
                ExtractWalkerSpecifications(text, specifications);
                break;
            case "HOSPITAL BED":
                ExtractHospitalBedSpecifications(text, specifications);
                break;
            // To add a new device type:
            // case "NEW_DEVICE":
            //     ExtractNewDeviceSpecifications(text, specifications);
            //     break;
        }

        return specifications;
    }

    /// <summary>
    /// Extracts CPAP/BiPAP specific specifications from physician note text.
    /// Looks for mask type, pressure settings, add-ons, and sleep study qualifiers.
    /// </summary>
    private void ExtractCpapSpecifications(string text, Dictionary<string, object> specs)
    {
        // Mask type - determines interface between device and patient
        if (text.Contains("full face", StringComparison.OrdinalIgnoreCase))
            specs["MaskType"] = "full face";
        else if (text.Contains("nasal", StringComparison.OrdinalIgnoreCase))
            specs["MaskType"] = "nasal";

        // Pressure settings - therapeutic air pressure in cmH2O
        var pressureMatch = Regex.Match(text, @"(\d+(?:\.\d+)?)\s*(?:cmH2O|cm|pressure)", RegexOptions.IgnoreCase);
        if (pressureMatch.Success)
            specs["Pressure"] = $"{pressureMatch.Groups[1].Value} cmH2O";

        // Add-ons - additional comfort/therapy features
        var addOns = new List<string>();
        if (text.Contains("humidifier", StringComparison.OrdinalIgnoreCase))
            addOns.Add("humidifier");
        if (text.Contains("heated tube", StringComparison.OrdinalIgnoreCase))
            addOns.Add("heated tube");

        if (addOns.Any())
            specs["AddOns"] = addOns.ToArray();

        // AHI (Apnea-Hypopnea Index) qualifier - sleep study severity indicator
        if (text.Contains("AHI", StringComparison.OrdinalIgnoreCase))
        {
            var ahiMatch = Regex.Match(text, @"AHI\s*[>]\s*(\d+)", RegexOptions.IgnoreCase);
            if (ahiMatch.Success)
                specs["AHI"] = $">{ahiMatch.Groups[1].Value}";
        }
    }

    private void ExtractOxygenSpecifications(string text, Dictionary<string, object> specs)
    {
        // Flow rate - look for various formats
        var flowPatterns = new[]
        {
            @"(\d+(?:\.\d+)?)\s*L(?:\s*per\s*minute|PM|/min|\s*LPM)",
            @"(\d+(?:\.\d+)?)\s*(?:liters?\s*per\s*minute|L)",
            @"delivering\s*(\d+(?:\.\d+)?)\s*L"
        };

        foreach (var pattern in flowPatterns)
        {
            var flowMatch = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (flowMatch.Success)
            {
                specs["FlowRate"] = $"{flowMatch.Groups[1].Value} L/min";
                break;
            }
        }

        // Delivery method
        if (text.Contains("cannula", StringComparison.OrdinalIgnoreCase))
            specs["DeliveryMethod"] = "nasal cannula";
        else if (text.Contains("mask", StringComparison.OrdinalIgnoreCase))
            specs["DeliveryMethod"] = "oxygen mask";
        else if (text.Contains("tank", StringComparison.OrdinalIgnoreCase))
            specs["DeliveryMethod"] = "oxygen tank";

        // Usage
        var usage = new List<string>();
        if (text.Contains("sleep", StringComparison.OrdinalIgnoreCase))
            usage.Add("sleep");
        if (text.Contains("exertion", StringComparison.OrdinalIgnoreCase))
            usage.Add("exertion");
        if (text.Contains("continuous", StringComparison.OrdinalIgnoreCase))
            usage.Add("continuous");

        if (usage.Any())
            specs["Usage"] = string.Join(" and ", usage);
    }

    private void ExtractNebulizerSpecifications(string text, Dictionary<string, object> specs)
    {
        // Medication
        if (text.Contains("albuterol", StringComparison.OrdinalIgnoreCase))
            specs["Medication"] = "albuterol";
        
        // Frequency
        var frequencyMatch = Regex.Match(text, @"(\d+)\s*times?\s*(?:per\s*)?day", RegexOptions.IgnoreCase);
        if (frequencyMatch.Success)
            specs["Frequency"] = $"{frequencyMatch.Groups[1].Value} times per day";
    }

    private void ExtractWheelchairSpecifications(string text, Dictionary<string, object> specs)
    {
        if (text.Contains("manual", StringComparison.OrdinalIgnoreCase))
            specs["Type"] = "manual";
        else if (text.Contains("electric", StringComparison.OrdinalIgnoreCase) || text.Contains("powered", StringComparison.OrdinalIgnoreCase))
            specs["Type"] = "electric";
        
        if (text.Contains("transport", StringComparison.OrdinalIgnoreCase))
            specs["Category"] = "transport";
    }

    private void ExtractWalkerSpecifications(string text, Dictionary<string, object> specs)
    {
        if (text.Contains("wheeled", StringComparison.OrdinalIgnoreCase) || text.Contains("rollator", StringComparison.OrdinalIgnoreCase))
            specs["Type"] = "wheeled";
        else if (text.Contains("standard", StringComparison.OrdinalIgnoreCase))
            specs["Type"] = "standard";
    }

    private void ExtractHospitalBedSpecifications(string text, Dictionary<string, object> specs)
    {
        if (text.Contains("electric", StringComparison.OrdinalIgnoreCase) || text.Contains("adjustable", StringComparison.OrdinalIgnoreCase))
            specs["Type"] = "electric adjustable";
        
        if (text.Contains("mattress", StringComparison.OrdinalIgnoreCase))
            specs["IncludesMattress"] = true;
    }
}
