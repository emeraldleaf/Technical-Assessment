using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SignalBooster.Configuration;
using SignalBooster.Models;
using SignalBooster.Services;
using SignalBooster.Tests.TestHelpers;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace SignalBooster.Tests;

[Trait("Category", "Integration")]
public class ModernDeviceExtractionTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly ITextParser _textParser = Substitute.For<ITextParser>();
    private readonly IApiClient _apiClient = Substitute.For<IApiClient>();
    private readonly ILogger<DeviceExtractor> _logger = Substitute.For<ILogger<DeviceExtractor>>();
    private readonly DeviceExtractor _extractor;

    public ModernDeviceExtractionTests()
    {
        var fileReader = new FileReader(_fileSystem); // Inject mocked file system
        var options = Options.Create(new SignalBoosterOptions
        {
            OpenAI = new OpenAIOptions { ApiKey = "" }, // Force regex parsing for predictable tests
            Api = new ApiOptions { EnableApiPosting = false } // Disable API calls in tests
        });

        _extractor = new DeviceExtractor(fileReader, _textParser, _apiClient, options, _logger);
    }

    [Theory]
    [InlineData("physician_note1.txt")]
    [InlineData("physician_note2.txt")]
    [InlineData("test_note.txt")]
    public async Task ProcessNoteAsync_AssignmentRequirements_ExtractsCorrectData(string testCase)
    {
        // Arrange - Get test data without file I/O
        var (noteText, expectedOrder) = GetTestCaseData(testCase);
        
        // Setup in-memory file system
        _fileSystem.AddFile(testCase, new MockFileData(noteText));
        
        // Mock parser to return expected result
        _textParser.ParseDeviceOrder(noteText).Returns(expectedOrder);
        
        // Act - No actual file I/O, all in memory
        var result = await _extractor.ProcessNoteAsync(testCase);
        
        // Assert - Structured object comparison
        result.Should().BeEquivalentTo(expectedOrder, options => options
            .ExcludingMissingMembers());
            
        // Verify interactions
        _textParser.Received(1).ParseDeviceOrder(noteText);
        await _apiClient.Received(1).PostDeviceOrderAsync(Arg.Any<DeviceOrder>());
    }

    [Fact]
    public async Task ProcessNoteAsync_OxygenTankScenario_ExtractsAllFields()
    {
        // Arrange - Use test builder for clean setup
        var testData = TestDataFactory.PhysicianNotes.OxygenTank;
        
        _textParser.ParseDeviceOrder(testData.Text).Returns(testData.Expected);
        
        // Act
        var result = await ProcessNoteInMemory(testData.Text);
        
        // Assert - Specific field validation
        result.Should().Match<DeviceOrder>(order =>
            order.Device == "Oxygen Tank" &&
            order.Liters == "2 L" &&
            order.Usage == "sleep and exertion" &&
            order.PatientName == "Harold Finch" &&
            order.Dob == "04/12/1952" &&
            order.Diagnosis == "COPD" &&
            order.OrderingProvider == "Dr. Cuddy");
    }

    [Fact]
    public async Task ProcessNoteAsync_CpapWithAccessories_ExtractsComplexData()
    {
        // Arrange
        var testData = TestDataFactory.PhysicianNotes.CpapWithAccessories;
        _textParser.ParseDeviceOrder(testData.Text).Returns(testData.Expected);
        
        // Act
        var result = await ProcessNoteInMemory(testData.Text);
        
        // Assert - Complex object validation
        result.Should().Match<DeviceOrder>(order =>
            order.Device == "CPAP" &&
            order.MaskType == "full face" &&
            order.AddOns != null && order.AddOns.Contains("heated humidifier") &&
            order.Qualifier == "AHI > 20" &&
            order.Diagnosis == "Severe sleep apnea");
    }

    [Theory]
    [MemberData(nameof(GetAllDmeDeviceScenarios))]
    public async Task ProcessNoteAsync_VariousDmeDevices_HandlesAllTypes(
        string deviceType, string noteText, DeviceOrder expectedOrder)
    {
        // Arrange
        _textParser.ParseDeviceOrder(noteText).Returns(expectedOrder);
        
        // Act
        var result = await ProcessNoteInMemory(noteText);
        
        // Assert
        result.Device.Should().Be(deviceType);
        result.Should().BeEquivalentTo(expectedOrder, options => options
            .ExcludingMissingMembers());
    }

    [Fact]
    public async Task ProcessNoteAsync_JsonWrappedNote_ExtractsFromJsonContent()
    {
        // Arrange - Test JSON file handling without actual files
        var jsonContent = """{"physician_note": "Patient needs CPAP therapy for severe sleep apnea."}""";
        var expectedNote = "Patient needs CPAP therapy for severe sleep apnea.";
        var expectedOrder = new DeviceOrder { Device = "CPAP", Diagnosis = "severe sleep apnea" };
        
        _textParser.ParseDeviceOrder(expectedNote).Returns(expectedOrder);
        
        // Act
        var result = await ProcessJsonNoteInMemory(jsonContent);
        
        // Assert
        result.Device.Should().Be("CPAP");
        _textParser.Received(1).ParseDeviceOrder(expectedNote);
    }

    public static IEnumerable<object[]> GetAllDmeDeviceScenarios()
    {
        yield return new object[] 
        { 
            "Hospital Bed", 
            TestDataFactory.PhysicianNotes.HospitalBed.Text,
            TestDataFactory.PhysicianNotes.HospitalBed.Expected
        };
        
        yield return new object[]
        {
            "TENS Unit",
            TestDataFactory.PhysicianNotes.TensUnit.Text,
            TestDataFactory.PhysicianNotes.TensUnit.Expected
        };
        
        // Add more device scenarios as needed
    }

    private async Task<DeviceOrder> ProcessNoteInMemory(string noteText)
    {
        const string fileName = "test_note.txt";
        _fileSystem.AddFile(fileName, new MockFileData(noteText));
        return await _extractor.ProcessNoteAsync(fileName);
    }

    private async Task<DeviceOrder> ProcessJsonNoteInMemory(string jsonContent)
    {
        const string fileName = "test_note.json";
        _fileSystem.AddFile(fileName, new MockFileData(jsonContent));
        return await _extractor.ProcessNoteAsync(fileName);
    }

    private static (string Text, DeviceOrder Expected) GetTestCaseData(string testCase) => testCase switch
    {
        "physician_note1.txt" => TestDataFactory.PhysicianNotes.OxygenTank,
        "physician_note2.txt" => TestDataFactory.PhysicianNotes.CpapWithAccessories, 
        "test_note.txt" => TestDataFactory.PhysicianNotes.SimpleCpap,
        _ => throw new ArgumentException($"Unknown test case: {testCase}")
    };
}