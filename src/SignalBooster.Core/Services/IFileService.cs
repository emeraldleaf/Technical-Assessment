using SignalBooster.Core.Common;

namespace SignalBooster.Core.Services;

public interface IFileService
{
    Task<Result<string>> ReadNoteFromFileAsync(string filePath);
    Task<Result<string>> WriteOutputAsync(string content, string? fileName = null);
    Result<bool> ValidateFileExtension(string filePath);
}