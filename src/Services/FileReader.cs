using System.IO.Abstractions;
using System.Text.Json;

namespace SignalBooster.Services;

public class FileReader : IFileReader
{
    private readonly IFileSystem _fileSystem;

    public FileReader(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public async Task<string> ReadTextAsync(string filePath)
    {
        if (!_fileSystem.File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");
        
        var fileInfo = _fileSystem.FileInfo.New(filePath);
        
        // Use StreamReader for large files (>1MB) for better memory efficiency
        if (fileInfo.Length > 1_048_576) // 1MB threshold
        {
            using var stream = _fileSystem.File.OpenRead(filePath);
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true, bufferSize: 4096);
            var content = await reader.ReadToEndAsync();
            
            if (filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return ExtractNoteFromJson(content);
            }
            
            return content;
        }
        else
        {
            // Use ReadAllTextAsync for smaller files
            var content = await _fileSystem.File.ReadAllTextAsync(filePath);
            
            if (filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return ExtractNoteFromJson(content);
            }
            
            return content;
        }
    }
    
    private static string ExtractNoteFromJson(string jsonContent)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;
            
            // Try common JSON wrapper properties
            if (root.TryGetProperty("note", out var noteProperty))
                return noteProperty.GetString() ?? jsonContent;
            
            if (root.TryGetProperty("physician_note", out var physicianNoteProperty))
                return physicianNoteProperty.GetString() ?? jsonContent;
            
            if (root.TryGetProperty("text", out var textProperty))
                return textProperty.GetString() ?? jsonContent;
            
            if (root.TryGetProperty("content", out var contentProperty))
                return contentProperty.GetString() ?? jsonContent;
            
            return jsonContent;
        }
        catch (JsonException)
        {
            return jsonContent;
        }
    }
}