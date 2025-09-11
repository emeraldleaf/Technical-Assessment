# SignalBooster MVP - Developer Guide

## 🏗️ Architecture Overview

SignalBooster follows **organized service architecture** with clear separation of concerns, making it maintainable and testable while avoiding over-engineering for the MVP scope.

### Project Structure
```
src/
├── Models/                    # Domain entities (immutable records)
│   └── DeviceOrder.cs        # Core business model
├── Services/                 # Business logic and infrastructure
│   ├── DeviceExtractor.cs    # Main orchestration service
│   ├── TextParser.cs         # LLM + regex parsing logic
│   ├── FileReader.cs         # File I/O operations
│   └── ApiClient.cs          # External API integration
├── Configuration/            # Strongly-typed settings
│   └── SignalBoosterOptions.cs
├── test_notes/              # Test input files
├── test_outputs/            # Golden master test results
├── .github/workflows/       # CI/CD pipeline
└── run-integration-tests.sh # Test automation
```

### SOLID Principles Implementation

1. **🎯 Single Responsibility**: Each class has one clear purpose
2. **🔓 Open/Closed**: Extensible without modification
3. **🔄 Liskov Substitution**: All implementations interchangeable
4. **🧩 Interface Segregation**: Focused, minimal interfaces
5. **🔁 Dependency Inversion**: Depends on abstractions, full DI container

---

## 🔧 Adding New Features

### 1. Adding New DME Device Types

To add a new device type (e.g., "Blood Glucose Monitor"):

**Step 1: Update DetermineDeviceType method in TextParser.cs**
```csharp
private static string DetermineDeviceType(string text)
{
    // CPAP and BiPAP devices
    if (text.Contains("CPAP", StringComparison.OrdinalIgnoreCase) || 
        text.Contains("continuous positive airway pressure", StringComparison.OrdinalIgnoreCase))
        return "CPAP";
    
    // Oxygen devices
    if (text.Contains("oxygen", StringComparison.OrdinalIgnoreCase) || 
        text.Contains("O2", StringComparison.OrdinalIgnoreCase))
        return "Oxygen Tank";
    
    // Add new device type here 👇
    if (text.Contains("glucose", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("blood sugar", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("diabetes", StringComparison.OrdinalIgnoreCase))
        return "Blood Glucose Monitor";
    
    // Continue with other existing devices...
    return "Unknown";
}
```

**Step 2: Add Device-Specific Extraction Logic**
```csharp
// Add device-specific extraction in ParseDeviceOrder method
var deviceOrder = new DeviceOrder
{
    Device = device,
    OrderingProvider = orderingProvider,
    PatientName = patientName,
    Dob = dob,
    Diagnosis = diagnosis,
    // Add device-specific fields
    MaskType = ExtractMaskType(device, noteText),
    AddOns = ExtractAddOns(device, noteText),    // 👈 This handles glucose features
    Qualifier = ExtractQualifier(device, noteText),
    Liters = ExtractLiters(device, noteText),
    Usage = ExtractUsage(device, noteText)
};
```

**Step 3: Update ExtractAddOns method to handle new device**
```csharp
private static List<string>? ExtractAddOns(string deviceType, string text)
{
    var addOns = new List<string>();
    
    switch (deviceType.ToUpperInvariant())
    {
        case "CPAP":
            if (text.Contains("humidifier", StringComparison.OrdinalIgnoreCase))
                addOns.Add("humidifier");
            break;
            
        case "BLOOD GLUCOSE MONITOR":  // 👈 Add this case
            if (text.Contains("test strips", StringComparison.OrdinalIgnoreCase))
                addOns.Add("test strips");
            if (text.Contains("lancets", StringComparison.OrdinalIgnoreCase))
                addOns.Add("lancets");
            if (text.Contains("carrying case", StringComparison.OrdinalIgnoreCase))
                addOns.Add("carrying case");
            break;
    }
    
    return addOns.Any() ? addOns : null;
}
```

