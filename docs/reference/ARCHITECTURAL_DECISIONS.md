# 🏗️ SignalBooster MVP - Architectural Decision Records (ADRs)

## Overview

This document captures the key architectural decisions made during the development of SignalBooster MVP, including the rationale, alternatives considered, and trade-offs evaluated.

---

## 📁 **ADR-001: Flat Services Folder Structure**

**Status:** ✅ Accepted  
**Date:** September 11, 2025  
**Context:** How to organize service classes in the Services/ directory

### Decision
Keep all services in a flat `Services/` folder structure without subfolders.

### Rationale
- **Scale-appropriate**: Only 7 files (4 services + 3 interfaces) - well within flat folder sweet spot
- **Simple discovery**: Easy to find any service quickly without deep navigation
- **Reduced complexity**: Avoids over-engineering for current MVP scope
- **IDE-friendly**: Modern editors handle flat structures efficiently with search/navigation

### Current Structure
```
Services/
├── ApiClient.cs              (68 lines)
├── DeviceExtractor.cs        (305 lines) 
├── TextParser.cs             (335 lines)
├── FileReader.cs             (49 lines)
├── IApiClient.cs             (7 lines)
├── IDeviceExtractor.cs       (8 lines)
└── ITextParser.cs            (5 lines)
```

### Alternatives Considered
**Option A: Domain-based subfolders**
```
Services/
├── Processing/
│   ├── DeviceExtractor.cs
│   └── TextParser.cs
├── Integration/
│   └── ApiClient.cs  
└── Infrastructure/
    └── FileReader.cs
```
**Rejected because:** Adds unnecessary nesting for small service count

**Option B: Service-per-folder**
```
Services/
├── DeviceExtractor/
│   ├── DeviceExtractor.cs
│   └── IDeviceExtractor.cs
└── TextParser/
    ├── TextParser.cs
    └── ITextParser.cs
```
**Rejected because:** Over-engineering for single-class services

### Decision Criteria
- ✅ **Discoverability**: Can developers find files quickly?
- ✅ **Maintainability**: Easy to navigate and understand?
- ✅ **Scale appropriateness**: Fits current team and codebase size?
- ✅ **Future flexibility**: Can evolve without major refactoring?

### Review Trigger
**Reconsider when:**
- 10+ service files in folder
- Individual services exceed 500 lines
- Clear domain boundaries emerge requiring separation
- Team size grows requiring clearer ownership

---

## 🏛️ **ADR-002: Organized Service Architecture vs Clean Architecture**

**Status:** ✅ Accepted  
**Date:** September 11, 2025  
**Context:** Overall application architecture pattern choice

### Decision
Use "Organized Service Architecture" - a pragmatic layered service approach rather than full Clean Architecture.

### Rationale
- **MVP-appropriate**: Balances structure with simplicity
- **Honest naming**: Avoids claiming "Clean Architecture" when it's not true Clean Architecture
- **SOLID compliance**: Maintains good principles without over-abstraction
- **Pragmatic**: Services contain both business logic and infrastructure concerns appropriately

### Architecture Characteristics
```
├── Models/                    # Domain entities (records)
├── Services/                  # Business logic + infrastructure services
├── Configuration/             # Strongly-typed settings
└── Program.cs                # Composition root
```

**What it IS:**
- ✅ SOLID principles applied
- ✅ Dependency injection throughout
- ✅ Interface-based abstractions
- ✅ Separation of concerns by service responsibility
- ✅ Testable design with mockable dependencies

**What it is NOT:**
- ❌ True Clean Architecture (no Application/Domain/Infrastructure layers)
- ❌ Onion Architecture (no strict dependency inversion layers)
- ❌ Hexagonal Architecture (no port/adapter abstractions)

### Alternatives Considered
**Option A: Full Clean Architecture**
```
├── Domain/
│   ├── Entities/
│   ├── ValueObjects/
│   └── Repositories/
├── Application/
│   ├── UseCases/
│   ├── Interfaces/
│   └── Services/
├── Infrastructure/
│   ├── Persistence/
│   ├── ExternalServices/
│   └── Configuration/
└── Presentation/
```
**Rejected because:** Over-engineering for MVP scope, adds complexity without benefit

