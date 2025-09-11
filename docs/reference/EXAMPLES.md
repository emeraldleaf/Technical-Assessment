# Signal Booster - Feature Extension Examples

This document provides step-by-step examples for common development tasks that junior developers will encounter.

## Example 1: Adding a New DME Device Type (Blood Glucose Monitor)

### Step 1: Update Device Detection

Edit `src/SignalBooster.Core/Services/NoteParser.cs` in the `DetermineDeviceType` method:

```csharp
var devicePatterns = new Dictionary<string, string[]>
{
    ["CPAP"] = ["cpap", "continuous positive airway pressure"],
    ["BiPAP"] = ["bipap", "bilevel", "bi-level"],
    ["Oxygen"] = ["oxygen", "o2", "oxygen tank", "oxygen concentrator"],
    ["Nebulizer"] = ["nebulizer", "breathing treatment", "albuterol"],
    ["Wheelchair"] = ["wheelchair", "mobility device"],
    ["Walker"] = ["walker", "walking aid"],
    ["Hospital Bed"] = ["hospital bed", "adjustable bed"],
    ["Blood Glucose Monitor"] = ["glucose monitor", "blood glucose", "diabetes monitor", "glucometer"] // 👈 Add this
};
```

### Step 2: Add Device-Specific Parsing

Add a new case in the `ExtractDeviceSpecifications` method:

```csharp
case "BLOOD GLUCOSE MONITOR":
    ExtractBloodGlucoseSpecifications(text, specifications);
    break;
```

### Step 3: Implement the Parsing Method

Add the new method to the `NoteParser` class:

```csharp
/// <summary>
/// Extracts blood glucose monitor specifications from physician note text.
/// Looks for testing frequency, strip type, and monitoring requirements.
/// </summary>
private void ExtractBloodGlucoseSpecifications(string text, Dictionary<string, object> specs)
{
    // Testing frequency - how often patient should test
    var frequencyPatterns = new[]
    {
        @"test\s*(\d+)\s*times?\s*(?:per\s*)?day",
        @"(\d+)x\s*daily",
        @"check\s*(\d+)\s*times?\s*daily"
    };

    foreach (var pattern in frequencyPatterns)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (match.Success)
        {
            specs["TestingFrequency"] = $"{match.Groups[1].Value} times per day";
            break;
        }
    }

    // Strip type
    if (text.Contains("test strips", StringComparison.OrdinalIgnoreCase))
        specs["IncludesStrips"] = true;
    
    // Monitoring type
    if (text.Contains("continuous", StringComparison.OrdinalIgnoreCase))
        specs["MonitoringType"] = "continuous";
    else
        specs["MonitoringType"] = "standard";

    // Data connectivity
    if (text.Contains("bluetooth", StringComparison.OrdinalIgnoreCase) || text.Contains("app", StringComparison.OrdinalIgnoreCase))
        specs["Connectivity"] = "bluetooth";
}
```

### Step 4: Write Tests

Create tests in `tests/SignalBooster.Tests/Services/NoteParserTests.cs`:

```csharp
[Theory]
[InlineData("Patient needs glucose monitor with test strips. Check 4x daily.", "Blood Glucose Monitor")]
[InlineData("Diabetes monitoring device required for blood glucose tracking.", "Blood Glucose Monitor")]
[InlineData("Glucometer needed for home testing.", "Blood Glucose Monitor")]
public void ParseNoteFromText_BloodGlucoseMonitor_ReturnsCorrectDeviceType(string noteText, string expectedDevice)
{
    // Arrange
    var note = CreateValidNote(noteText);

    // Act
    var result = _noteParser.ExtractDeviceOrder(note.Value!);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value!.DeviceType.Should().Be(expectedDevice);
}

[Fact]
public void ExtractDeviceOrder_BloodGlucoseMonitor_ExtractsSpecifications()
{
    // Arrange
    var noteText = "Patient needs glucose monitor with test strips. Check 4x daily with Bluetooth connectivity.";
    var note = CreateValidNote(noteText);

    // Act
    var result = _noteParser.ExtractDeviceOrder(note.Value!);

    // Assert
    result.IsSuccess.Should().BeTrue();
    var specs = result.Value!.Specifications;
    specs.Should().ContainKey("TestingFrequency");
    specs["TestingFrequency"].Should().Be("4 times per day");
    specs.Should().ContainKey("IncludesStrips");
    specs["IncludesStrips"].Should().Be(true);
    specs.Should().ContainKey("Connectivity");
    specs["Connectivity"].Should().Be("bluetooth");
}
```

