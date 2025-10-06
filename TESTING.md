# Testing Documentation

## Overview

This document describes the testing strategy and architecture for the SignalBooster medical device extraction system. The test suite emphasizes **behavior-driven testing** over implementation testing to ensure robust, maintainable tests that verify business value.

## Testing Philosophy

### Behavior-First Testing
- **Test WHAT the system does, not HOW it does it**
- Focus on observable outcomes and user value
- Verify contracts and business requirements
- Maintain test validity across refactoring

### Anti-Patterns Avoided
- ❌ Testing implementation details (which parser was called)
- ❌ Mocking verification calls (`Received(1)`)
- ❌ Testing internal metadata structures
- ❌ Brittle tests that break on refactoring

## Test Categories

### 1. Unit Tests (`TextParserTests.cs`)
**Purpose**: Core business logic validation
- **Pattern**: Isolated component testing
- **Focus**: Input/output behavior of parsing logic
- **Coverage**: 20+ device types, edge cases, malformed input

```csharp
[Fact]
public void ParseDeviceOrder_CpapNote_ReturnsCpapOrder()
{
    // Tests: Given CPAP note → Should extract CPAP device
    // Focuses on business outcome, not regex implementation
}
```

### 2. Integration Tests (`AgenticExtractionTests.cs`)
**Purpose**: End-to-end workflow validation
- **Pattern**: Behavior-driven integration testing
- **Focus**: System contracts and cross-component behavior
- **Coverage**: Configuration modes, error handling, performance

#### Key Test Patterns:

**Contract Tests**
```csharp
[Fact]
public async Task DifferentExtractionModes_SameCpapNote_ShouldExtractSameCoreDevice()
{
    // Contract: All modes should agree on device type
    // Tests consistency across configuration variations
}
```

**Integration Tests**
```csharp
[Fact]
public async Task AgenticVsFallback_ShouldBothExtractValidResults()
{
    // Tests: Both AI and regex approaches should work
    // Verifies system reliability across different modes
}
```

**Error Handling Tests**
```csharp
[Fact]
public async Task AgenticExtractor_WithInvalidApiKey_ShouldFallbackGracefully()
{
    // Tests: System should degrade gracefully, not crash
    // Verifies robustness under failure conditions
}
```

**Performance Tests**
```csharp
[Fact]
public async Task AgenticExtractor_UnderConcurrentLoad_ShouldMaintainConsistency()
{
    // Tests: Concurrent requests should produce consistent results
    // Verifies thread safety and performance characteristics
}
```

### 3. Snapshot Regression Tests (`SnapshotRegressionTests.cs`)
**Purpose**: Change detection and output stability
- **Pattern**: Golden master testing with Verify framework
- **Focus**: Detecting unintended changes in parsing output
- **Coverage**: Real-world note formats and edge cases

```csharp
[Fact]
public async Task ProcessNote_CpapWithAccessories_MatchesSnapshot()
{
    // Tests: Parsing output should remain stable over time
    // Catches regressions in extraction accuracy
}
```

#### AI vs Regex Comparison Tests
Tests that compare AI and regex parsing results:

```csharp
ProcessNote_AIvsRegex_CPAPScenario_ComparisonSnapshot()
ProcessNote_AIvsRegex_OxygenTankScenario_ComparisonSnapshot()
```

**Purpose**: Validate that AI parsing provides enhanced extraction while maintaining accuracy

**Output**: Snapshots containing both results plus comparison metrics

#### AI Accuracy Suite
```csharp
ProcessNote_AI_RegressionSuite_AccuracySnapshot()
```

**Purpose**: Test AI consistency across multiple device types

**Coverage**: CPAP, Oxygen Tank, Hospital Bed scenarios

### 4. Property-Based Tests (`PropertyBasedTests.cs`)
**Purpose**: Edge case discovery and input validation
- **Pattern**: Generative testing with random inputs
- **Focus**: System behavior under unexpected conditions
- **Coverage**: Malformed input, boundary conditions, stress testing

```csharp
[Theory]
[InlineData("", "Empty string")]
[InlineData("   \n\t   ", "Whitespace only")]
public void ParseDeviceOrder_EdgeCases_HandlesGracefully(string input, string testCase)
{
    // Tests: System should handle edge cases without crashing
    // Verifies robustness across input variations
}
```

## Test Environment Configuration

