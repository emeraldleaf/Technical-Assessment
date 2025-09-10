using SignalBooster.Core.Common;

namespace SignalBooster.Core.Domain.Errors;

public static class ValidationErrors
{
    public static Error InvalidDeviceType(string deviceType) =>
        Error.Validation("Validation.InvalidDeviceType", $"'{deviceType}' is not a valid device type.");

    public static Error MissingRequiredField(string fieldName) =>
        Error.Validation("Validation.MissingRequiredField", $"Required field '{fieldName}' is missing or empty.");

    public static Error InvalidFormat(string fieldName, string expectedFormat) =>
        Error.Validation("Validation.InvalidFormat", $"Field '{fieldName}' is not in the expected format: {expectedFormat}");

    public static Error InvalidRange(string fieldName, string actualValue, string expectedRange) =>
        Error.Validation("Validation.InvalidRange", 
            $"Field '{fieldName}' with value '{actualValue}' is not within the expected range: {expectedRange}");

    public static Error NoteParsingFailed(string reason) =>
        Error.Validation("Validation.NoteParsingFailed", $"Failed to parse physician note: {reason}");

    public static Error DeviceOrderValidationFailed(string reason) =>
        Error.Validation("Validation.DeviceOrderValidationFailed", $"Device order validation failed: {reason}");
}