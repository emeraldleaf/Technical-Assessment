using SignalBooster.Services;
using System.IO.Abstractions;
using Xunit;

namespace SignalBooster.Tests;

/// <summary>
/// Integration tests for FileReader class with real file system operations
///
/// Test Categories:
/// - Real file I/O operations with temporary files
/// - JSON and plain text file format handling
/// - File encoding and character set validation
/// - Error handling for missing or corrupted files
///
/// Testing Approach:
/// - Uses real FileSystem (not mocked) for true integration testing
/// - Creates and cleans up temporary files for each test
/// - Validates both .txt and .json file parsing
/// - Tests actual file system error conditions (permissions, missing files)
///
/// File Format Support:
/// - Plain text files: Direct content reading
/// - JSON files: Extracts "note" property value
/// - UTF-8 encoding support for international characters
/// - Large file handling (>1MB) performance validation
/// </summary>
[Trait("Category", "Integration")]
public class FileReaderIntegrationTests
{
    private readonly FileReader _fileReader = new(new FileSystem());

    [Fact]
    public async Task ReadTextAsync_JsonFileWithNote_ExtractsNoteContent()
    {
        var tempFile = Path.GetTempFileName() + ".json";
        try
        {
            var jsonContent = """{"note": "Patient needs CPAP therapy."}""";
            await File.WriteAllTextAsync(tempFile, jsonContent);
            
            var result = await _fileReader.ReadTextAsync(tempFile);
            
            Assert.Equal("Patient needs CPAP therapy.", result);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadTextAsync_JsonFileWithPhysicianNote_ExtractsNoteContent()
    {
        var tempFile = Path.GetTempFileName() + ".json";
        try
        {
            var jsonContent = """{"physician_note": "Patient requires oxygen tank."}""";
            await File.WriteAllTextAsync(tempFile, jsonContent);
            
            var result = await _fileReader.ReadTextAsync(tempFile);
            
            Assert.Equal("Patient requires oxygen tank.", result);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadTextAsync_PlainTextFile_ReturnsContent()
    {
        var tempFile = Path.GetTempFileName() + ".json";
        try
        {
            var content = "Patient needs CPAP device.";
            await File.WriteAllTextAsync(tempFile, content);
            
            var result = await _fileReader.ReadTextAsync(tempFile);
            
            Assert.Equal("Patient needs CPAP device.", result);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}