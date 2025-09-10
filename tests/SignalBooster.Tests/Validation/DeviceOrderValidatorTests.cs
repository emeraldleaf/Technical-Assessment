using FluentAssertions;
using SignalBooster.Core.Models;
using SignalBooster.Core.Validation;

namespace SignalBooster.Tests.Validation;

public class DeviceOrderValidatorTests
{
    private readonly DeviceOrderValidator _validator;

    public DeviceOrderValidatorTests()
    {
        _validator = new DeviceOrderValidator();
    }

    [Fact]
    public void Validate_WithValidCpapOrder_ShouldPass()
    {
        // Arrange
        var deviceOrder = new DeviceOrder(
            Device: "CPAP",
            MaskType: "full face",
            AddOns: null,
            Qualifier: null,
            OrderingProvider: "Dr. Smith",
            PatientName: "John Doe",
            DateOfBirth: "01/01/1980"
        )
        {
            PatientId = "12345",
            Specifications = new Dictionary<string, object>
            {
                ["MaskType"] = "full face",
                ["PressureMin"] = "8 cmH2O"
            }
        };

        // Act
        var result = _validator.Validate(deviceOrder);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithValidOxygenOrder_ShouldPass()
    {
        // Arrange
        var deviceOrder = new DeviceOrder(
            Device: "Oxygen",
            MaskType: null,
            AddOns: null,
            Qualifier: null,
            OrderingProvider: "Dr. Johnson",
            PatientName: "Jane Smith",
            DateOfBirth: "05/15/1975"
        )
        {
            PatientId = "67890",
            Specifications = new Dictionary<string, object>
            {
                ["FlowRate"] = "2 L/min",
                ["DeliveryMethod"] = "nasal cannula"
            }
        };

        // Act
        var result = _validator.Validate(deviceOrder);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WithInvalidDeviceType_ShouldFail(string? deviceType)
    {
        // Arrange
        var deviceOrder = new DeviceOrder(
            Device: deviceType!,
            MaskType: null,
            AddOns: null,
            Qualifier: null,
            OrderingProvider: "Dr. Smith",
            PatientName: "Test Patient",
            DateOfBirth: "01/01/1980"
        )
        {
            PatientId = "12345",
            Specifications = new Dictionary<string, object> { ["Type"] = "test" }
        };

        // Act
        var result = _validator.Validate(deviceOrder);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(DeviceOrder.DeviceType));
    }

    [Theory]
    [InlineData("InvalidDevice")]
    [InlineData("Ventilator")]
    [InlineData("CPAP Machine")] // Close but not exact
    public void Validate_WithUnsupportedDeviceType_ShouldFail(string deviceType)
    {
        // Arrange
        var deviceOrder = new DeviceOrder(
            Device: deviceType,
            MaskType: null,
            AddOns: null,
            Qualifier: null,
            OrderingProvider: "Dr. Smith",
            PatientName: "Test Patient",
            DateOfBirth: "01/01/1980"
        )
        {
            PatientId = "12345"
        };

        // Act
        var result = _validator.Validate(deviceOrder);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(DeviceOrder.DeviceType) && 
            e.ErrorMessage.Contains("CPAP, BiPAP, Oxygen"));
    }

    [Theory]
    [InlineData("CPAP")]
    [InlineData("BiPAP")]
    [InlineData("Oxygen")]
    [InlineData("Nebulizer")]
    [InlineData("Wheelchair")]
    [InlineData("Walker")]
    [InlineData("Hospital Bed")]
    public void Validate_WithValidDeviceTypes_ShouldPass(string deviceType)
    {
        // Arrange
        var specifications = new Dictionary<string, object>();
        
        // Add required specifications based on device type
        switch (deviceType.ToUpperInvariant())
        {
            case "CPAP":
                specifications["MaskType"] = "full face";
                specifications["Pressure"] = "10 cmH2O";
                break;
            case "OXYGEN":
                specifications["FlowRate"] = "2 L/min";
                specifications["DeliveryMethod"] = "nasal cannula";
                break;
            default:
                specifications["Type"] = "standard";
                break;
        }
        
        var deviceOrder = new DeviceOrder(
            Device: deviceType,
            MaskType: null,
            AddOns: null,
            Qualifier: null,
            OrderingProvider: "Dr. Smith",
            PatientName: "Test Patient",
            DateOfBirth: "01/01/1980"
        )
        {
            PatientId = "12345",
            Specifications = specifications
        };

        // Act
        var result = _validator.Validate(deviceOrder);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WithInvalidProvider_ShouldFail(string? provider)
    {
        // Arrange
        var deviceOrder = new DeviceOrder(
            Device: "CPAP",
            MaskType: null,
            AddOns: null,
            Qualifier: null,
            OrderingProvider: provider!,
            PatientName: "Test Patient",
            DateOfBirth: "01/01/1980"
        )
        {
            PatientId = "12345",
            Specifications = new Dictionary<string, object> 
            { 
                ["MaskType"] = "full face",
                ["Pressure"] = "10 cmH2O"
            }
        };

        // Act
        var result = _validator.Validate(deviceOrder);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(DeviceOrder.Provider));
    }

