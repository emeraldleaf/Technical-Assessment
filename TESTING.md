# Testing Documentation

This document explains the testing strategy and implementation for the SignalBooster medical device extraction system.

## Test Architecture

### Test Categories

1. **Unit Tests** - Test individual components in isolation
2. **Integration Tests** - Test component interactions
3. **Snapshot Regression Tests** - Detect changes in parsing outputs
4. **AI Comparison Tests** - Compare AI vs regex parsing results

## Snapshot Regression Tests

The core testing strategy uses the **Verify framework** for snapshot-based regression testing.

### How Snapshot Tests Work

1. **First Run**: Creates baseline snapshots stored as `.verified.txt` files
2. **Subsequent Runs**: Compares current output against approved snapshots
3. **Change Detection**: Any differences cause test failures requiring manual review
4. **Approval Process**: New snapshots must be manually approved to update baselines

### Test Structure

```csharp
[UsesVerify]
[Trait("Category", "Regression")]
public class SnapshotRegressionTests
```

The test class uses two parser configurations:

- **Regex Parser**: Baseline regex-only parsing for consistency testing
- **AI Parser**: OpenAI-enabled parser for enhanced extraction (requires API key)

### AI Testing Strategy

#### 1. AI vs Regex Comparison Tests

Tests that compare AI and regex parsing results:

```csharp
ProcessNote_AIvsRegex_CPAPScenario_ComparisonSnapshot()
ProcessNote_AIvsRegex_OxygenTankScenario_ComparisonSnapshot()
```

**Purpose**: Validate that AI parsing provides enhanced extraction while maintaining accuracy

**Output**: Snapshots containing both results plus comparison metrics

#### 2. AI Accuracy Suite

```csharp
ProcessNote_AI_RegressionSuite_AccuracySnapshot()
```

**Purpose**: Test AI consistency across multiple device types

**Coverage**: CPAP, Oxygen Tank, Hospital Bed scenarios

#### 3. Device-Specific Consistency Tests

```csharp
ProcessNote_AI_DeviceSpecific_ConsistencySnapshot(string deviceType)
```

**Purpose**: Ensure AI parsing remains consistent for the same device type with variations

**Method**: Tests multiple note variations for each device type

### Configuration

#### AI Parser Setup
- **Model**: `gpt-3.5-turbo`
- **Temperature**: `0.1` (low for consistent outputs)
- **Max Tokens**: `500`
- **API Key**: Required via `OPENAI_API_KEY` environment variable

#### Fallback Behavior
When no API key is configured, AI tests create "no API key" snapshots to maintain test structure.

### Test Data

Tests use `TestDataFactory.PhysicianNotes` for standardized test scenarios:
- `OxygenTank` - Oxygen tank prescription scenario
- `CpapWithAccessories` - CPAP with mask and accessories
- `HospitalBed` - Hospital bed rental scenario
- `SimpleCpap` - Basic CPAP prescription

### Snapshot File Structure

Snapshot files contain structured output like:

```json
[
  {
    "ExtractedDevice": "Oxygen Tank",
    "ConsistencyMarkers": {
      "DeviceCorrect": true,
      "HasPatientInfo": true,
      "HasProvider": true
    }
  }
]
```

### Running Tests

#### Prerequisites
- .NET test runner (xUnit)
- Optional: `OPENAI_API_KEY` environment variable for AI tests

#### Commands
```bash
# Run all tests
dotnet test

# Run only regression tests
dotnet test --filter "Category=Regression"

# Run specific AI tests
dotnet test --filter "ProcessNote_AI"
```

### Snapshot Management

#### Reviewing Changes
1. When tests fail, review `.received.txt` files
2. Compare against existing `.verified.txt` files
3. Approve changes by copying `.received.txt` to `.verified.txt`

#### Best Practices
- Review all snapshot changes carefully
- Understand why outputs changed before approving
- Ensure changes align with intended improvements
- Test with multiple device scenarios before approval

### Quality Metrics

Tests include quality metrics to track parsing effectiveness:

- **Field Count**: Number of non-null extracted fields
- **Device Accuracy**: Correct device type extraction
- **Enhanced Details**: AI-specific extractions (MaskType, Liters, Usage, etc.)
- **Consistency**: Same device type produces consistent results

### Troubleshooting

#### Common Issues

1. **API Key Missing**: AI tests will skip and create placeholder snapshots
2. **Snapshot Mismatches**: Review changes and approve if intentional
3. **Inconsistent AI Results**: Check temperature settings and test data variations

#### Debug Tips

- Use `UseParameters()` to create device-specific snapshots
- Check test output for comparison metrics
- Verify test data matches expected physician note formats