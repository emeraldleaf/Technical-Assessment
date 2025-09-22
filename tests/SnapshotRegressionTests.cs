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

/// <summary>
/// Snapshot regression tests using Verify framework for change detection
///
/// Test Categories:
/// - Baseline snapshot creation for parsing results
/// - Regression detection when parsing logic changes
/// - Output format stability verification
/// - Integration test validation with real parser components
///
/// Testing Strategy:
/// - Verify framework captures and compares parsing outputs
/// - Snapshots stored as .received.txt files for review
/// - Automatic baseline creation on first test run
/// - Fail-fast detection of unintended parsing changes
///
/// Workflow:
/// - First run: Creates baseline snapshots for approval
/// - Subsequent runs: Compares against approved baselines
/// - Changes require manual review and approval of new snapshots
/// - Prevents accidental regression in parsing accuracy
/// </summary>
[UsesVerify]
[Trait("Category", "Regression")]
public class SnapshotRegressionTests
{
    private readonly IFileReader _fileReader = Substitute.For<IFileReader>();
    private readonly TextParser _realParser; // Use real parser for integration testing
    private readonly TextParser? _aiParser; // AI-enabled parser for comparison testing
    private readonly IAgenticExtractor _agenticExtractor = Substitute.For<IAgenticExtractor>();
    private readonly IApiClient _apiClient = Substitute.For<IApiClient>();
    private readonly ILogger<DeviceExtractor> _logger = Substitute.For<ILogger<DeviceExtractor>>();
    private readonly DeviceExtractor _extractor;
    private readonly DeviceExtractor? _aiExtractor;
    private readonly bool _hasApiKey;

    public SnapshotRegressionTests()
    {
        // Regex-only parser for baseline testing
        var regexOptions = Options.Create(new SignalBoosterOptions
        {
            OpenAI = new OpenAIOptions { ApiKey = "" }, // Force regex parsing for consistency
            Api = new ApiOptions { EnableApiPosting = false }
        });

        _realParser = new TextParser(regexOptions, Substitute.For<ILogger<TextParser>>());
        _apiClient.PostDeviceOrderAsync(Arg.Any<DeviceOrder>()).Returns(Task.CompletedTask);
        _extractor = new DeviceExtractor(_fileReader, _realParser, _agenticExtractor, _apiClient, regexOptions, _logger);

        // AI-enabled parser for comparison testing
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        _hasApiKey = !string.IsNullOrEmpty(apiKey);

        if (_hasApiKey)
        {
            var aiOptions = Options.Create(new SignalBoosterOptions
            {
                OpenAI = new OpenAIOptions
                {
                    ApiKey = apiKey!,
                    Model = "gpt-3.5-turbo",
                    MaxTokens = 500,
                    Temperature = 0.1f // Low temperature for consistent snapshots
                },
                Api = new ApiOptions { EnableApiPosting = false }
            });

            _aiParser = new TextParser(aiOptions, Substitute.For<ILogger<TextParser>>());
            _aiExtractor = new DeviceExtractor(_fileReader, _aiParser, _agenticExtractor, _apiClient, aiOptions, _logger);
        }
    }

    [Fact]
    public Task ProcessNote_OxygenTankScenario_MatchesSnapshot()
    {
        // Arrange
        var testData = TestDataFactory.PhysicianNotes.OxygenTank;
        _fileReader.ReadTextAsync("oxygen_test.txt").Returns(Task.FromResult(testData.Text));

        // Act & Assert - Snapshot testing automatically detects changes
        return Verify(_extractor.ProcessNoteAsync("oxygen_test.txt"))
            .UseParameters("OxygenTank");
    }

    [Fact]
    public Task ProcessNote_CpapWithAccessories_MatchesSnapshot()
    {
        // Arrange
        var testData = TestDataFactory.PhysicianNotes.CpapWithAccessories;
        _fileReader.ReadTextAsync("cpap_test.txt").Returns(Task.FromResult(testData.Text));

        // Act & Assert
        return Verify(_extractor.ProcessNoteAsync("cpap_test.txt"))
            .UseParameters("CpapAccessories");
    }

