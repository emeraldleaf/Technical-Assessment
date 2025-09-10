using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SignalBooster.Core.Models;
using SignalBooster.Core.Services;

namespace SignalBooster.Tests.Services;

public class NoteParserTests
{
    private readonly ILogger<NoteParser> _logger;
    private readonly IValidator<PhysicianNote> _noteValidator;
    private readonly IValidator<DeviceOrder> _deviceOrderValidator;
    private readonly NoteParser _noteParser;

    public NoteParserTests()
    {
        _logger = Substitute.For<ILogger<NoteParser>>();
        _noteValidator = Substitute.For<IValidator<PhysicianNote>>();
        _deviceOrderValidator = Substitute.For<IValidator<DeviceOrder>>();
        
        // Setup validators to return valid by default
        _noteValidator.Validate(Arg.Any<PhysicianNote>())
            .Returns(new FluentValidation.Results.ValidationResult());
        _deviceOrderValidator.Validate(Arg.Any<DeviceOrder>())
            .Returns(new FluentValidation.Results.ValidationResult());
        
        _noteParser = new NoteParser(_logger, _noteValidator, _deviceOrderValidator);
    }

    [Fact]
    public void ParseNoteFromText_WithValidCpapNote_ShouldReturnSuccessResult()
    {
        // Arrange
        var noteText = "Patient needs a CPAP with full face mask and humidifier. AHI > 20. Ordered by Dr. Cameron.";

        // Act
        var result = _noteParser.ParseNoteFromText(noteText);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.RawText.Should().Be(noteText);
        result.Value.OrderingProvider.Should().Be("Dr. Cameron");
    }

    [Fact]
    public void ParseNoteFromText_WithEmptyText_ShouldReturnValidationError()
    {
        // Arrange
        var noteText = "";

        // Act
        var result = _noteParser.ParseNoteFromText(noteText);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Validation.MissingRequiredField");
    }

    [Fact]
    public void ExtractDeviceOrder_WithCpapNote_ShouldReturnCpapOrder()
    {
        // Arrange
        var note = new PhysicianNote(
            "John Doe",
            "01/01/1980", 
            "Sleep Apnea",
            "CPAP therapy",
            "Nightly",
            "Dr. Cameron",
            "Patient needs a CPAP with full face mask and humidifier. AHI > 20. Ordered by Dr. Cameron."
        );

        // Act
        var result = _noteParser.ExtractDeviceOrder(note);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Device.Should().Be("CPAP");
        result.Value.PatientName.Should().Be("John Doe");
        result.Value.OrderingProvider.Should().Be("Dr. Cameron");
        result.Value.Specifications.Should().NotBeNull();
        result.Value.Specifications!.Should().ContainKey("MaskType");
        result.Value.Specifications["MaskType"].Should().Be("full face");
    }

    [Theory]
    [InlineData("Patient needs oxygen at 2 L/min for sleep apnea. Ordered by Dr. Smith.", "Oxygen")]
    [InlineData("Prescribe CPAP therapy with nasal mask. Ordered by Dr. Johnson.", "CPAP")]
    [InlineData("Patient requires BiPAP for respiratory support. Ordered by Dr. Wilson.", "BiPAP")]
    [InlineData("Nebulizer treatments with albuterol 3 times per day. Ordered by Dr. Davis.", "Nebulizer")]
    [InlineData("Manual wheelchair for mobility assistance. Ordered by Dr. Miller.", "Wheelchair")]
    public void ExtractDeviceOrder_WithDifferentDeviceTypes_ShouldReturnCorrectDeviceType(
        string noteText, 
        string expectedDeviceType)
    {
        // Arrange
        var note = new PhysicianNote(
            "Test Patient",
            "01/01/1990",
            "Test Diagnosis", 
            noteText,
            "As needed",
            "Dr. Test",
            noteText
        );

        // Act
        var result = _noteParser.ExtractDeviceOrder(note);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Device.Should().Be(expectedDeviceType);
    }

    [Fact]
    public void ParseNoteFromText_WithComplexNote_ShouldExtractAllFields()
    {
        // Arrange
        var noteText = @"
            Patient Name: Jane Smith
            DOB: 03/15/1975
            Diagnosis: Obstructive Sleep Apnea
            Prescription: CPAP therapy with full face mask and heated humidifier
            Usage: Nightly use
            Patient requires CPAP therapy with full face mask due to severe sleep apnea.
            AHI > 30 events per hour. Include heated humidifier for patient comfort.
            Ordered by Dr. Rebecca Martinez.";

        // Act
        var result = _noteParser.ParseNoteFromText(noteText);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var note = result.Value!;
        
        note.PatientName.Should().Be("Jane Smith");
        note.DateOfBirth.Should().Be("03/15/1975");
        note.Diagnosis.Should().Be("Obstructive Sleep Apnea");
        note.OrderingProvider.Should().Be("Dr. Rebecca Martinez");
        note.RawText.Should().Contain("CPAP therapy");
    }

    [Fact]
    public void ExtractDeviceOrder_WithOxygenNote_ShouldExtractOxygenSpecifications()
    {
        // Arrange
        var noteText = "Patient requires oxygen therapy at 2.5 L/min via nasal cannula for continuous use. Ordered by Dr. Anderson.";
        var note = new PhysicianNote(
            "Test Patient",
            "01/01/1985",
            "Chronic Hypoxemia",
            noteText,
            "Continuous",
            "Dr. Anderson",
            noteText
        );

        // Act
        var result = _noteParser.ExtractDeviceOrder(note);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var deviceOrder = result.Value!;
        
        deviceOrder.Device.Should().Be("Oxygen");
        deviceOrder.Specifications.Should().NotBeNull();
        deviceOrder.Specifications!.Should().ContainKey("FlowRate");
        deviceOrder.Specifications["FlowRate"].Should().Be("2.5 L/min");
        deviceOrder.Specifications.Should().ContainKey("DeliveryMethod");
        deviceOrder.Specifications["DeliveryMethod"].Should().Be("nasal cannula");
        deviceOrder.Specifications.Should().ContainKey("Usage");
        deviceOrder.Specifications["Usage"].Should().Be("continuous");
    }
}