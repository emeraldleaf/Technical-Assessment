using Bogus;
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

[Trait("Category", "Property")]
public class PropertyBasedTests
{
    private readonly TextParser _parser;
    private readonly Faker _faker = new();

    public PropertyBasedTests()
    {
        var options = Options.Create(new SignalBoosterOptions 
        { 
            OpenAI = new OpenAIOptions { ApiKey = "" } 
        }); // Force regex
        _parser = new TextParser(options, Substitute.For<ILogger<TextParser>>());
    }

    [Theory]
    [MemberData(nameof(GenerateRandomValidNotes), parameters: 20)]
    public void ParseDeviceOrder_ValidNotes_AlwaysExtractsDevice(string noteText)
    {
        // Act
        var result = _parser.ParseDeviceOrder(noteText);

        // Assert - Property: All valid notes should extract at least a device
        result.Device.Should().NotBeNullOrWhiteSpace("every valid physician note should identify a device");
    }

    [Theory]
    [MemberData(nameof(GenerateNotesWithPatientInfo), parameters: 15)]
    public void ParseDeviceOrder_NotesWithPatientData_ExtractsPatientFields(
        string noteText, bool hasPatientName, bool hasDob)
    {
        // Act
        var result = _parser.ParseDeviceOrder(noteText);

        // Assert - Property: If patient info is present, it should be extracted
        if (hasPatientName)
            result.PatientName.Should().NotBeNullOrWhiteSpace();
        
        if (hasDob)
            result.Dob.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(GenerateEdgeCaseInputs), parameters: 10)]
    public void ParseDeviceOrder_EdgeCases_HandlesGracefully(string input, string testCase)
    {
        // Act & Assert - Property: Parser should never throw, always return something
        var result = Record.Exception(() => _parser.ParseDeviceOrder(input));
        
        result.Should().BeNull($"parser should handle edge case gracefully: {testCase}");
        
        var parsed = _parser.ParseDeviceOrder(input);
        parsed.Should().NotBeNull("parser should always return a result object");
    }

    [Fact]
    public void ParseDeviceOrder_IdenticalInputs_ProducesConsistentResults()
    {
        // Arrange - Generate a random valid note
        var noteText = PhysicianNoteBuilder.Create()
            .WithPatient(_faker.Name.FullName(), _faker.Date.Past(60).ToString("MM/dd/yyyy"))
            .WithDiagnosis(_faker.Lorem.Sentence())
            .WithProvider($"Dr. {_faker.Name.LastName()}")
            .WithDevice(_faker.PickRandom("CPAP", "Oxygen Tank", "Hospital Bed"))
            .BuildNoteText();

        // Act - Parse same input multiple times
        var results = Enumerable.Range(0, 5)
            .Select(_ => _parser.ParseDeviceOrder(noteText))
            .ToList();

        // Assert - Property: Identical inputs should produce identical outputs
        results.Should().AllBeEquivalentTo(results.First(), 
            "parser should be deterministic and produce consistent results");
    }

    [Theory]
    [MemberData(nameof(GenerateDeviceVariations), parameters: 25)]
    public void ParseDeviceOrder_DeviceNameVariations_NormalizesCorrectly(
        string deviceVariation, string expectedDevice)
    {
        // Arrange
        var noteText = $"Patient requires {deviceVariation} for medical needs.";

        // Act
        var result = _parser.ParseDeviceOrder(noteText);

        // Assert - Property: Device name variations should normalize to standard names
        result.Device.Should().Be(expectedDevice, 
            $"device variation '{deviceVariation}' should normalize to '{expectedDevice}'");
    }

    public static IEnumerable<object[]> GenerateRandomValidNotes(int count)
    {
        var faker = new Faker();
        var devices = new[] { "CPAP", "Oxygen Tank", "Hospital Bed", "TENS Unit", "Nebulizer" };
        
        for (int i = 0; i < count; i++)
        {
            var noteText = PhysicianNoteBuilder.Create()
                .WithPatient(faker.Name.FullName(), faker.Date.Past(80).ToString("MM/dd/yyyy"))
                .WithDiagnosis(faker.Lorem.Sentence())
                .WithProvider($"Dr. {faker.Name.LastName()}")
                .WithDevice(faker.PickRandom(devices))
                .BuildNoteText();
                
            yield return new object[] { noteText };
        }
    }

    public static IEnumerable<object[]> GenerateNotesWithPatientInfo(int count)
    {
        var faker = new Faker();
        
        for (int i = 0; i < count; i++)
        {
            var hasPatientName = faker.Random.Bool(0.8f); // 80% chance
            var hasDob = faker.Random.Bool(0.7f); // 70% chance
            
            var builder = PhysicianNoteBuilder.Create()
                .WithDevice("CPAP")
                .WithDiagnosis("sleep apnea");
                
            if (hasPatientName && hasDob)
            {
                builder = builder.WithPatient(faker.Name.FullName(), faker.Date.Past(80).ToString("MM/dd/yyyy"));
            }
            else if (hasPatientName)
            {
                builder = builder.WithPatient(faker.Name.FullName(), "Unknown DOB");
            }
            else if (hasDob)
            {
                builder = builder.WithPatient("Unknown Patient", faker.Date.Past(80).ToString("MM/dd/yyyy"));
            }
            
            yield return new object[] { builder.BuildNoteText(), hasPatientName, hasDob };
        }
    }

    public static IEnumerable<object[]> GenerateEdgeCaseInputs(int count)
    {
        var edgeCases = new[]
        {
            ("", "Empty string"),
            ("   \n\t   ", "Whitespace only"),
            ("No medical content here", "Non-medical text"),
            ("Patient: John\nDevice: \nDiagnosis:", "Empty fields"),
            ("CPAP CPAP CPAP oxygen tank hospital bed", "Multiple devices"),
            ("Patient needs... um... maybe a CPAP?", "Uncertain language"),
            ("Special chars: éñtity naïve résumé", "Unicode characters"),
            ("Very ".PadRight(1000, 'x'), "Extremely long text"),
            ("A\nB\nC\nD\nE\nF\nG\nH\nI\nJ", "Many short lines"),
            ("PATIENT NEEDS CPAP DEVICE", "ALL CAPS")
        };

        return edgeCases.Take(count).Select(x => new object[] { x.Item1, x.Item2 });
    }

    public static IEnumerable<object[]> GenerateDeviceVariations(int count)
    {
        var variations = new[]
        {
            ("CPAP machine", "CPAP"),
            ("cpap device", "CPAP"),
            ("oxygen concentrator", "Oxygen Tank"),
            ("O2 tank", "Oxygen Tank"),
            ("hospital bed", "Hospital Bed"),
            ("adjustable bed", "Hospital Bed"),
            ("TENS unit", "TENS Unit"),
            ("nebulizer machine", "Nebulizer"),
            ("breathing machine", "Nebulizer"),
            ("tens device", "TENS Unit")
        };

        return variations.Take(count).Select(x => new object[] { x.Item1, x.Item2 });
    }
}