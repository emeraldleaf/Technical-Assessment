using System.Text.Json;

namespace SignalBooster.Mvp.Services;

public class FileReader : IFileReader
{
    public async Task<string> ReadTextAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");
        
        var content = await File.ReadAllTextAsync(filePath);
        
        // Support JSON-wrapped notes
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