**Step 3: Create Test Files**
```bash
# Create test file
echo "Patient needs glucose monitor with test strips for diabetes management." > test_notes/glucose_monitor_test.txt

# Create expected output
cat > test_outputs/glucose_monitor_expected.json << EOF
{
  "device": "Blood Glucose Monitor",
  "add_ons": ["test strips"],
  "diagnosis": "diabetes"
}
EOF
```

**Step 4: Run Tests**
```bash
# Test single file
dotnet run test_notes/glucose_monitor_test.txt

# Run full regression test suite
./run-integration-tests.sh
```

### 2. Adding New Input Formats

To support additional input formats (e.g., XML):

**Step 1: Update FileReader.cs**
```csharp
public async Task<string> ReadFileAsync(string filePath)
{
    var extension = Path.GetExtension(filePath).ToLower();
    
    return extension switch
    {
        ".txt" => await File.ReadAllTextAsync(filePath),
        ".json" => ExtractJsonContent(await File.ReadAllTextAsync(filePath)),
        ".xml" => ExtractXmlContent(await File.ReadAllTextAsync(filePath)), // 👈 Add this
        _ => throw new NotSupportedException($"File type {extension} not supported")
    };
}
```

**Step 2: Update Configuration**
```json
{
  "SignalBooster": {
    "Files": {
      "SupportedExtensions": [".txt", ".json", ".xml"] // 👈 Add .xml
    }
  }
}
```

---

## 🧪 Testing Strategy

### Golden Master Testing Framework

The project uses **Golden Master Testing** for comprehensive regression detection:

```bash
# Run all tests with regression detection
./run-integration-tests.sh

# Options available:
./run-integration-tests.sh --batch-only    # Batch processing only
./run-integration-tests.sh --skip-batch    # Unit tests only  
./run-integration-tests.sh --verbose       # Detailed output
```

### Test File Structure
- **Input Files**: `test_notes/*.{txt,json}` - Test cases for processing
- **Expected Outputs**: `test_outputs/*_expected.json` - Golden master baselines
- **Actual Outputs**: `test_outputs/*_actual.json` - Generated during testing

### Adding New Test Cases

1. **Create Input File**:
   ```bash
   echo "Your test content here" > test_notes/new_device_test.txt
   ```

2. **Generate Expected Output**:
   ```bash
   # Process file to get actual output
   dotnet run test_notes/new_device_test.txt
   
   # Copy to expected baseline (after verifying correctness)
   cp output.json test_outputs/new_device_expected.json
   ```

3. **Run Regression Tests**:
   ```bash
   ./run-integration-tests.sh
   ```

---

## ⚙️ Configuration Management

### Hierarchical Configuration System

```
appsettings.json              # Base configuration
appsettings.Development.json  # Development overrides
appsettings.Production.json   # Production overrides  
appsettings.Local.json       # Local dev settings (git-ignored)
```

### Environment Variables
Override any setting using `SIGNALBOOSTER_` prefix:
```bash
export SIGNALBOOSTER_SignalBooster__OpenAI__ApiKey="sk-your-key"
export SIGNALBOOSTER_ApplicationInsights__ConnectionString="your-connection"
```

### Strongly-Typed Configuration

All settings use strongly-typed classes in `Configuration/SignalBoosterOptions.cs`:

```csharp
public class SignalBoosterOptions
{
    public ApiOptions Api { get; set; } = new();
    public FileOptions Files { get; set; } = new();
    public OpenAIOptions OpenAI { get; set; } = new();
}
```

---

## 🤖 LLM Integration

### OpenAI Integration Architecture

The system uses a **fallback strategy**:
1. **Primary**: OpenAI LLM for high accuracy extraction
2. **Fallback**: Regex parsing for reliability when LLM fails

### Configuring LLM Settings

```json
{
  "SignalBooster": {
    "OpenAI": {
      "ApiKey": "",                    // Set in appsettings.Local.json
      "Model": "gpt-3.5-turbo",       // or "gpt-4"
      "MaxTokens": 1000,              // Response limit
      "Temperature": 0.1              // Low for consistent results
    }
  }
}
```