**Option B: Monolithic single file**
**Rejected because:** Poor maintainability and testability

**Option C: MVC pattern**
**Rejected because:** Not suitable for console application architecture

### Decision Criteria
- ✅ **Appropriate complexity**: Matches problem domain and team size
- ✅ **Testability**: Easy to unit test and mock dependencies  
- ✅ **Maintainability**: Clear responsibilities and easy to modify
- ✅ **SOLID compliance**: Good software engineering principles
- ✅ **Honest representation**: Accurately describes what we built

---

## 📦 **ADR-003: Record Types for Domain Models**

**Status:** ✅ Accepted  
**Date:** September 11, 2025  
**Context:** Data structure choice for DeviceOrder domain model

### Decision
Use C# 9+ record types for domain models instead of traditional classes.

### Rationale
- **Immutability**: Records are immutable by default, preventing accidental modification
- **Value equality**: Automatic implementation of value-based equality comparison
- **Thread safety**: Immutable objects are inherently thread-safe
- **Concise syntax**: Less boilerplate code than traditional classes
- **Modern C#**: Leverages latest language features appropriately

### Implementation
```csharp
public record DeviceOrder
{
    public string Device { get; init; } = string.Empty;
    public string? Liters { get; init; }
    public string? Usage { get; init; }
    // ... other properties with init accessors
}
```

### Alternatives Considered
**Option A: Traditional class with properties**
```csharp
public class DeviceOrder
{
    public string Device { get; set; } = string.Empty;
    // Mutable, requires manual equality implementation
}
```
**Rejected because:** Mutable state can lead to bugs, more boilerplate

**Option B: Immutable class with readonly fields**
```csharp
public class DeviceOrder
{
    public DeviceOrder(string device, string? liters, ...)
    {
        Device = device;
        Liters = liters;
    }
    public readonly string Device;
    public readonly string? Liters;
}
```
**Rejected because:** More verbose, less modern syntax

### Decision Criteria
- ✅ **Immutability**: Prevents accidental state changes
- ✅ **Thread safety**: Safe for concurrent operations
- ✅ **Testing**: Easier to test with value equality
- ✅ **Modern syntax**: Uses contemporary C# features
- ✅ **Domain appropriateness**: Models represent values, not entities

---

## 🔧 **ADR-004: Options Pattern for Configuration**

**Status:** ✅ Accepted  
**Date:** September 11, 2025  
**Context:** Configuration management approach

### Decision
Use strongly-typed configuration classes with the Options pattern.

### Rationale
- **Type safety**: Compile-time checking of configuration properties
- **IntelliSense**: IDE support for configuration properties
- **Validation**: Built-in validation support
- **Testability**: Easy to mock and test different configurations
- **Hierarchical**: Supports nested configuration sections

### Implementation
```csharp
public class SignalBoosterOptions
{
    public ApiOptions Api { get; set; } = new();
    public FileOptions Files { get; set; } = new();
    public OpenAiOptions OpenAI { get; set; } = new();
    
    public class ApiOptions
    {
        public string BaseUrl { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
        public int RetryCount { get; set; } = 3;
    }
    // ... other nested options
}
```

### Alternatives Considered
**Option A: Direct IConfiguration usage**
```csharp
var baseUrl = configuration["SignalBooster:Api:BaseUrl"];
```
**Rejected because:** No type safety, prone to typos, no IntelliSense

**Option B: Static configuration class**
```csharp
public static class Config
{
    public static string BaseUrl = "https://api.com";
}
```
**Rejected because:** Not configurable per environment, hard to test

### Decision Criteria
- ✅ **Type safety**: Prevents configuration errors at compile time
- ✅ **Testability**: Easy to provide test configurations
- ✅ **Environment support**: Works with appsettings.json hierarchy
- ✅ **Maintainability**: Clear structure and documentation
- ✅ **Validation**: Can add data annotations for validation

---

## 🧪 **ADR-005: Golden Master Testing Approach**