### Local Development
```bash
# Run all tests
dotnet test

# Run specific categories
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=Regression"

# Run AI-specific tests (requires API key)
dotnet test --filter "ProcessNote_AI"
```

### CI/CD Pipeline
- **Environment**: GitHub Actions (Ubuntu)
- **API Key**: Configured via `OPENAI_API_KEY` repository secret
- **Coverage**: xPlat Code Coverage with Cobertura reports
- **Artifacts**: Test results (TRX) and coverage reports

#### Setting Up GitHub Secrets for CI/CD
To enable full test execution including AI-enhanced tests in GitHub Actions:

1. **Repository Settings**:
   - Navigate to: `Settings` → `Secrets and variables` → `Actions`

2. **Add Repository Secret**:
   ```
   Name: OPENAI_API_KEY
   Value: sk-proj-[your-openai-api-key-here]
   ```

3. **Workflow Configuration** (already configured):
   ```yaml
   env:
     OPENAI_API_KEY: ${{ secrets.OPENAI_API_KEY }}
   ```

#### Test Execution Modes in CI/CD
- **Without GitHub Secret**: ~140/143 tests pass (AI snapshot tests gracefully skip)
- **With GitHub Secret**: All 143 tests execute with full OpenAI integration
- **Security**: No API keys in code - all handled via GitHub secrets management

### API Key Handling
- **With API Key**: Tests run full AI extraction pipeline
- **Without API Key**: Tests verify graceful fallback to regex parsing
- **Snapshots**: Separate verified files for both scenarios

#### AI Parser Configuration
- **Model**: `gpt-3.5-turbo`
- **Temperature**: `0.1` (low for consistent outputs)
- **Max Tokens**: `500`
- **API Key**: Required via `OPENAI_API_KEY` environment variable

## Test Data Strategy

### Real-World Notes
- **Source**: `test_notes/` directory contains actual medical notes
- **Variety**: CPAP, oxygen tanks, hospital beds, various formats
- **Privacy**: All PHI removed, synthetic patient data used

### Test Data Factory
Tests use `TestDataFactory.PhysicianNotes` for standardized scenarios:
- `OxygenTank` - Oxygen tank prescription scenario
- `CpapWithAccessories` - CPAP with mask and accessories
- `HospitalBed` - Hospital bed rental scenario
- `SimpleCpap` - Basic CPAP prescription

### Test Builders
```csharp
// PhysicianNoteBuilder provides fluent test data creation
var note = new PhysicianNoteBuilder()
    .WithDevice("CPAP")
    .WithProvider("Dr. House")
    .WithPatient("John Doe", "01/01/1980")
    .Build();
```

## Assertion Patterns

### Behavior Assertions (✅ Preferred)
```csharp
// Test observable outcomes
Assert.Equal("CPAP", result.Device);
Assert.NotEmpty(result.OrderingProvider);
Assert.InRange(result.ConfidenceScore, 0.0, 1.0);

// Test business contracts
Assert.All(results, r => Assert.NotNull(r.DeviceOrder));

// Test system behavior under different conditions
Assert.Contains("CPAP", result.Device);
Assert.True(result.Device.Length > 0);
```

### Implementation Assertions (❌ Avoided)
```csharp
// Don't test how it works internally
await _parser.Received(1).ParseAsync();  // ❌
Assert.Contains("ExtractionMode", metadata); // ❌
Assert.True(result.Metadata.AdditionalData.ContainsKey("ExtractionMode")); // ❌
```

## Recent Test Improvements

### Refactored AgenticExtractionTests
The integration tests were refactored from implementation-focused to behavior-focused:

**Before (Implementation-Focused)**:
```csharp
// Testing which parser was called
await _fallbackParser.Received(1).ParseDeviceOrderAsync(_testNote);

// Testing internal metadata
Assert.True(result.Metadata.AdditionalData.ContainsKey("ExtractionMode"));
```

**After (Behavior-Focused)**:
```csharp
// Testing system behavior
Assert.Equal("CPAP", result.Device);
Assert.NotEmpty(result.OrderingProvider);

// Testing contracts across configurations
Assert.All(results, r => Assert.NotEmpty(r.DeviceOrder.Device));
```

### New Test Categories Added
1. **Contract Tests**: Verify consistency across different extraction modes
2. **Integration Tests**: Test end-to-end workflows with both AI and fallback
3. **Error Handling Tests**: Verify graceful degradation under failure
4. **Performance Tests**: Test concurrent execution and consistency

## Test Maintenance

