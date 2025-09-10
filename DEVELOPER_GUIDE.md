# Signal Booster Developer Guide

## 🏗️ Architecture Overview

Signal Booster follows a **vertical slice architecture** that makes it easy to add new features without affecting existing code.

### Project Structure
```
src/SignalBooster.Core/
├── Features/           # Self-contained business features
├── Services/           # Reusable business services
├── Models/            # Domain models
├── Validation/        # Input validation rules
├── Configuration/     # App settings and options
├── Infrastructure/    # Cross-cutting concerns (logging, etc.)
└── Common/           # Shared utilities (Result pattern, Error types)

tests/SignalBooster.Tests/
├── Features/         # Feature-level tests
├── Services/         # Unit tests for services  
├── Validation/       # Validator tests
└── Common/          # Shared test utilities
```

## 🔧 Adding New Features

### 1. Create a New Feature Slice

When adding a new feature (e.g., processing insurance claims), create:

```
Features/ProcessClaim/
├── ProcessClaimRequest.cs      # Input model
├── ProcessClaimResponse.cs     # Output model  
├── ProcessClaimHandler.cs      # Business logic
└── ProcessClaimValidator.cs    # Input validation
```

### 2. Register Services

Add your new services to `Program.cs`:

```csharp
// Feature handlers
services.AddScoped<ProcessClaimHandler>();

// Validators  
services.AddScoped<IValidator<ProcessClaimRequest>, ProcessClaimValidator>();
```

### 3. Follow the Pattern

Each feature follows the same pattern:
- **Handler**: Contains business logic, returns `Result<T>`
- **Request**: Input parameters with validation attributes
- **Validator**: FluentValidation rules
- **Tests**: Comprehensive unit tests

## 🏥 Adding New Device Types

To add a new DME device (e.g., "Blood Glucose Monitor"):

### 1. Update the NoteParser

Edit `Services/NoteParser.cs` and add detection logic:

```csharp
private static string ExtractDeviceType(string noteText)
{
    if (noteText.Contains("CPAP", StringComparison.OrdinalIgnoreCase)) 
        return "CPAP";
    else if (noteText.Contains("oxygen", StringComparison.OrdinalIgnoreCase)) 
        return "Oxygen Tank";
    else if (noteText.Contains("wheelchair", StringComparison.OrdinalIgnoreCase)) 
        return "Wheelchair";
    else if (noteText.Contains("glucose", StringComparison.OrdinalIgnoreCase)) 
        return "Blood Glucose Monitor";  // 👈 Add this line
        
    return "Unknown";
}
```

### 2. Add Device-Specific Logic

Add parsing logic for device-specific attributes:

```csharp
private static DeviceOrder ParseDeviceSpecificDetails(string deviceType, string noteText)
{
    var order = new DeviceOrder { DeviceType = deviceType };
    
    switch (deviceType)
    {
        case "CPAP":
            // Existing CPAP logic...
            break;
            
        case "Blood Glucose Monitor":  // 👈 Add this case
            order.AddOns = ExtractGlucoseMonitorFeatures(noteText);
            order.Qualifier = ExtractTestingFrequency(noteText);
            break;
    }
    
    return order;
}
```

### 3. Write Tests

Always add tests for new device types:

```csharp
[Fact]
public void ParseNote_BloodGlucoseMonitor_ReturnsCorrectOrder()
{
    // Arrange
    var note = new PhysicianNote 
    { 
        Content = "Patient needs glucose monitor with test strips. Check 4x daily." 
    };
    
    // Act
    var result = _noteParser.ParseNote(note);
    
    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.DeviceType.Should().Be("Blood Glucose Monitor");
}
```

## 🔍 Error Handling

The app uses the **Result pattern** for clean error handling:

```csharp
// Good ✅
public Result<DeviceOrder> ProcessOrder(string input)
{
    if (string.IsNullOrEmpty(input))
        return Result<DeviceOrder>.Failure(ValidationErrors.EmptyInput);
        
    var order = ParseOrder(input);
    return Result<DeviceOrder>.Success(order);
}

// Avoid ❌
public DeviceOrder ProcessOrder(string input)
{
    if (string.IsNullOrEmpty(input))
        throw new ArgumentException("Input cannot be empty");  // Don't throw!
}
```

## 📝 Validation Rules

Use FluentValidation for all input validation:

```csharp
public class ProcessClaimValidator : AbstractValidator<ProcessClaimRequest>
{
    public ProcessClaimValidator()
    {
        RuleFor(x => x.ClaimId)
            .NotEmpty()
            .WithMessage("Claim ID is required");
            
        RuleFor(x => x.PatientId)
            .NotEmpty()
            .Must(BeValidPatientId)
            .WithMessage("Invalid patient ID format");
    }
}
```

## 🧪 Testing Guidelines

### Unit Test Structure
```csharp
[Fact]
public void MethodName_Scenario_ExpectedResult()
{
    // Arrange
    var input = CreateTestInput();
    
    // Act  
    var result = _systemUnderTest.Method(input);
    
    // Assert
    result.Should().NotBeNull();
    result.Value.Should().Be(expectedValue);
}
```

### Test Categories
- **Services**: Test business logic in isolation
- **Validators**: Test all validation rules
- **Features**: Test end-to-end feature workflows
- **Integration**: Test external dependencies

## 🚀 Running the Application

### Development
```bash
# Build the solution
dotnet build

# Run tests
dotnet test

# Run the application
dotnet run --project src/SignalBooster.Core

# With parameters
dotnet run --project src/SignalBooster.Core -- "physician_note.txt" --save-output
```

### Configuration

Edit `appsettings.json` for environment-specific settings:

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
    }
  }
}
```

## 🔧 Common Patterns

### Service Registration
```csharp
// Always use interfaces for dependency injection
services.AddScoped<IMyService, MyService>();
```

### Async Operations  
```csharp
// Always use async/await for I/O operations
public async Task<Result<T>> ProcessAsync(Request request)
{
    var data = await _httpClient.GetAsync(url);
    return Result<T>.Success(data);
}
```

### Logging
```csharp
// Use structured logging
_logger.LogInformation("Processing order {OrderId} for device {DeviceType}", 
    order.Id, order.DeviceType);
```

## 🆘 Getting Help

- **Architecture questions**: Review existing Features/ for patterns
- **Adding services**: Check Services/ folder and Program.cs registration
- **Validation**: See Validation/ folder for FluentValidation examples  
- **Testing**: Look at existing tests in tests/ folder
- **Configuration**: Check appsettings.json and Configuration/ folder

Remember: **Follow existing patterns** - the codebase is designed to be consistent and predictable!