### Adding Custom LLM Prompts

Edit the `CreateExtractionPrompt` method in `TextParser.cs` to customize prompts:

```csharp
private string CreateExtractionPrompt(string noteText)
{
    return $"""
Extract DME (Durable Medical Equipment) device order information from this physician note and return valid JSON.

Physician Note:
{noteText}

Required JSON structure:
{{
  "device": "string - Primary device type (CPAP, Oxygen Tank, Wheelchair, etc.)",
  "ordering_provider": "string - Provider name", 
  "patient_name": "string - Patient name",
  "dob": "string - Date of birth (MM/DD/YYYY format)",
  "diagnosis": "string - Medical diagnosis",
  "liters": "string - For oxygen (""2 L"")",
  "usage": "string - When used (""sleep and exertion"")",
  "mask_type": "string - For CPAP (""full face"")",
  "add_ons": ["array", "of", "features"],
  "qualifier": "string - Medical qualifiers (""AHI > 20"")"
}}

Rules:
- Return only valid JSON, no explanations
- Use null for missing information  
- Be precise and factual
- Follow medical terminology standards
""";
}
```

---

## 🔄 Batch Processing

### Enabling Batch Mode

Set `"BatchProcessingMode": true` in appsettings.json:

```json
{
  "SignalBooster": {
    "Files": {
      "BatchProcessingMode": true,
      "BatchInputDirectory": "test_notes",
      "BatchOutputDirectory": "test_outputs",
      "CleanupActualFiles": true  // Delete previous results
    }
  }
}
```

### Batch Processing Flow

1. **File Discovery**: Scans `BatchInputDirectory` for supported files
2. **Processing**: Processes each file individually 
3. **Output Generation**: Creates `*_actual.json` files in `BatchOutputDirectory`
4. **Error Handling**: Continues processing on individual failures
5. **Cleanup**: Optionally removes previous actual files

---

## 📊 Monitoring & Observability

### Structured Logging with Serilog

All services use structured logging with correlation IDs:

```csharp
_logger.LogInformation("Processing file {FilePath} with correlation {CorrelationId}",
    filePath, correlationId);
```

### Application Insights Integration

Key telemetry captured:
- Processing durations and performance metrics
- Error rates and failure patterns
- LLM vs regex usage statistics
- Device type distribution analytics

Use queries from `../SignalBooster-Queries.kql` for monitoring.

---

## 🚀 Deployment

### Local Development

```bash
# Prerequisites: .NET 8.0 SDK
dotnet --version

# Build and run
cd src
dotnet restore
dotnet build
dotnet run

# With custom file
dotnet run test_notes/physician_note1.txt
```

### Docker Deployment

```bash
# Build image
docker build -t signalbooster-mvp .

# Run container
docker run -p 8080:80 signalbooster-mvp
```

### CI/CD Pipeline

GitHub Actions workflow (`.github/workflows/ci.yml`) provides:
- ✅ Automated integration testing
- ✅ Build validation and packaging
- ✅ Quality gates and regression detection
- ✅ Deployment readiness verification

---

## 🛠️ Development Patterns

### Error Handling Strategy

The application uses graceful degradation with LLM fallback:

```csharp
public DeviceOrder ParseDeviceOrder(string noteText)
{
    // Always try LLM first if available
    if (!string.IsNullOrEmpty(_options.ApiKey))
    {
        try
        {
            return await ParseDeviceOrderAsync(noteText);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{Class}.{Method}] LLM extraction failed, falling back to regex parsing", 
                nameof(TextParser), nameof(ParseDeviceOrder));
        }
    }
    
    // Fallback to regex-based parsing
    _logger.LogInformation("[{Class}.{Method}] Using regex-based device extraction", 
        nameof(TextParser), nameof(ParseDeviceOrder));
        
    var device = DetermineDeviceType(noteText);
    var orderingProvider = ExtractOrderingProvider(noteText);
    // ... rest of regex extraction logic
    
    return new DeviceOrder
    {
        Device = device,
        OrderingProvider = orderingProvider,
        // ... other fields
    };
}
```

