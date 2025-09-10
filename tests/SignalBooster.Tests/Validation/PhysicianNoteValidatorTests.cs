using FluentAssertions;
using SignalBooster.Core.Models;
using SignalBooster.Core.Validation;

namespace SignalBooster.Tests.Validation;

public class PhysicianNoteValidatorTests
{
    private readonly PhysicianNoteValidator _validator;

    public PhysicianNoteValidatorTests()
    {
        _validator = new PhysicianNoteValidator();
    }

    [Fact]
    public void Validate_WithValidNote_ShouldPass()
    {
        // Arrange
        var note = new PhysicianNote(
            PatientName: "John Doe",
            DateOfBirth: "01/01/1980",
            Diagnosis: "Sleep Apnea",
            Prescription: "Patient needs CPAP therapy with full face mask",
            Usage: "Nightly use",
            OrderingProvider: "Dr. Smith",
            RawText: "Patient needs CPAP therapy with full face mask for sleep apnea treatment. Ordered by Dr. Smith."
        )
        {
            PatientId = "12345",
            NoteDate = DateTime.UtcNow.AddDays(-1)
        };

        // Act
        var result = _validator.Validate(note);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WithInvalidRawText_ShouldFail(string? rawText)
    {
        // Arrange
        var note = CreateValidNote();
        note = note with { RawText = rawText! };

        // Act
        var result = _validator.Validate(note);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(PhysicianNote.RawText));
    }

