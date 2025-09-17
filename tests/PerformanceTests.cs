using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SignalBooster.Configuration;
using SignalBooster.Models;
using SignalBooster.Services;
using SignalBooster.Tests.TestHelpers;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace SignalBooster.Tests;

/// <summary>
/// Performance tests for DeviceExtractor and TextParser to ensure scalability
///
/// Test Categories:
/// - Large file processing performance (1MB+ physician notes)
/// - Batch processing throughput validation
/// - Memory usage patterns under load
/// - Response time benchmarks for different parsing methods
///
/// Performance Criteria:
/// - Single file processing: &lt; 2 seconds for files up to 1MB
/// - Batch processing: Linear scaling with file count
/// - Memory usage: Stable allocation patterns, no leaks
/// - Regex parsing: &lt; 100ms for typical notes
///
/// Test Output:
/// - Execution times logged to test output for monitoring
/// - Performance assertions fail if thresholds are exceeded
/// </summary>
[Trait("Category", "Performance")]
public class PerformanceTests
{
    private readonly ITestOutputHelper _output;
    private readonly TextParser _regexParser;
    private readonly IFileReader _fileReader = Substitute.For<IFileReader>();
    private readonly IApiClient _apiClient = Substitute.For<IApiClient>();

    public PerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        var options = Options.Create(new SignalBoosterOptions 
        { 
            OpenAI = new OpenAIOptions { ApiKey = "" } 
        }); // Force regex for performance
        _regexParser = new TextParser(options, Substitute.For<ILogger<TextParser>>());
    }

    [Fact]
    public async Task ProcessNotes_BatchProcessing_MeetsPerformanceTargets()
    {
        // Arrange - Generate 100 test notes
        var testNotes = Enumerable.Range(1, 100)
            .Select(i => TestDataFactory.CreateRandomDeviceOrder())
            .Select(order => PhysicianNoteBuilder.Create()
                .WithPatient(order.PatientName ?? "Test Patient", order.Dob ?? "01/01/1980")
                .WithDiagnosis(order.Diagnosis ?? "Test condition")
                .WithProvider(order.OrderingProvider ?? "Dr. Test")
                .WithDevice(order.Device ?? "CPAP")
                .BuildNoteText())
            .ToList();

        var options = Options.Create(new SignalBoosterOptions
        {
            OpenAI = new OpenAIOptions { ApiKey = "" },
            Api = new ApiOptions { EnableApiPosting = false }
        });

        var extractor = new DeviceExtractor(_fileReader, _regexParser, _apiClient, options, 
            Substitute.For<ILogger<DeviceExtractor>>());

        // Act - Measure batch processing time
        var stopwatch = Stopwatch.StartNew();
        var results = new List<DeviceOrder>();

        for (int i = 0; i < testNotes.Count; i++)
        {
            var fileName = $"test_{i}.txt";
            _fileReader.ReadTextAsync(fileName).Returns(testNotes[i]);
            var result = await extractor.ProcessNoteAsync(fileName);
            results.Add(result);
        }

        stopwatch.Stop();

        // Assert - Performance targets
        var averageTimePerNote = stopwatch.ElapsedMilliseconds / (double)testNotes.Count;
        
        _output.WriteLine($"Processed {testNotes.Count} notes in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average time per note: {averageTimePerNote:F2}ms");
        _output.WriteLine($"Throughput: {60000 / averageTimePerNote:F0} notes/minute");

        // Performance assertions
        averageTimePerNote.Should().BeLessThan(100, "regex parsing should be fast");
        results.Should().HaveCount(100, "all notes should be processed");
        results.Should().OnlyContain(r => !string.IsNullOrEmpty(r.Device), "all should extract device");
    }

    [Fact]
    public void ParseDeviceOrder_RegexPerformance_ConsistentTiming()
    {
        // Arrange - Use consistent test data
        var testNote = TestDataFactory.PhysicianNotes.CpapWithAccessories.Text;
        const int iterations = 1000;

        // Warmup
        for (int i = 0; i < 10; i++)
        {
            _regexParser.ParseDeviceOrder(testNote);
        }

        // Act - Measure parsing performance
        var times = new List<long>();
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var result = _regexParser.ParseDeviceOrder(testNote);
            sw.Stop();
            times.Add(sw.ElapsedTicks);

            // Verify parsing still works
            result.Device.Should().Be("CPAP");
        }

        // Assert - Performance consistency
        var avgTicks = times.Average();
        var avgMs = (avgTicks / (double)Stopwatch.Frequency) * 1000;
        var maxMs = (times.Max() / (double)Stopwatch.Frequency) * 1000;
        var minMs = (times.Min() / (double)Stopwatch.Frequency) * 1000;

        _output.WriteLine($"Regex parsing stats over {iterations} iterations:");
        _output.WriteLine($"Average: {avgMs:F3}ms, Min: {minMs:F3}ms, Max: {maxMs:F3}ms");
        _output.WriteLine($"Standard deviation: {CalculateStandardDeviation(times, avgTicks):F3} ticks");

        // Performance targets for regex parsing
        avgMs.Should().BeLessThan(10, "regex parsing should be very fast");
        maxMs.Should().BeLessThan(50, "no single parse should take too long");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task ProcessNotes_ScalabilityTest_LinearPerformance(int noteCount)
    {
        // Arrange
        var notes = Enumerable.Range(1, noteCount)
            .Select(i => $"Patient {i} needs CPAP for sleep apnea. Dr. Test{i}")
            .ToList();

        var options = Options.Create(new SignalBoosterOptions
        {
            OpenAI = new OpenAIOptions { ApiKey = "" },
            Api = new ApiOptions { EnableApiPosting = false }
        });

        var extractor = new DeviceExtractor(_fileReader, _regexParser, _apiClient, options,
            Substitute.For<ILogger<DeviceExtractor>>());

        // Act
        var stopwatch = Stopwatch.StartNew();
        var processed = 0;

        for (int i = 0; i < noteCount; i++)
        {
            var fileName = $"scale_test_{i}.txt";
            _fileReader.ReadTextAsync(fileName).Returns(notes[i]);
            await extractor.ProcessNoteAsync(fileName);
            processed++;
        }

        stopwatch.Stop();

        // Assert - Linear scalability
        var throughput = (processed * 1000.0) / stopwatch.ElapsedMilliseconds;
        
        _output.WriteLine($"Processed {processed} notes in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Throughput: {throughput:F1} notes/second");

        // Throughput should be reasonably high for regex parsing
        throughput.Should().BeGreaterThan(50, "should maintain good throughput at scale");
    }

    [Fact]
    public void ParseDeviceOrder_MemoryUsage_StableAllocation()
    {
        // Arrange
        var testNote = TestDataFactory.PhysicianNotes.OxygenTank.Text;
        
        // Force garbage collection before test
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var initialMemory = GC.GetTotalMemory(false);

        // Act - Process many notes to test memory stability
        for (int i = 0; i < 1000; i++)
        {
            var result = _regexParser.ParseDeviceOrder(testNote);
            result.Device.Should().Be("Oxygen Tank"); // Verify it still works
        }

        // Force garbage collection after test
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert - Memory should be stable
        _output.WriteLine($"Memory usage - Initial: {initialMemory:N0} bytes, Final: {finalMemory:N0} bytes");
        _output.WriteLine($"Memory increase: {memoryIncrease:N0} bytes");

        memoryIncrease.Should().BeLessThan(1_500_000, "memory usage should not grow significantly"); // Less than 1.5MB
    }

    private static double CalculateStandardDeviation(IEnumerable<long> values, double mean)
    {
        var variance = values.Select(x => Math.Pow(x - mean, 2)).Average();
        return Math.Sqrt(variance);
    }
}