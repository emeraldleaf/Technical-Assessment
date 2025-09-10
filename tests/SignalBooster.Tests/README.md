# SignalBooster.Tests Documentation

## Overview

The SignalBooster.Tests project provides comprehensive unit testing for the Signal Booster DME (Durable Medical Equipment) processing application. This test suite ensures the reliability and accuracy of healthcare data processing, physician note parsing, and device order generation.

## Test Architecture

### Testing Stack
- **Framework**: xUnit 2.5.3
- **Assertions**: FluentAssertions 6.12.0
- **Mocking**: NSubstitute 5.3.0  
- **Coverage**: Coverlet.collector 6.0.0
- **Target**: .NET 8.0

### Project Structure
```
tests/SignalBooster.Tests/
├── Common/           # Shared utilities and result pattern tests
├── Features/         # Application feature integration tests
├── Services/         # Infrastructure service tests
├── Validation/       # Domain validation logic tests
└── SignalBooster.Tests.csproj
```

## Test Categories

### 1. **Common Tests** (`Common/`)

#### `ResultTests.cs`
Tests the Result pattern implementation used throughout the application for error handling.

**Key Test Scenarios:**
- Success result creation and properties
- Failure result creation with error messages
- Result pattern value extraction
- Implicit conversion operations

**Purpose**: Ensures robust error handling across the entire application.

---

### 2. **Feature Tests** (`Features/`)

#### `ProcessNoteHandlerTests.cs`
Integration tests for the main application workflow that processes physician notes end-to-end.

**Key Test Scenarios:**
- ✅ **Valid Request Processing**: Complete workflow from note file to device order
- ⚠️ **File Not Found Handling**: Error handling for missing physician note files
- 🔄 **API Integration**: Mock external API calls for device order submission
- 🛡️ **Validation Integration**: Request validation and error propagation
- 📊 **Response Formatting**: Proper API response structure and data mapping

**Mocked Dependencies:**
- `IFileService` - File system operations
- `INoteParser` - Physician note parsing logic
- `IApiService` - External API communications
- `IValidator<ProcessNoteRequest>` - Request validation

**Purpose**: Validates the complete DME processing pipeline works correctly.

---

### 3. **Service Tests** (`Services/`)

#### `ApiServiceTests.cs`
Tests external API integration for submitting device orders to healthcare systems.

**Key Test Scenarios:**
- HTTP client configuration and setup
- API request formatting and serialization
- Response handling and deserialization
- Error handling for network failures
- Retry logic and timeout handling

**Healthcare Context**: Ensures reliable communication with DME ordering systems.

#### `FileServiceTests.cs`
Tests file system operations for reading physician notes and writing output files.

**Key Test Scenarios:**
- ✅ **File Reading**: Physician note file parsing with encoding detection
- 📁 **Path Validation**: Cross-platform file path handling
- 🔒 **Security Checks**: File extension validation and security constraints
- 📝 **Output Generation**: JSON output file creation and formatting
- ⚠️ **Error Handling**: File not found, permissions, and I/O errors

**Healthcare Context**: Critical for processing sensitive medical documents securely.

#### `NoteParserTests.cs`
Tests the core logic for parsing physician notes and extracting medical device information.

**Key Test Scenarios:**
- 🏥 **Medical Device Detection**: Parsing CPAP, oxygen therapy, wheelchair orders
- 👨‍⚕️ **Provider Extraction**: Identifying ordering physicians and medical professionals
- 📅 **Date Parsing**: Medical order dates and prescription timestamps
- 🔍 **Diagnostic Information**: Extracting medical conditions and justifications
- ✅ **Validation Integration**: Ensuring parsed data meets healthcare standards

**Mocked Dependencies:**
- `IValidator<PhysicianNote>` - Medical note validation rules
- `IValidator<DeviceOrder>` - Device order validation rules
- `ILogger<NoteParser>` - Structured logging for audit trails

**Purpose**: Core business logic for converting free-text medical notes into structured orders.

---

### 4. **Validation Tests** (`Validation/`)

#### `DeviceOrderValidatorTests.cs`
Comprehensive validation testing for generated medical device orders.

