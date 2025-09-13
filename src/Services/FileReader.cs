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

    /// <summary>
    /// Reads text content from a file, supporting both plain text and JSON-wrapped physician notes.
    /// </summary>
    /// <param name="filePath">Path to the file to read</param>
    /// <returns>The extracted text content</returns>
    /// <remarks>
    /// For medical notes, streaming isn't beneficial since:
    /// - Notes are typically small (500 bytes - 50KB)
    /// - Text parsing requires complete content for context
    /// - LLM/regex extraction needs full document
    /// 
    /// Streaming would be useful for:
    /// - Log file processing: ReadLineAsync() for line-by-line processing
    /// - Large dataset imports: JsonSerializer.DeserializeAsyncEnumerable()
    /// - Real-time data feeds: Stream processing with yield return
    /// 
    /// Example streaming implementation:
    /// public async IAsyncEnumerable&lt;string&gt; ReadLinesAsync(string filePath)
    /// {
    ///     using var reader = new StreamReader(_fileSystem.File.OpenRead(filePath));
    ///     string? line;
    ///     while ((line = await reader.ReadLineAsync()) != null)
    ///         yield return line;
    /// }
    /// </remarks>
    public async Task<string> ReadTextAsync(string filePath)
    {
        if (!_fileSystem.File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");
        
        using var stream = _fileSystem.File.OpenRead(filePath);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        var content = await reader.ReadToEndAsync();
        
        if (filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractNoteFromJson(content);
        }
        
        return content;
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