### Snapshot Updates
When business logic changes require new snapshots:
```bash
# Generate new snapshots
dotnet test --logger "console;verbosity=normal"

# Review changes in .received.txt files
# Copy to .verified.txt when changes are intentional
cp *.received.txt *.verified.txt
```

### Adding New Tests
1. **Identify the behavior** to test (not implementation)
2. **Choose appropriate test category** (Unit/Integration/Regression)
3. **Write descriptive test names** that explain the expected behavior
4. **Focus on observable outcomes** and business value
5. **Ensure tests are independent** and can run in any order

### Test Naming Convention
```csharp
// Pattern: [Component]_[Scenario]_[ExpectedBehavior]
public async Task AgenticExtractor_WithValidInput_ShouldReturnValidDeviceOrder()
public void TextParser_MalformedNote_ShouldHandleGracefully()
public async Task DifferentExtractionModes_SameCpapNote_ShouldExtractSameCoreDevice()
```

## Quality Metrics

### Test Coverage Targets
- **Unit Tests**: >90% code coverage
- **Integration Tests**: >80% critical path coverage
- **Regression Tests**: 100% snapshot coverage for core scenarios

### Quality Indicators
Tests include quality metrics to track parsing effectiveness:
- **Field Count**: Number of non-null extracted fields
- **Device Accuracy**: Correct device type extraction
- **Enhanced Details**: AI-specific extractions (MaskType, Liters, Usage, etc.)
- **Consistency**: Same device type produces consistent results

### Test Health Metrics
- **Flaky Test Rate**: <1% (tests should be deterministic)
- **Build Time**: <2 minutes for full test suite
- **Failure Recovery**: Clear error messages and quick fixes

## Troubleshooting

### Common Issues

**Snapshot Mismatches**
```
VerifyException: Directory not found
```
- **Cause**: Snapshot files missing or path issues
- **Fix**: Regenerate snapshots or check file permissions

**API Key Tests Failing**
```
Message: AI tests skipped - no API key configured
```
- **Cause**: Missing `OPENAI_API_KEY` environment variable
- **Fix**: Set API key in local environment or CI secrets

**Flaky Integration Tests**
```
Intermittent failures in agentic extraction
```
- **Cause**: API rate limiting or network issues
- **Fix**: Add retry logic or use test-specific API keys

**Nullability Warnings**
```
CS8602: Dereference of a possibly null reference
```
- **Cause**: Missing null checks in test assertions
- **Fix**: Use null-conditional operators (`?.`) and proper null handling

### Debug Tips
- Use `UseParameters()` to create device-specific snapshots
- Check test output for comparison metrics
- Verify test data matches expected physician note formats
- Review `.received.txt` files when snapshots fail
- Run tests with `--verbosity normal` for detailed output

## Best Practices

### Writing Maintainable Tests
1. **Test one behavior per test method**
2. **Use descriptive assertions with clear error messages**
3. **Minimize test dependencies and shared state**
4. **Keep tests simple and focused**
5. **Use meaningful test data that reflects real scenarios**

### Mock Usage Guidelines
- **Mock external dependencies** (file system, HTTP calls)
- **Don't mock the system under test**
- **Use real objects for value types and simple data**
- **Prefer stubs over complex mock setups**

### Continuous Improvement
- **Review test failures** for patterns and root causes
- **Refactor tests** when they become brittle or unclear
- **Add tests for new bug reports** before fixing issues
- **Monitor test execution time** and optimize slow tests

## Snapshot File Structure

Snapshot files contain structured output like:

```json
[
  {
    "TestCase": "cpap.txt",
    "AIExtraction": {
      "Device": "CPAP",
      "Diagnosis": "Severe sleep apnea",
      "OrderingProvider": "Dr. Foreman",
      "PatientName": "Lisa Turner",
      "Dob": "Date_1",
      "MaskType": "full face",
      "AddOns": ["heated humidifier"],
      "Qualifier": "AHI > 20"
    },
    "QualityMetrics": {
      "ExtractedFieldCount": 8,
      "HasSpecificDetails": {
        "MaskType": true,
        "Liters": false,
        "Usage": false,
        "AddOns": true,
        "Qualifier": true
      }
    }
  }
]
```

---

*This testing strategy ensures the SignalBooster system maintains high quality while remaining adaptable to changing business requirements. The focus on behavior over implementation creates a robust test suite that provides confidence during refactoring and feature development.*