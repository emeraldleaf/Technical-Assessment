# SignalBooster - DME Processing Application

A production-ready application for DME (Durable Medical Equipment) device order processing that extracts structured data from physician notes using LLM integration with regex fallback.

## Quick Start

### Prerequisites
- .NET 8.0 SDK
- Optional: OpenAI API key for enhanced extraction

### Setup
```bash
# Build from root (using solution file)
dotnet build SignalBooster.sln

# Run with default batch processing
dotnet run --project src

# Process single file
dotnet run --project src ../tests/test_notes/physician_note1.txt

# Run tests
dotnet test
```

### OpenAI API Configuration (Optional)
```bash
# Copy template and add your API key
cp src/appsettings.Local.json.template src/appsettings.Local.json
# Edit appsettings.Local.json with your OpenAI API key
```

## Sample Input/Output

**Input** (`physician_note1.txt`):
```
Patient Name: Harold Finch
DOB: 04/12/1952
Diagnosis: COPD
Ordering Physician: Dr. Cuddy

Patient requires oxygen tank with 2 L flow rate for sleep and exertion.
```

**Output** (`output.json`):
```json
{
  "device": "Oxygen Tank",
  "liters": "2 L",
  "usage": "sleep and exertion", 
  "diagnosis": "COPD",
  "ordering_provider": "Dr. Cuddy",
  "patient_name": "Harold Finch",
  "dob": "04/12/1952"
}
```

## Architecture

Clean service-oriented design with dependency injection:

```
├── Models/DeviceOrder.cs           # Data structures
├── Services/
│   ├── DeviceExtractor.cs         # Main orchestration
│   ├── TextParser.cs              # LLM + regex parsing
│   ├── FileReader.cs              # File operations
│   └── ApiClient.cs               # External API calls
├── Configuration/                  # Settings
└── Program.cs                     # Application entry
```

## Key Features

- **LLM Integration**: OpenAI GPT-4o with regex fallback
- **Multiple Formats**: Supports .txt and .json input files
- **20+ Device Types**: CPAP, Oxygen, Hospital Beds, Wheelchairs, etc.
- **Batch Processing**: Process entire directories
- **Fault Tolerance**: API retry logic with exponential backoff
- **Performance Optimized**: StreamReader for large files (>1MB)
- **Comprehensive Testing**: 89 tests across 5 categories (100% pass rate)
- **Production Ready**: Structured logging, error handling, observability

## Testing

```bash
# Run all tests (89 total)
dotnet test

# Run tests with coverage (matches CI pipeline)
dotnet test --configuration Release --logger trx --collect:"XPlat Code Coverage"

# Expected: All 89 tests pass (100% success rate)

# Run by category
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration" 
dotnet test --filter "Category=Performance"
```

**Note**: Snapshot tests may create baseline files on first run for future comparisons.

## Configuration

Edit `src/appsettings.json` or create `src/appsettings.Local.json`:

```json
{
  "SignalBooster": {
    "OpenAI": {
      "ApiKey": "your-openai-api-key",
      "Model": "gpt-4o"
    },
    "Api": {
      "BaseUrl": "https://alert-api.com",
      "Endpoint": "/device-orders",
      "TimeoutSeconds": 30,
      "RetryCount": 3,
      "EnableApiPosting": true
    },
    "Files": {
      "BatchProcessingMode": true,
      "BatchInputDirectory": "../tests/test_notes"
    }
  }
}
```

## Assignment Summary

**Signal Booster Technical Assessment** submission implementing enterprise-grade DME processing.

### Tools Used
- **IDE**: VS Code with C# extension
- **AI Tools**: Claude Code, GitHub Copilot
- **Framework**: .NET 8.0 with xUnit testing

### Assignment Requirements ✅
1. **Refactored Logic**: Clean service architecture with dependency injection
2. **Logging & Error Handling**: Structured logging with graceful LLM fallback
3. **Unit Tests**: 89 tests across Unit, Integration, Performance categories
4. **Clear Comments**: XML documentation throughout
5. **Functional**: Reads files, extracts data, POSTs to API
6. **Bonus Features**: LLM integration, multiple formats, 20+ device types

### Instructions to Run
```bash
# Build and run from root
dotnet build SignalBooster.sln
dotnet run --project src

# Run tests
dotnet test

# For LLM enhancement: Add OpenAI API key to src/appsettings.Local.json
```

**For detailed verification steps, see [VERIFICATION_GUIDE.md](VERIFICATION_GUIDE.md)**

### Assumptions & Architecture Decisions
- **Console Application**: Built as console app for batch processing and simple deployment
- **UTF-8 file encoding** for all input files
- **English physician notes** (extensible to other languages)
- **Optional OpenAI API key** (graceful fallback to regex)
- **Sequential processing** (extensible to parallel)

### Future Deployment Options
The current console application architecture provides multiple deployment paths:

**Azure Container Instances** (Direct Deployment):
- Deploy current console app as-is in containers
- Serverless containers for batch jobs
- Event-driven via Azure Storage triggers
- No code changes required

**Azure Functions** (Requires Refactoring):
- Event-driven processing on file uploads
- Serverless scaling based on demand
- **Refactoring needed**: Convert `Program.cs` to function triggers
- Extract core services (already done) for reuse in function handlers

**Azure App Service**:
- Web app hosting with container support
- Scheduled batch jobs via WebJobs
- Direct console app deployment possible

**Refactoring Effort for Azure Functions**:
```csharp
// Current: Program.cs console entry
public static async Task Main(string[] args)

// Azure Functions: Function trigger entry  
[FunctionName("ProcessNote")]
public static async Task Run([BlobTrigger("notes/{name}")] Stream note)
{
    // Reuse existing DeviceExtractor service (no changes needed)
    var extractor = serviceProvider.GetService<DeviceExtractor>();
    await extractor.ProcessNoteAsync(notePath);
}
```
**Minimal Effort**: Core business logic in services remains unchanged - only entry point needs modification.

**Example Docker Setup**:
```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0
COPY src/bin/Release/net8.0/ /app
WORKDIR /app
ENTRYPOINT ["dotnet", "SignalBooster.dll"]
```

---

**Ready for Production** ✅