    [Fact]
    public void Validate_WithRawTextTooShort_ShouldFail()
    {
        // Arrange
        var note = CreateValidNote();
        note = note with { RawText = "Too short" }; // Less than 10 characters

        // Act
        var result = _validator.Validate(note);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(PhysicianNote.RawText) &&
            e.ErrorMessage.Contains("10 characters"));
    }

    [Fact]
    public void Validate_WithRawTextTooLong_ShouldFail()
    {
        // Arrange
        var note = CreateValidNote();
        var veryLongText = new string('A', 10001); // More than 10,000 characters
        note = note with { RawText = veryLongText };

        // Act
        var result = _validator.Validate(note);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(PhysicianNote.RawText) &&
            e.ErrorMessage.Contains("10,000 characters"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WithInvalidPatientId_ShouldFail(string? patientId)
    {
        // Arrange
        var note = CreateValidNote();
        note = note with { PatientId = patientId! };

        // Act
        var result = _validator.Validate(note);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(PhysicianNote.PatientId));
    }

    [Fact]
    public void Validate_WithPatientIdTooLong_ShouldFail()
    {
        // Arrange
        var note = CreateValidNote();
        var longPatientId = new string('1', 51); // More than 50 characters
        note = note with { PatientId = longPatientId };

        // Act
        var result = _validator.Validate(note);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(PhysicianNote.PatientId) &&
            e.ErrorMessage.Contains("50 characters"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WithInvalidPhysicianName_ShouldFail(string? physicianName)
    {
        // Arrange
        var note = CreateValidNote();
        note = note with { OrderingProvider = physicianName! };

        // Act
        var result = _validator.Validate(note);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(PhysicianNote.OrderingProvider));
    }

    [Fact]
    public void Validate_WithPhysicianNameTooShort_ShouldFail()
    {
        // Arrange
        var note = CreateValidNote();
        note = note with { OrderingProvider = "A" }; // Less than 2 characters

        // Act
        var result = _validator.Validate(note);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(PhysicianNote.OrderingProvider) &&
            e.ErrorMessage.Contains("2 and 100 characters"));
    }

    [Fact]
    public void Validate_WithPhysicianNameTooLong_ShouldFail()
    {
        // Arrange
        var note = CreateValidNote();
        var longName = new string('A', 101); // More than 100 characters
        note = note with { OrderingProvider = longName };

        // Act
        var result = _validator.Validate(note);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(PhysicianNote.OrderingProvider) &&
            e.ErrorMessage.Contains("100 characters"));
    }

    [Theory]
    [InlineData("Patient needs CPAP therapy for sleep apnea")]
    [InlineData("Oxygen therapy required for respiratory support")]
    [InlineData("BiPAP treatment recommended")]
    [InlineData("Nebulizer for breathing treatments")]
    [InlineData("Wheelchair for mobility assistance")]
    [InlineData("Walker needed for patient")]
    [InlineData("Hospital bed required")]
    [InlineData("Patient has breathing difficulties")]
    [InlineData("Sleep apnea diagnosis confirmed")]
    [InlineData("Respiratory issues noted")]
    [InlineData("Mobility concerns addressed")]
    [InlineData("DME equipment needed")]
    [InlineData("Durable medical equipment prescribed")]
    public void Validate_WithDeviceReferenceInText_ShouldPass(string textWithDeviceReference)
    {
        // Arrange
        var note = CreateValidNote();
        note = note with { RawText = textWithDeviceReference };

        // Act
        var result = _validator.Validate(note);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Patient has a headache and needs pain medication")]
    [InlineData("Regular checkup shows normal vitals")]
    [InlineData("Patient reports feeling better today")]
    [InlineData("Blood work results are normal")]
    public void Validate_WithoutDeviceReference_ShouldFail(string textWithoutDeviceReference)
    {
        // Arrange
        var note = CreateValidNote();
        note = note with { RawText = textWithoutDeviceReference };

        // Act
        var result = _validator.Validate(note);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(PhysicianNote.RawText) &&
            e.ErrorMessage.Contains("medical device"));
    }

    [Fact]
    public void Validate_WithMultipleValidationErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var note = new PhysicianNote(
            PatientName: "", // Invalid - empty
            DateOfBirth: "01/01/1980",
            Diagnosis: "Test",
            Prescription: "Test",
            Usage: "Test",
            OrderingProvider: "A", // Invalid - too short
            RawText: "Short" // Invalid - too short and no device reference
        )
        {
            PatientId = "", // Invalid - empty
            NoteDate = DateTime.UtcNow
        };

        // Act
        var result = _validator.Validate(note);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(1);
        
        // Should have errors for multiple fields
        var errorProperties = result.Errors.Select(e => e.PropertyName).ToList();
        errorProperties.Should().Contain(nameof(PhysicianNote.PatientName));
        errorProperties.Should().Contain(nameof(PhysicianNote.PatientId));
        errorProperties.Should().Contain(nameof(PhysicianNote.OrderingProvider));
        errorProperties.Should().Contain(nameof(PhysicianNote.RawText));
    }

    [Fact]
    public void Validate_WithValidMinimumFieldLengths_ShouldPass()
    {
        // Arrange
        var note = new PhysicianNote(
            PatientName: "AB", // Minimum 2 characters (from base validation)
            DateOfBirth: "01/01/1980",
            Diagnosis: "Test",
            Prescription: "Test",
            Usage: "Test",
            OrderingProvider: "Dr", // Minimum 2 characters
            RawText: "Patient needs CPAP equipment for treatment" // Contains device reference and >10 chars
        )
        {
            PatientId = "1", // Minimum 1 character
            NoteDate = DateTime.UtcNow
        };

        // Act
        var result = _validator.Validate(note);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithMaximumFieldLengths_ShouldPass()
    {
        // Arrange
        var note = new PhysicianNote(
            PatientName: new string('A', 100), // Assuming reasonable max length
            DateOfBirth: "01/01/1980",
            Diagnosis: "Test",
            Prescription: "Test", 
            Usage: "Test",
            OrderingProvider: new string('D', 100), // Max 100 characters
            RawText: "Patient needs CPAP equipment. " + new string('X', 9950) // Close to 10k limit
        )
        {
            PatientId = new string('1', 50), // Max 50 characters
            NoteDate = DateTime.UtcNow
        };

        // Act
        var result = _validator.Validate(note);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    private static PhysicianNote CreateValidNote()
    {
        return new PhysicianNote(
            PatientName: "John Doe",
            DateOfBirth: "01/01/1980",
            Diagnosis: "Sleep Apnea",
            Prescription: "CPAP therapy prescribed",
            Usage: "Nightly use",
            OrderingProvider: "Dr. Smith",
            RawText: "Patient needs CPAP therapy with full face mask for sleep apnea treatment. Ordered by Dr. Smith."
        )
        {
            PatientId = "12345",
            NoteDate = DateTime.UtcNow.AddDays(-1)
        };
    }
}