    [Fact]
    public Task ProcessNote_HospitalBedScenario_MatchesSnapshot()
    {
        // Arrange
        var testData = TestDataFactory.PhysicianNotes.HospitalBed;
        _fileReader.ReadTextAsync("bed_test.txt").Returns(Task.FromResult(testData.Text));

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

        _fileReader.ReadTextAsync("test.txt").Returns(Task.FromResult(noteText));

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
            _fileReader.ReadTextAsync(fileName).Returns(Task.FromResult(noteText));
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

    #region AI Snapshot Tests

    [Fact]
    public async Task ProcessNote_AIvsRegex_CPAPScenario_ComparisonSnapshot()
    {
        if (!_hasApiKey)
        {
            // Create empty snapshot to maintain test structure
            await Verify(new { Message = "AI tests skipped - no API key configured" })
                .UseParameters("NoApiKey");
            return;
        }

        // Arrange
        var testData = TestDataFactory.PhysicianNotes.CpapWithAccessories;
        _fileReader.ReadTextAsync("cpap_test.txt").Returns(Task.FromResult(testData.Text));

        // Act - Run both parsers
        var regexResult = await _extractor.ProcessNoteAsync("cpap_test.txt");
        var aiResult = await _aiExtractor!.ProcessNoteAsync("cpap_test.txt");

        // Assert - Snapshot comparison
        await Verify(new {
                RegexResult = regexResult,
                AIResult = aiResult,
                ComparisonMetrics = new
                {
                    DeviceMatch = regexResult.Device == aiResult.Device,
                    PatientMatch = regexResult.PatientName == aiResult.PatientName,
                    ProviderMatch = regexResult.OrderingProvider == aiResult.OrderingProvider,
                    AIEnhancement = new
                    {
                        MoreDetailedDiagnosis = (aiResult.Diagnosis?.Length ?? 0) > (regexResult.Diagnosis?.Length ?? 0),
                        ExtractedMaskType = !string.IsNullOrEmpty(aiResult.MaskType),
                        ExtractedAddOns = aiResult.AddOns?.Length > 0
                    }
                }
            })
            .UseParameters("CPAP");
    }

    [Fact]
    public async Task ProcessNote_AIvsRegex_OxygenTankScenario_ComparisonSnapshot()
    {
        if (!_hasApiKey)
        {
            await Verify(new { Message = "AI tests skipped - no API key configured" })
                .UseParameters("NoApiKey");
            return;
        }

        // Arrange
        var testData = TestDataFactory.PhysicianNotes.OxygenTank;
        _fileReader.ReadTextAsync("oxygen_test.txt").Returns(Task.FromResult(testData.Text));

        // Act
        var regexResult = await _extractor.ProcessNoteAsync("oxygen_test.txt");
        var aiResult = await _aiExtractor!.ProcessNoteAsync("oxygen_test.txt");

        // Assert
        await Verify(new {
                RegexResult = regexResult,
                AIResult = aiResult,
                AIEnhancements = new
                {
                    ExtractedLiters = !string.IsNullOrEmpty(aiResult.Liters),
                    ExtractedUsage = !string.IsNullOrEmpty(aiResult.Usage),
                    DetailedQualifier = !string.IsNullOrEmpty(aiResult.Qualifier)
                }
            })
            .UseParameters("OxygenTank");
    }

    [Fact]
    public async Task ProcessNote_AI_RegressionSuite_AccuracySnapshot()
    {
        if (!_hasApiKey)
        {
            await Verify(new { Message = "AI tests skipped - no API key configured" })
                .UseParameters("NoApiKey");
            return;
        }

        // Arrange - Test multiple scenarios for AI consistency
        var testCases = new[]
        {
            ("cpap.txt", TestDataFactory.PhysicianNotes.CpapWithAccessories.Text),
            ("oxygen.txt", TestDataFactory.PhysicianNotes.OxygenTank.Text),
            ("bed.txt", TestDataFactory.PhysicianNotes.HospitalBed.Text)
        };

        var results = new List<object>();

        // Act - Process all test cases with AI
        foreach (var (fileName, noteText) in testCases)
        {
            _fileReader.ReadTextAsync(fileName).Returns(Task.FromResult(noteText));
            var aiResult = await _aiExtractor!.ProcessNoteAsync(fileName);

            results.Add(new
            {
                TestCase = fileName,
                AIExtraction = aiResult,
                QualityMetrics = new
                {
                    ExtractedFieldCount = CountNonNullFields(aiResult),
                    HasSpecificDetails = new
                    {
                        MaskType = !string.IsNullOrEmpty(aiResult.MaskType),
                        Liters = !string.IsNullOrEmpty(aiResult.Liters),
                        Usage = !string.IsNullOrEmpty(aiResult.Usage),
                        AddOns = aiResult.AddOns?.Any() == true,
                        Qualifier = !string.IsNullOrEmpty(aiResult.Qualifier)
                    }
                }
            });
        }

        // Assert - Full AI behavior snapshot
        await Verify(results);
    }

    [Theory]
    [InlineData("CPAP")]
    [InlineData("Oxygen Tank")]
    [InlineData("Hospital Bed")]
    public async Task ProcessNote_AI_DeviceSpecific_ConsistencySnapshot(string deviceType)
    {
        if (!_hasApiKey)
        {
            await Verify(new { Message = "AI tests skipped - no API key configured" })
                .UseParameters($"{deviceType.Replace(" ", "")}_NoApiKey");
            return;
        }

        // Arrange - Generate multiple variations of the same device type
        var variations = new[]
        {
            PhysicianNoteBuilder.Create().WithDevice(deviceType).WithProvider("Dr. Test1").BuildNoteText(),
            PhysicianNoteBuilder.Create().WithDevice(deviceType).WithProvider("Dr. Test2").WithDiagnosis("Updated condition").BuildNoteText(),
            PhysicianNoteBuilder.Create().WithDevice(deviceType).WithProvider("Dr. Test3").WithQualifier("Complex medical scenario").BuildNoteText()
        };

        var results = new List<object>();

        // Act - Test AI consistency across variations
        foreach (var (variation, index) in variations.Select((v, i) => (v, i)))
        {
            _fileReader.ReadTextAsync($"test_{index}.txt").Returns(Task.FromResult(variation));
            var result = await _aiExtractor!.ProcessNoteAsync($"test_{index}.txt");

            results.Add(new
            {
                VariationIndex = index,
                ExtractedDevice = result.Device,
                ConsistencyMarkers = new
                {
                    DeviceCorrect = result.Device.Contains(deviceType, StringComparison.OrdinalIgnoreCase),
                    HasPatientInfo = !string.IsNullOrEmpty(result.PatientName),
                    HasProvider = !string.IsNullOrEmpty(result.OrderingProvider)
                }
            });
        }

        // Assert - Device-specific AI consistency
        await Verify(results)
            .UseParameters(deviceType.Replace(" ", ""));
    }

    #endregion
}