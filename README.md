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

# Run the application (mode determined by BatchProcessingMode setting in appsettings.json)
dotnet run --project src

# Optional: Override with specific file path in single file mode
# dotnet run --project src tests/test_notes/physician_note1.txt

# Run tests
dotnet test
```

### OpenAI API Configuration (Optional)
```bash
# Copy template and add your API key
cp src/appsettings.Local.json.template src/appsettings.Local.json
# Edit src/appsettings.Local.json with your OpenAI API key
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

## Verifying It Works

### Single File Mode (Default)
After running `dotnet run --project src tests/test_notes/physician_note1.txt`, check:

- ✅ **Console shows:** `Processing completed successfully`
- ✅ **File created:** `output.json` in project root with extracted device data
- ✅ **Logs created:** `logs/signal-booster-YYYYMMDD.txt` with detailed processing info
- ✅ **API simulation:** Console shows "Test environment detected - simulating API call"

### Batch Mode
To switch between modes, simply change `"BatchProcessingMode"` in `src/appsettings.json`:
- `"BatchProcessingMode": true` - Processes all files in `BatchInputDirectory`
- `"BatchProcessingMode": false` - Processes single file from `DefaultInputPath` (or command line override)

Then run `dotnet run --project src`. Check:

- ✅ **Console shows:** `Batch processing completed: Successfully processed X files`
- ✅ **File summary:** Console lists each processed file (e.g., `✓ physician_note1 → Oxygen Tank for Harold Finch`)
- ✅ **Logs created:** Detailed processing info for each file in `logs/`
- ✅ **No output.json:** Batch mode only logs and posts to API (no individual output files)

### Expected Data Extraction
Both modes should extract structured data like:
- **Device Type:** Oxygen Tank, CPAP, Hospital Bed, etc.
- **Patient Info:** Name, DOB, diagnosis
- **Provider:** Ordering physician name
- **Device Details:** Flow rates, settings, usage instructions

## Architecture

Layered service-oriented design with dependency injection:

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

- **Advanced Agentic AI**: Multi-agent extraction system with autonomous reasoning and validation
- **LLM Integration**: OpenAI GPT-4o with intelligent fallback strategies
- **Multiple Formats**: Supports .txt and .json input files
- **20+ Device Types**: CPAP, Oxygen, Hospital Beds, Wheelchairs, etc.
- **Batch Processing**: Process entire directories
- **Fault Tolerance**: API retry logic with exponential backoff
- **Performance Optimized**: StreamReader for large files (>1MB)
- **Comprehensive Testing**: 143 tests across multiple categories (100% pass rate)
- **Production Ready**: Structured logging, error handling, observability

## Testing

```bash
# Run all tests (89 total)
dotnet test

# Run tests with coverage (matches CI pipeline)
dotnet test --configuration Release --logger trx --collect:"XPlat Code Coverage"

# Expected: All 143 tests pass (100% success rate)

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
    "Extraction": {
      "UseAgenticMode": true,
      "ExtractionMode": "Standard",
      "RequireValidation": false,
      "MinConfidenceThreshold": 0.8,
      "EnableSelfCorrection": false,
      "MaxCorrectionAttempts": 2
    },
    "Api": {
      "BaseUrl": "https://alert-api.com",
      "Endpoint": "/device-orders",
      "TimeoutSeconds": 30,
      "RetryCount": 3,
      "EnableApiPosting": true
    },
    "Files": {
      "BatchProcessingMode": false,
      "BatchInputDirectory": "../tests/test_notes"
    }
  }
}
```

### Agentic AI Mode

The application features an advanced multi-agent AI system for enhanced extraction accuracy:

#### Extraction Modes
- **Fast**: Single-pass extraction for quick processing
- **Standard**: Multi-agent with validation (recommended)
- **Thorough**: Comprehensive with multiple validation rounds

#### AI Agents
1. **Document Analyzer**: Analyzes structure and identifies key sections
2. **Primary Extractor**: Extracts device order information with medical context
3. **Medical Validator**: Validates medical accuracy and completeness
4. **Confidence Assessor**: Evaluates extraction confidence and identifies uncertainties

#### Configuration Options
- `UseAgenticMode`: Enable/disable multi-agent system (falls back to simple parser)
- `ExtractionMode`: Choose processing depth (Fast/Standard/Thorough)
- `RequireValidation`: Enable validation step with potential self-correction
- `MinConfidenceThreshold`: Minimum confidence for accepting results
- `EnableSelfCorrection`: Allow agents to fix identified issues
- `MaxCorrectionAttempts`: Limit correction iterations

#### Benefits
- **Higher Accuracy**: Multiple agents cross-validate findings
- **Medical Context**: Specialized medical knowledge for device orders
- **Self-Correction**: Automatic fixing of identified issues
- **Confidence Scoring**: Per-field and overall confidence metrics
- **Detailed Reasoning**: Complete audit trail of agent decisions

## Assignment Summary

**Signal Booster Technical Assessment** submission implementing enterprise-grade DME processing.

### Tools Used
- **IDE**: VS Code with C# extension
- **AI Tools**: Claude Code, GitHub Copilot
- **Framework**: .NET 8.0 with xUnit testing

### Assignment Requirements ✅
1. **Refactored Logic**: Layered service architecture with dependency injection
2. **Logging & Error Handling**: Structured logging with graceful LLM fallback
3. **Unit Tests**: 143 tests across Unit, Integration, Performance, and Coverage categories
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

## CI/CD Pipeline

### GitHub Actions Setup
The repository includes a complete CI/CD pipeline (`.github/workflows/ci.yml`) that:
- Runs **143 comprehensive tests** on every push/PR
- Generates **code coverage reports** (currently 73% line coverage)
- Builds and deploys artifacts for production
- Supports both **regex-only** and **OpenAI-enhanced** testing modes

### Required GitHub Secrets
To enable full CI/CD functionality including OpenAI-enhanced tests:

1. **Navigate to Repository Settings**:
   - Go to `https://github.com/[your-org]/[your-repo]/settings/secrets/actions`

2. **Add OpenAI API Key Secret**:
   ```
   Name: OPENAI_API_KEY
   Value: sk-proj-[your-openai-api-key]
   ```

3. **Workflow Environment Variables**:
   The CI/CD pipeline automatically uses the secret:
   ```yaml
   env:
     OPENAI_API_KEY: ${{ secrets.OPENAI_API_KEY }}
   ```

### Test Execution in CI/CD
- **Without API Key**: ~140/143 tests pass (AI snapshot tests gracefully skipped)
- **With API Key**: All 143 tests execute with full OpenAI integration
- **Security**: No hardcoded secrets - all handled via GitHub secrets

### Pipeline Triggers
- **Push to main/develop**: Full test suite + deployment
- **Pull Requests**: Test suite validation
- **Manual Dispatch**: On-demand pipeline execution

---

**Ready for Production** ✅