### Step 5: Run Tests

```bash
dotnet test --filter "BloodGlucose"
```

## Example 2: Adding a New Feature Handler (Processing Insurance Claims)

### Step 1: Create Feature Directory Structure

```
src/SignalBooster.Core/Features/ProcessClaim/
├── ProcessClaimRequest.cs
├── ProcessClaimResponse.cs  
├── ProcessClaimHandler.cs
└── ProcessClaimValidator.cs
```

### Step 2: Create the Request Model

`ProcessClaimRequest.cs`:
```csharp
using SignalBooster.Core.Models;

namespace SignalBooster.Core.Features.ProcessClaim;

public record ProcessClaimRequest(
    string ClaimId,
    string PatientId, 
    DeviceOrder DeviceOrder,
    string InsuranceProvider
);
```

### Step 3: Create the Response Model

`ProcessClaimResponse.cs`:
```csharp
namespace SignalBooster.Core.Features.ProcessClaim;

public record ProcessClaimResponse(
    string ClaimId,
    string Status,
    decimal ApprovedAmount,
    string AuthorizationCode,
    DateTime ProcessedDate
);
```

### Step 4: Create the Validator

`ProcessClaimValidator.cs`:
```csharp
using FluentValidation;

namespace SignalBooster.Core.Features.ProcessClaim;

public class ProcessClaimValidator : AbstractValidator<ProcessClaimRequest>
{
    public ProcessClaimValidator()
    {
        RuleFor(x => x.ClaimId)
            .NotEmpty()
            .WithMessage("Claim ID is required");

        RuleFor(x => x.PatientId)
            .NotEmpty()
            .WithMessage("Patient ID is required");

        RuleFor(x => x.DeviceOrder)
            .NotNull()
            .WithMessage("Device order is required");

        RuleFor(x => x.InsuranceProvider)
            .NotEmpty()
            .WithMessage("Insurance provider is required");
    }
}
```

### Step 5: Create the Handler

`ProcessClaimHandler.cs`:
```csharp
using FluentValidation;
using Microsoft.Extensions.Logging;
using SignalBooster.Core.Common;
using SignalBooster.Core.Domain.Errors;

namespace SignalBooster.Core.Features.ProcessClaim;

public class ProcessClaimHandler
{
    private readonly ILogger<ProcessClaimHandler> _logger;
    private readonly IValidator<ProcessClaimRequest> _validator;

    public ProcessClaimHandler(
        ILogger<ProcessClaimHandler> logger,
        IValidator<ProcessClaimRequest> validator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<Result<ProcessClaimResponse>> Handle(ProcessClaimRequest request)
    {
        try
        {
            _logger.LogInformation("Processing insurance claim {ClaimId} for patient {PatientId}", 
                request.ClaimId, request.PatientId);

            // Validate the request
            var validationResult = await _validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .Select(e => ValidationErrors.InvalidFormat(e.PropertyName, e.ErrorMessage))
                    .ToList();
                
                return errors;
            }

            // Simulate claim processing logic
            var response = new ProcessClaimResponse(
                request.ClaimId,
                "Approved",
                1250.00m,
                $"AUTH{DateTime.UtcNow:yyyyMMddHHmmss}",
                DateTime.UtcNow
            );

            _logger.LogInformation("Successfully processed claim {ClaimId} with authorization {AuthCode}", 
                request.ClaimId, response.AuthorizationCode);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing claim {ClaimId}", request.ClaimId);
            return ApiErrors.ClaimProcessingFailed(ex.Message);
        }
    }
}
```

### Step 6: Register Services

Add to `Program.cs` in the `ConfigureServices` method:

```csharp
// Feature handlers
services.AddScoped<ProcessNoteHandler>();
services.AddScoped<ProcessClaimHandler>(); // 👈 Add this

// Validators
services.AddScoped<IValidator<ProcessNoteRequest>, ProcessNoteRequestValidator>();
services.AddScoped<IValidator<ProcessClaimRequest>, ProcessClaimValidator>(); // 👈 Add this
```

### Step 7: Add Error Types

Add to `src/SignalBooster.Core/Domain/Errors/ApiErrors.cs`:

```csharp
public static Error ClaimProcessingFailed(string details) => 
    new("Api.ClaimProcessingFailed", $"Failed to process insurance claim: {details}");
```