### Dependency Injection Pattern

All services are registered in `Program.cs`:

```csharp
// Configuration
services.Configure<SignalBoosterOptions>(configuration.GetSection("SignalBooster"));

// Services with interfaces for testability
services.AddScoped<IFileReader, FileReader>();
services.AddScoped<ITextParser, TextParser>(); 
services.AddScoped<IApiClient, ApiClient>();
services.AddScoped<DeviceExtractor>();

// HTTP client for API calls
services.AddHttpClient<IApiClient, ApiClient>();
```

### Async/Await Best Practices

```csharp
// Good ✅ - Proper async implementation
public async Task<DeviceOrder> ProcessFileAsync(string filePath)
{
    var content = await _fileReader.ReadFileAsync(filePath);
    var order = await _textParser.ParseAsync(content);
    await _apiClient.SubmitAsync(order);
    return order;
}

// Avoid ❌ - Blocking async calls
public DeviceOrder ProcessFile(string filePath)
{
    var content = _fileReader.ReadFileAsync(filePath).Result; // Don't do this!
}
```

---

## 📝 Code Style Guidelines

### Naming Conventions

- **Classes**: PascalCase (`DeviceExtractor`)
- **Methods**: PascalCase (`ExtractDeviceOrder`)
- **Properties**: PascalCase (`DeviceType`)
- **Fields**: camelCase with underscore (`_logger`)
- **Constants**: PascalCase (`MaxRetryAttempts`)

### Documentation Standards

Use XML documentation for all public APIs:

```csharp
/// <summary>
/// Extracts DME device order information from physician notes
/// </summary>
/// <param name="noteText">The physician note content to process</param>
/// <returns>Structured device order information</returns>
/// <exception cref="ArgumentException">Thrown when noteText is empty</exception>
public async Task<DeviceOrder> ExtractAsync(string noteText)
```

### Testing Standards

Follow AAA pattern (Arrange, Act, Assert):

```csharp
[Fact]
public async Task ExtractDeviceOrder_OxygenTankNote_ReturnsCorrectDevice()
{
    // Arrange
    var noteText = "Patient requires oxygen tank with 2L flow rate";
    var extractor = CreateExtractor();
    
    // Act
    var result = await extractor.ExtractAsync(noteText);
    
    // Assert
    result.Device.Should().Be("Oxygen Tank");
    result.Liters.Should().Be("2 L");
}
```

---

## 🆘 Troubleshooting

### Common Issues

**1. LLM API Failures**
- Check API key configuration in `appsettings.Local.json`
- Monitor quota usage in OpenAI dashboard
- Application automatically falls back to regex

**2. Test Failures**
- Run `./run-integration-tests.sh --verbose` for detailed output
- Check if actual outputs match expected baselines
- Verify test files exist in `test_notes/`

**3. Build Errors**
- Ensure .NET 8.0 SDK is installed
- Run `dotnet restore` to install packages
- Check for missing using statements

### Debugging Tips

1. **Enable verbose logging**: Set log level to `Debug` in appsettings.json
2. **Use correlation IDs**: Track requests end-to-end in logs
3. **Check Application Insights**: Use KQL queries for production issues
4. **Golden Master Diffs**: Compare actual vs expected outputs for test failures

---

## 📚 Additional Resources

- **Architecture**: Review `Models/` and `Services/` folders for patterns
- **Configuration**: Check `Configuration/SignalBoosterOptions.cs`
- **Testing**: See `run-integration-tests.sh` and test file structure
- **Monitoring**: Use `../SignalBooster-Queries.kql` for Application Insights
- **CI/CD**: Review `.github/workflows/ci.yml`

---

**🎯 Ready to Contribute!** This guide provides everything needed to understand, extend, and maintain the SignalBooster MVP codebase.