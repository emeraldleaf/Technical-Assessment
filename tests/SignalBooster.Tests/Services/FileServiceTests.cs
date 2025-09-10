using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SignalBooster.Core.Configuration;
using SignalBooster.Core.Services;

namespace SignalBooster.Tests.Services;

public class FileServiceTests : IDisposable
{
    private readonly ILogger<FileService> _logger;
    private readonly IOptions<SignalBoosterOptions> _options;
    private readonly FileService _fileService;
    private readonly string _testDirectory;
    private readonly string _outputDirectory;

    public FileServiceTests()
    {
        _logger = Substitute.For<ILogger<FileService>>();
        
        // Setup test directories
        _testDirectory = Path.Combine(Path.GetTempPath(), "SignalBoosterTests", Guid.NewGuid().ToString());
        _outputDirectory = Path.Combine(_testDirectory, "output");
        
        Directory.CreateDirectory(_testDirectory);
        Directory.CreateDirectory(_outputDirectory);

        var signalBoosterOptions = new SignalBoosterOptions
        {
            Files = new SignalBooster.Core.Configuration.FileOptions
            {
                DefaultInputPath = "physician_note1.txt",
                OutputDirectory = _outputDirectory,
                CreateOutputDirectory = true,
                SupportedExtensions = [".txt", ".json"]
            }
        };
        
        _options = Substitute.For<IOptions<SignalBoosterOptions>>();
        _options.Value.Returns(signalBoosterOptions);
        
        _fileService = new FileService(_logger, _options);
    }

    [Fact]
    public async Task ReadNoteFromFileAsync_WithValidFile_ShouldReturnContent()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        var expectedContent = "Patient needs CPAP therapy. Ordered by Dr. Smith.";
        await File.WriteAllTextAsync(testFile, expectedContent);

        // Act
        var result = await _fileService.ReadNoteFromFileAsync(testFile);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedContent);
    }

    [Fact]
    public async Task ReadNoteFromFileAsync_WithNonExistentFile_ShouldReturnNotFoundError()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act
        var result = await _fileService.ReadNoteFromFileAsync(nonExistentFile);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("File.NotFound");
        result.FirstError.Description.Should().Contain(nonExistentFile);
    }

    [Fact]
    public async Task ReadNoteFromFileAsync_WithEmptyFile_ShouldReturnEmptyError()
    {
        // Arrange
        var emptyFile = Path.Combine(_testDirectory, "empty.txt");
        await File.WriteAllTextAsync(emptyFile, "");

        // Act
        var result = await _fileService.ReadNoteFromFileAsync(emptyFile);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("File.Empty");
    }

    [Fact]
    public async Task ReadNoteFromFileAsync_WithWhitespaceOnlyFile_ShouldReturnEmptyError()
    {
        // Arrange
        var whitespaceFile = Path.Combine(_testDirectory, "whitespace.txt");
        await File.WriteAllTextAsync(whitespaceFile, "   \n\t  ");

        // Act
        var result = await _fileService.ReadNoteFromFileAsync(whitespaceFile);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("File.Empty");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ReadNoteFromFileAsync_WithInvalidFilePath_ShouldReturnValidationError(string? filePath)
    {
        // Act
        var result = await _fileService.ReadNoteFromFileAsync(filePath!);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Validation.MissingRequiredField");
    }

    [Fact]
    public async Task ReadNoteFromFileAsync_WithUnsupportedExtension_ShouldReturnUnsupportedExtensionError()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.pdf");

        // Act
        var result = await _fileService.ReadNoteFromFileAsync(testFile);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("File.UnsupportedExtension");
        result.FirstError.Description.Should().Contain(".pdf");
    }

    [Fact]
    public async Task WriteOutputAsync_WithValidContent_ShouldCreateFile()
    {
        // Arrange
        var content = "Test output content";

        // Act
        var result = await _fileService.WriteOutputAsync(content);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        
        var filePath = result.Value!;
        File.Exists(filePath).Should().BeTrue();
        
        var writtenContent = await File.ReadAllTextAsync(filePath);
        writtenContent.Should().Be(content);
    }

    [Fact]
    public async Task WriteOutputAsync_WithCustomFileName_ShouldUseCustomName()
    {
        // Arrange
        var content = "Test content";
        var customFileName = "custom_output.json";

        // Act
        var result = await _fileService.WriteOutputAsync(content, customFileName);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().EndWith(customFileName);
        File.Exists(result.Value!).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task WriteOutputAsync_WithInvalidContent_ShouldReturnValidationError(string? content)
    {
        // Act
        var result = await _fileService.WriteOutputAsync(content!);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Validation.MissingRequiredField");
    }

    [Theory]
    [InlineData("test.txt", true)]
    [InlineData("test.json", true)]
    [InlineData("test.TXT", true)]
    [InlineData("test.JSON", true)]
    [InlineData("test.pdf", false)]
    [InlineData("test.doc", false)]
    [InlineData("test", false)]
    public void ValidateFileExtension_WithVariousExtensions_ShouldReturnExpectedResult(string fileName, bool expectedValid)
    {
        // Act
        var result = _fileService.ValidateFileExtension(fileName);

        // Assert
        result.IsSuccess.Should().Be(expectedValid);
        if (!expectedValid)
        {
            result.IsError.Should().BeTrue();
            result.FirstError.Code.Should().Be("File.UnsupportedExtension");
        }
    }

    [Fact]
    public async Task ReadNoteFromFileAsync_WithFileContainingLeadingAndTrailingWhitespace_ShouldTrimContent()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "whitespace_test.txt");
        var originalContent = "   Patient needs CPAP therapy. Ordered by Dr. Smith.   \n\t";
        var expectedContent = "Patient needs CPAP therapy. Ordered by Dr. Smith.";
        await File.WriteAllTextAsync(testFile, originalContent);

        // Act
        var result = await _fileService.ReadNoteFromFileAsync(testFile);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedContent);
    }

    [Fact]
    public async Task WriteOutputAsync_WhenOutputDirectoryDoesNotExist_ShouldCreateDirectoryAndFile()
    {
        // Arrange
        var newOutputDir = Path.Combine(_testDirectory, "new_output");
        var options = new SignalBoosterOptions
        {
            Files = new SignalBooster.Core.Configuration.FileOptions
            {
                OutputDirectory = newOutputDir,
                CreateOutputDirectory = true,
                SupportedExtensions = [".txt", ".json"]
            }
        };
        _options.Value.Returns(options);
        
        var fileService = new FileService(_logger, _options);
        var content = "Test content for new directory";

        // Ensure directory doesn't exist
        Directory.Exists(newOutputDir).Should().BeFalse();

        // Act
        var result = await fileService.WriteOutputAsync(content);

        // Assert
        result.IsSuccess.Should().BeTrue();
        Directory.Exists(newOutputDir).Should().BeTrue();
        File.Exists(result.Value!).Should().BeTrue();
    }

    [Fact]
    public async Task ReadNoteFromFileAsync_WithLargeFile_ShouldReadSuccessfully()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "large_file.txt");
        var largeContent = string.Join("\n", Enumerable.Repeat("Patient needs medical equipment.", 1000));
        await File.WriteAllTextAsync(testFile, largeContent);

        // Act
        var result = await _fileService.ReadNoteFromFileAsync(testFile);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveLength(largeContent.Length);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
        GC.SuppressFinalize(this);
    }
}