**Key Test Scenarios:**
- 📋 **Required Fields**: Patient name, device type, provider information
- 📅 **Date Validation**: Order dates, prescription dates, delivery requirements
- 🏥 **Provider Validation**: Medical professional licensing and credentials
- 🔢 **Quantity Validation**: Device quantities and prescription limits
- 💰 **Insurance Validation**: Coverage requirements and authorization codes
- 🏠 **Delivery Validation**: Patient addresses and shipping requirements

**Healthcare Compliance**: Ensures orders meet DME billing and regulatory requirements.

#### `PhysicianNoteValidatorTests.cs`
Validation testing for physician notes before processing.

**Key Test Scenarios:**
- 📝 **Required Content**: Minimum medical information requirements
- 👨‍⚕️ **Provider Information**: Valid physician identification
- 📅 **Date Validation**: Note creation and medical order dates
- 🏥 **Medical Necessity**: Diagnostic information and justification
- 📋 **Format Validation**: Note structure and required medical elements

**Healthcare Compliance**: Ensures notes contain sufficient information for DME authorization.

---

## Test Data Patterns

### Healthcare Test Data
The tests use realistic but anonymized healthcare scenarios:

```csharp
// Example test physician note
"Patient needs CPAP therapy for sleep apnea. Prescribed by Dr. Smith."

// Example device order validation
DeviceType: "CPAP Machine"
Diagnosis: "Obstructive Sleep Apnea" 
Provider: "Dr. Jane Smith, MD"
```

### Mocking Strategy
- **External Dependencies**: All I/O operations and external APIs are mocked
- **Validation Rules**: Business rule validators are mocked to isolate unit logic
- **Logging**: All logging is mocked to focus on business logic
- **Configuration**: Settings and options are mocked for test isolation

## Running Tests

### Command Line
```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test category
dotnet test --filter "Category=Integration"

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"
```

### Visual Studio / VS Code
- Use built-in test runners
- Individual test debugging support
- Live test discovery and execution

## Test Quality Metrics

### Coverage Goals
- **Target**: >90% code coverage
- **Critical Paths**: 100% coverage for healthcare data processing
- **Validation Logic**: 100% coverage for all business rules

### Test Categories
- **Unit Tests**: Individual component behavior
- **Integration Tests**: Component interaction workflows  
- **Validation Tests**: Business rule enforcement
- **Error Handling**: Exception scenarios and edge cases

## Healthcare Testing Considerations

### Data Privacy
- ❌ **No Real PHI**: All test data uses synthetic patient information
- 🔒 **HIPAA Compliance**: Test patterns follow healthcare data protection standards
- 🛡️ **Security Testing**: File access and permission validation

### Medical Accuracy
- ✅ **Realistic Scenarios**: Tests use authentic medical device types and conditions
- 📋 **Clinical Workflows**: Test patterns match real DME ordering processes
- 🏥 **Provider Standards**: Validation rules reflect medical professional requirements

### Regulatory Compliance
- 📝 **Documentation**: Comprehensive test documentation for audits
- 🔍 **Traceability**: Clear mapping between requirements and test coverage
- ✅ **Validation**: Extensive testing of all business rules and constraints

## Continuous Integration

### Automated Testing
- Tests run on every commit
- Coverage reports generated automatically
- Failed tests block deployments
- Healthcare data validation is mandatory

### Quality Gates
- All tests must pass before merge
- Coverage thresholds must be maintained
- No critical healthcare validation failures allowed
- Performance tests for large note processing

## Contributing to Tests

### Adding New Tests
1. Follow existing naming conventions
2. Use appropriate test categories (Unit/Integration/Validation)
3. Mock all external dependencies
4. Include both positive and negative test cases
5. Add healthcare-specific edge cases

### Test Data Guidelines
- Use realistic but synthetic medical scenarios
- Follow HIPAA-compliant test data patterns
- Include edge cases for medical device types
- Test various provider and patient scenarios

---

**Healthcare Focus**: This test suite is specifically designed for medical device ordering workflows, ensuring compliance with healthcare regulations, data protection standards, and clinical accuracy requirements.