**Status:** ✅ Accepted  
**Date:** September 11, 2025  
**Context:** Testing strategy for regression detection

### Decision
Implement Golden Master Testing by comparing actual vs expected JSON outputs.

### Rationale
- **Regression detection**: Automatically catches any changes in output format
- **Comprehensive coverage**: Tests entire pipeline end-to-end
- **CI/CD integration**: Fails builds when outputs change unexpectedly
- **Maintainable**: Easy to add new test cases by adding input/expected files
- **Real-world validation**: Uses actual input files similar to production

### Implementation Structure
```
tests/
├── test_notes/                # Input files
│   ├── physician_note1.txt
│   ├── hospital_bed_test.txt
│   └── ...
├── test_outputs/              # Expected & actual outputs  
│   ├── physician_note1_expected.json
│   ├── physician_note1_actual.json
│   └── ...
└── run-integration-tests.sh   # Test automation
```

### Alternatives Considered
**Option A: Unit tests only**
**Rejected because:** Doesn't test integration between components or actual file I/O

**Option B: Property-based testing**
**Rejected because:** Complex to implement for NLP/extraction domain

**Option C: Manual testing only**
**Rejected because:** Not scalable, prone to human error, no CI/CD integration

### Decision Criteria
- ✅ **Regression detection**: Catches unintended changes automatically
- ✅ **End-to-end coverage**: Tests complete workflows  
- ✅ **Maintainability**: Easy to add new test cases
- ✅ **CI/CD integration**: Automated quality gates
- ✅ **Real-world relevance**: Uses realistic input data

---

## 🎯 **ADR-006: Graceful Degradation Strategy**

**Status:** ✅ Accepted  
**Date:** September 11, 2025  
**Context:** Handling external dependencies and failures

### Decision
Implement graceful degradation where external service failures don't crash the application.

### Rationale
- **Reliability**: Application continues working when external services fail
- **User experience**: Processing continues even with API/LLM failures
- **Production readiness**: Handles real-world network issues gracefully
- **Flexibility**: Works with or without OpenAI API keys

### Implementation Examples
```csharp
// LLM with regex fallback
try 
{
    result = await openAiClient.ExtractAsync(text);
}
catch (Exception ex)
{
    logger.LogWarning("LLM extraction failed, using regex fallback");
    result = regexParser.Extract(text);
}

// API posting with warning, not failure
try
{
    await apiClient.PostAsync(order);
}
catch (Exception ex)
{
    logger.LogWarning("API posting failed, continuing processing");
    // Continue execution
}
```

### Alternatives Considered
**Option A: Fail fast on any error**
**Rejected because:** Would make application unreliable in production

**Option B: Retry indefinitely**
**Rejected because:** Could cause infinite loops or excessive delays

**Option C: Queue failed requests**
**Rejected because:** Adds complexity not needed for MVP

### Decision Criteria
- ✅ **Reliability**: Application remains functional despite failures
- ✅ **User experience**: Processing continues with fallback options
- ✅ **Observability**: Failures are logged but don't crash application
- ✅ **Production readiness**: Handles real-world networking issues
- ✅ **Simplicity**: Straightforward error handling strategy

---

## 📝 **ADR-007: Comprehensive Logging Strategy**

**Status:** ✅ Accepted  
**Date:** September 11, 2025  
**Context:** Observability and debugging approach

### Decision
Implement structured logging with Serilog, including correlation IDs and step-by-step processing logs.

### Rationale
- **Production observability**: Essential for monitoring and troubleshooting
- **Correlation tracking**: Can trace individual requests end-to-end
- **Performance monitoring**: Duration tracking for optimization
- **Business intelligence**: Metrics on device types, success rates, etc.
- **Debugging support**: Detailed context for issue resolution

### Implementation Characteristics
```csharp
// Correlation ID for request tracking
var correlationId = Guid.NewGuid();

// Step-by-step logging with context
logger.LogInformation(
    "[{ServiceName}.{MethodName}] Step {Step}: {Message}. " +
    "Device: {Device}, Patient: {Patient}, Duration: {Duration}ms",
    nameof(DeviceExtractor), nameof(ProcessNoteAsync), 3,
    "Device order extracted successfully",
    order.Device, order.PatientName, stopwatch.ElapsedMilliseconds);
```

