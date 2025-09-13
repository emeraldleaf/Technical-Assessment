using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SignalBooster.Configuration;
using SignalBooster.Models;
using SignalBooster.Services;
using SignalBooster.Tests.TestHelpers;
using Xunit;

namespace SignalBooster.Tests;

[UsesVerify]
[Trait("Category", "Regression")]
public class SnapshotRegressionTests
{
    private readonly IFileReader _fileReader = Substitute.For<IFileReader>();
    private readonly TextParser _realParser; // Use real parser for integration testing
    private readonly IApiClient _apiClient = Substitute.For<IApiClient>();
    private readonly ILogger<DeviceExtractor> _logger = Substitute.For<ILogger<DeviceExtractor>>();
    private readonly DeviceExtractor _extractor;

    public SnapshotRegressionTests()
    {
        var options = Options.Create(new SignalBoosterOptions
        {
            OpenAI = new OpenAIOptions { ApiKey = "" }, // Force regex parsing for consistency
            Api = new ApiOptions { EnableApiPosting = false }
        });

        _realParser = new TextParser(options, Substitute.For<ILogger<TextParser>>());
        _extractor = new DeviceExtractor(_fileReader, _realParser, _apiClient, options, _logger);
    }

    [Fact]
    public Task ProcessNote_OxygenTankScenario_MatchesSnapshot()
    {
        // Arrange
        var testData = TestDataFactory.PhysicianNotes.OxygenTank;
        _fileReader.ReadTextAsync("oxygen_test.txt").Returns(testData.Text);

        // Act & Assert - Snapshot testing automatically detects changes
        return Verify(_extractor.ProcessNoteAsync("oxygen_test.txt"))
            .UseParameters("OxygenTank");
    }

    [Fact]
    public Task ProcessNote_CpapWithAccessories_MatchesSnapshot()
    {
        // Arrange
        var testData = TestDataFactory.PhysicianNotes.CpapWithAccessories;
        _fileReader.ReadTextAsync("cpap_test.txt").Returns(testData.Text);

        // Act & Assert
        return Verify(_extractor.ProcessNoteAsync("cpap_test.txt"))
            .UseParameters("CpapAccessories");
    }

    [Fact]
    public Task ProcessNote_HospitalBedScenario_MatchesSnapshot()
    {
        // Arrange
        var testData = TestDataFactory.PhysicianNotes.HospitalBed;
        _fileReader.ReadTextAsync("bed_test.txt").Returns(testData.Text);

        // Act & Assert
        return Verify(_extractor.ProcessNoteAsync("bed_test.txt"))
            .UseParameters("HospitalBed");
    }

    [Theory]
    [InlineData("CPAP", "sleep apnea")]
    [InlineData("Oxygen Tank", "COPD")]
    [InlineData("TENS Unit", "chronic pain")]
    [InlineData("Hospital Bed", "mobility issues")]
    public Task ProcessNote_VariousDeviceTypes_GenerateConsistentOutput(string deviceType, string diagnosis)
    {
        // Arrange - Generate test data programmatically
        var noteText = PhysicianNoteBuilder.Create()
            .WithDevice(deviceType)
            .WithDiagnosis(diagnosis)
            .WithProvider($"Dr. Test{deviceType.Replace(" ", "")}")
            .BuildNoteText();

        _fileReader.ReadTextAsync("test.txt").Returns(noteText);

        // Act & Assert - Snapshot per device type
        return Verify(_extractor.ProcessNoteAsync("test.txt"))
            .UseParameters(deviceType.Replace(" ", ""));
    }

    [Fact]
    public async Task RealWorldIntegration_AllAssignmentFiles_ProduceExpectedStructure()
    {
        // Arrange - Test all assignment requirements together
        var testCases = new[]
        {
            ("physician_note1.txt", TestDataFactory.PhysicianNotes.OxygenTank.Text),
            ("physician_note2.txt", TestDataFactory.PhysicianNotes.CpapWithAccessories.Text),
            ("test_note.txt", TestDataFactory.PhysicianNotes.SimpleCpap.Text)
        };

        var results = new List<object>();

        // Act - Process all test cases
        foreach (var (fileName, noteText) in testCases)
        {
            _fileReader.ReadTextAsync(fileName).Returns(noteText);
            var result = await _extractor.ProcessNoteAsync(fileName);
            
            results.Add(new 
            { 
                FileName = fileName,
                ExtractedData = result,
                ProcessingMetadata = new
                {
                    HasDevice = !string.IsNullOrEmpty(result.Device),
                    HasPatientInfo = !string.IsNullOrEmpty(result.PatientName),
                    HasProvider = !string.IsNullOrEmpty(result.OrderingProvider),
                    FieldCount = CountNonNullFields(result)
                }
            });
        }

        // Assert - Snapshot entire result set for regression detection
        await Verify(results);
    }

    private static int CountNonNullFields(DeviceOrder order)
    {
        var properties = typeof(DeviceOrder).GetProperties();
        return properties.Count(prop => 
        {
            var value = prop.GetValue(order);
            return value switch
            {
                string s => !string.IsNullOrEmpty(s),
                Array arr => arr.Length > 0,
                _ => value != null
            };
        });
    }
}