using SignalBooster.Core.Common;

namespace SignalBooster.Core.Domain.Errors;

public static class FileErrors
{
    public static Error NotFound(string filePath) =>
        Error.NotFound("File.NotFound", $"The file '{filePath}' was not found.");

    public static Error AccessDenied(string filePath) =>
        Error.Failure("File.AccessDenied", $"Access denied when trying to read file '{filePath}'.");

    public static Error InvalidFormat(string filePath, string expectedFormat) =>
        Error.Validation("File.InvalidFormat", $"The file '{filePath}' is not in the expected format: {expectedFormat}.");

    public static Error Empty(string filePath) =>
        Error.Validation("File.Empty", $"The file '{filePath}' is empty or contains no valid content.");

    public static Error UnsupportedExtension(string extension, string[] supportedExtensions) =>
        Error.Validation("File.UnsupportedExtension", 
            $"File extension '{extension}' is not supported. Supported extensions: {string.Join(", ", supportedExtensions)}.");

    public static Error ReadError(string filePath, string errorMessage) =>
        Error.Failure("File.ReadError", $"Failed to read file '{filePath}': {errorMessage}.");
}