    [Fact]
    public void Validate_WithProviderTooLong_ShouldFail()
    {
        // Arrange
        var longProvider = new string('A', 101); // 101 characters
        var deviceOrder = new DeviceOrder(
            Device: "CPAP",
            MaskType: null,
            AddOns: null,
            Qualifier: null,
            OrderingProvider: longProvider,
            PatientName: "Test Patient",
            DateOfBirth: "01/01/1980"
        )
        {
            PatientId = "12345"
        };

        // Act
        var result = _validator.Validate(deviceOrder);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(DeviceOrder.Provider) && 
            e.ErrorMessage.Contains("100 characters"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WithInvalidPatientId_ShouldFail(string? patientId)
    {
        // Arrange
        var deviceOrder = new DeviceOrder(
            Device: "CPAP",
            MaskType: null,
            AddOns: null,
            Qualifier: null,
            OrderingProvider: "Dr. Smith",
            PatientName: "Test Patient",
            DateOfBirth: "01/01/1980"
        )
        {
            PatientId = patientId!,
            Specifications = new Dictionary<string, object> 
            { 
                ["MaskType"] = "full face",
                ["Pressure"] = "10 cmH2O"
            }
        };

        // Act
        var result = _validator.Validate(deviceOrder);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(DeviceOrder.PatientId));
    }

    [Fact]
    public void Validate_WithCpapMissingMaskType_ShouldFail()
    {
        // Arrange
        var deviceOrder = new DeviceOrder(
            Device: "CPAP",
            MaskType: null,
            AddOns: null,
            Qualifier: null,
            OrderingProvider: "Dr. Smith",
            PatientName: "Test Patient",
            DateOfBirth: "01/01/1980"
        )
        {
            PatientId = "12345",
            Specifications = new Dictionary<string, object>
            {
                ["Pressure"] = "10 cmH2O"
                // Missing MaskType
            }
        };

        // Act
        var result = _validator.Validate(deviceOrder);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(DeviceOrder.Specifications) &&
            e.ErrorMessage.Contains("mask type"));
    }

    [Fact]
    public void Validate_WithCpapMissingPressureSettings_ShouldFail()
    {
        // Arrange
        var deviceOrder = new DeviceOrder(
            Device: "CPAP",
            MaskType: null,
            AddOns: null,
            Qualifier: null,
            OrderingProvider: "Dr. Smith",
            PatientName: "Test Patient",
            DateOfBirth: "01/01/1980"
        )
        {
            PatientId = "12345",
            Specifications = new Dictionary<string, object>
            {
                ["MaskType"] = "full face"
                // Missing pressure settings
            }
        };

        // Act
        var result = _validator.Validate(deviceOrder);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(DeviceOrder.Specifications) &&
            e.ErrorMessage.Contains("pressure settings"));
    }

    [Fact]
    public void Validate_WithOxygenMissingFlowRate_ShouldFail()
    {
        // Arrange
        var deviceOrder = new DeviceOrder(
            Device: "Oxygen",
            MaskType: null,
            AddOns: null,
            Qualifier: null,
            OrderingProvider: "Dr. Smith",
            PatientName: "Test Patient",
            DateOfBirth: "01/01/1980"
        )
        {
            PatientId = "12345",
            Specifications = new Dictionary<string, object>
            {
                ["DeliveryMethod"] = "nasal cannula"
                // Missing FlowRate
            }
        };

        // Act
        var result = _validator.Validate(deviceOrder);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(DeviceOrder.Specifications) &&
            e.ErrorMessage.Contains("flow rate"));
    }

    [Fact]
    public void Validate_WithOxygenMissingDeliveryMethod_ShouldFail()
    {
        // Arrange
        var deviceOrder = new DeviceOrder(
            Device: "Oxygen",
            MaskType: null,
            AddOns: null,
            Qualifier: null,
            OrderingProvider: "Dr. Smith",
            PatientName: "Test Patient",
            DateOfBirth: "01/01/1980"
        )
        {
            PatientId = "12345",
            Specifications = new Dictionary<string, object>
            {
                ["FlowRate"] = "2 L/min"
                // Missing DeliveryMethod
            }
        };

        // Act
        var result = _validator.Validate(deviceOrder);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(DeviceOrder.Specifications) &&
            e.ErrorMessage.Contains("delivery method"));
    }

    [Fact]
    public void Validate_WithNullSpecifications_ShouldFail()
    {
        // Arrange
        var deviceOrder = new DeviceOrder(
            Device: "CPAP",
            MaskType: null,
            AddOns: null,
            Qualifier: null,
            OrderingProvider: "Dr. Smith",
            PatientName: "Test Patient",
            DateOfBirth: "01/01/1980"
        )
        {
            PatientId = "12345",
            Specifications = null
        };

        // Act
        var result = _validator.Validate(deviceOrder);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(DeviceOrder.Specifications) &&
            e.ErrorMessage.Contains("required"));
    }

    [Fact]
    public void Validate_WithNonCpapOrOxygenDevice_ShouldNotRequireSpecificSpecs()
    {
        // Arrange
        var deviceOrder = new DeviceOrder(
            Device: "Wheelchair",
            MaskType: null,
            AddOns: null,
            Qualifier: null,
            OrderingProvider: "Dr. Smith",
            PatientName: "Test Patient",
            DateOfBirth: "01/01/1980"
        )
        {
            PatientId = "12345",
            Specifications = new Dictionary<string, object>
            {
                ["Type"] = "manual"
            }
        };

        // Act
        var result = _validator.Validate(deviceOrder);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}