### Alternatives Considered
**Option A: Minimal logging only**
**Rejected because:** Insufficient for production debugging and monitoring

**Option B: Console.WriteLine debugging**
**Rejected because:** Not structured, not configurable, not production-ready

**Option C: Third-party APM only**
**Rejected because:** Adds external dependency and cost

### Decision Criteria
- ✅ **Production readiness**: Supports monitoring and alerting
- ✅ **Troubleshooting**: Provides context for debugging issues
- ✅ **Performance monitoring**: Tracks timing and bottlenecks
- ✅ **Business intelligence**: Enables analytics on usage patterns
- ✅ **Correlation**: Can trace requests across service boundaries

---

## 🔄 **ADR-008: Batch Processing Mode Design**

**Status:** ✅ Accepted  
**Date:** September 11, 2025  
**Context:** How to handle multiple file processing efficiently

### Decision
Implement configurable batch processing mode that processes all files in a directory automatically.

### Rationale
- **Testing efficiency**: Process all test files in one operation
- **Production scalability**: Handle multiple files without manual intervention
- **Flexible operation**: Can run single files or batch mode as needed
- **Clean execution**: Optionally cleanup previous results for fresh runs

### Implementation Features
```json
{
  "Files": {
    "BatchProcessingMode": true,
    "BatchInputDirectory": "../tests/test_notes",
    "BatchOutputDirectory": "../tests/test_outputs", 
    "CleanupActualFiles": true
  }
}
```

### Alternatives Considered
**Option A: Single file processing only**
**Rejected because:** Inefficient for testing and production bulk operations

**Option B: Always batch mode**
**Rejected because:** Reduces flexibility for single file use cases

**Option C: Separate batch application**
**Rejected because:** Increases complexity and maintenance burden

### Decision Criteria
- ✅ **Flexibility**: Supports both single file and batch scenarios
- ✅ **Testing efficiency**: Enables comprehensive test automation
- ✅ **Production readiness**: Handles bulk processing scenarios
- ✅ **Configurable**: Can be toggled without code changes
- ✅ **Clean operation**: Manages output files appropriately

---

## 📋 **Architecture Decision Summary**

| Decision | Status | Key Benefit | Trade-off |
|----------|--------|-------------|-----------|
| Flat Services Structure | ✅ Accepted | Simplicity & Discoverability | May need refactoring if services grow |
| Organized Service Architecture | ✅ Accepted | Appropriate complexity | Not "textbook" Clean Architecture |
| Record Types | ✅ Accepted | Immutability & Thread Safety | Less familiar to some developers |
| Options Pattern | ✅ Accepted | Type Safety & Validation | More initial setup |
| Golden Master Testing | ✅ Accepted | Regression Detection | Requires expected file maintenance |
| Graceful Degradation | ✅ Accepted | Reliability & User Experience | May mask some failures |
| Comprehensive Logging | ✅ Accepted | Production Observability | Increases log volume |
| Batch Processing Mode | ✅ Accepted | Testing & Production Efficiency | Additional configuration complexity |

---

## 🔮 **Future Architecture Considerations**

### When to Revisit Decisions

**Services Structure (ADR-001):**
- Refactor when 10+ services or clear domain boundaries emerge
- Consider subfolder organization for larger teams

**Architecture Pattern (ADR-002):**
- Evaluate Clean Architecture when business logic becomes more complex
- Consider Domain-Driven Design for complex business rules

**Technology Choices:**
- Consider database persistence for audit trails and bulk storage
- Evaluate message queues for high-volume asynchronous processing
- Consider microservices when team size or deployment requirements change

### Architectural Evolution Path
1. **Current**: MVP with organized services
2. **Phase 2**: Add persistence layer and caching
3. **Phase 3**: Consider event-driven architecture for scalability
4. **Phase 4**: Evaluate microservices for team independence

---

*Last Updated: September 11, 2025*  
*Next Review: When significant architectural changes are proposed*