### Step 8: Write Tests

Create `tests/SignalBooster.Tests/Features/ProcessClaimHandlerTests.cs`:

```csharp
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Moq;
using SignalBooster.Core.Features.ProcessClaim;
using SignalBooster.Core.Models;

namespace SignalBooster.Tests.Features;

public class ProcessClaimHandlerTests
{
    private readonly Mock<ILogger<ProcessClaimHandler>> _loggerMock;
    private readonly Mock<IValidator<ProcessClaimRequest>> _validatorMock;
    private readonly ProcessClaimHandler _handler;

    public ProcessClaimHandlerTests()
    {
        _loggerMock = new Mock<ILogger<ProcessClaimHandler>>();
        _validatorMock = new Mock<IValidator<ProcessClaimRequest>>();
        _handler = new ProcessClaimHandler(_loggerMock.Object, _validatorMock.Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsApprovedClaim()
    {
        // Arrange
        var request = new ProcessClaimRequest(
            "CLAIM123",
            "PAT456", 
            CreateTestDeviceOrder(),
            "BlueCross BlueShield"
        );

        _validatorMock
            .Setup(x => x.ValidateAsync(request, default))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ClaimId.Should().Be("CLAIM123");
        result.Value!.Status.Should().Be("Approved");
        result.Value!.ApprovedAmount.Should().BeGreaterThan(0);
    }

    private DeviceOrder CreateTestDeviceOrder() =>
        new DeviceOrder("CPAP", null, null, null, "Dr. Smith", null, null, "Sleep Apnea", "John Doe", "1980-01-01");
}
```

## Example 3: Adding Configuration Options

### Step 1: Update Configuration Model

Edit `src/SignalBooster.Core/Configuration/SignalBoosterOptions.cs`:

```csharp
public class SignalBoosterOptions
{
    public const string SectionName = "SignalBooster";
    
    public string ApiEndpoint { get; set; } = string.Empty;
    public FileOptions Files { get; set; } = new();
    public RetryOptions Retry { get; set; } = new();
    public ClaimOptions Claims { get; set; } = new(); // 👈 Add this
}

// 👈 Add new options class
public class ClaimOptions
{
    public string DefaultInsuranceProvider { get; set; } = "Unknown";
    public int TimeoutSeconds { get; set; } = 30;
    public bool AutoSubmit { get; set; } = false;
}
```

### Step 2: Update appsettings.json

```json
{
  "SignalBooster": {
    "ApiEndpoint": "https://alert-api.com/DrExtract",
    "Files": {
      "DefaultInputPath": "physician_note.txt",
      "OutputDirectory": "output/"
    },
    "Retry": {
      "MaxAttempts": 3,
      "DelayMs": 1000  
    },
    "Claims": {
      "DefaultInsuranceProvider": "Medicare",
      "TimeoutSeconds": 45,
      "AutoSubmit": true
    }
  }
}
```

### Step 3: Use Configuration in Handler

Update your handler constructor:

```csharp
public class ProcessClaimHandler
{
    private readonly IOptions<SignalBoosterOptions> _options;
    // ... other dependencies

    public ProcessClaimHandler(
        IOptions<SignalBoosterOptions> options,
        // ... other parameters
    )
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        // ... assign other dependencies
    }

    public async Task<Result<ProcessClaimResponse>> Handle(ProcessClaimRequest request)
    {
        var claimOptions = _options.Value.Claims;
        var timeout = TimeSpan.FromSeconds(claimOptions.TimeoutSeconds);
        
        // Use configuration values in your logic
        // ...
    }
}
```

## Running and Testing Your Changes

### Build and Test
```bash
# Build the solution
dotnet build

# Run all tests
dotnet test

# Run specific test category
dotnet test --filter "Category=Integration"

# Run the application
dotnet run --project src/SignalBooster.Core
```

### Debugging Tips

1. **Use structured logging** for visibility:
   ```csharp
   _logger.LogInformation("Processing {DeviceType} order for patient {PatientId}", 
       order.DeviceType, order.PatientId);
   ```

2. **Leverage the Result pattern** for error handling:
   ```csharp
   if (someCondition)
       return ValidationErrors.SomeSpecificError("details");
   ```

3. **Test incrementally** - add one feature at a time and test thoroughly before moving to the next.

These examples demonstrate the consistent patterns used throughout the Signal Booster codebase. Following these patterns will help maintain code quality and make your features easy to understand and maintain.