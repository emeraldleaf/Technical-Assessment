# Signal Booster - Enhanced MVP with DDD Architecture

## Overview
A clean, testable MVP version of the Signal Booster application that follows SOLID principles and Domain-Driven Design (DDD) architecture. Features LLM integration, Application Insights logging, and enterprise-ready patterns for DME device order processing.

## 🏗️ Domain-Driven Design Architecture

This application follows a clean DDD layered architecture:

```
├── Domain/                 # Core business logic (no dependencies)
│   ├── Models/            # Domain entities and value objects
│   │   └── DeviceOrder.cs
│   └── Interfaces/        # Domain service abstractions
│       ├── IFileReader.cs
│       ├── ITextParser.cs
│       ├── ILlmTextParser.cs
│       └── IApiClient.cs
│
├── Application/           # Use cases and orchestration
│   └── Services/
│       └── DeviceExtractor.cs  # Main business workflow
│
├── Infrastructure/        # External concerns and implementations
│   ├── Configuration/     # Settings and options
│   │   └── SignalBoosterOptions.cs
│   └── Services/         # Interface implementations
│       ├── FileReader.cs
│       ├── TextParser.cs
│       ├── OpenAITextParser.cs
│       └── ApiClient.cs
│
└── Presentation/         # Entry point and UI concerns
    └── Program.cs        # Console application host
```

### 🎯 DDD Layer Responsibilities

- **Domain**: Contains business entities and core abstractions. No external dependencies.
- **Application**: Orchestrates business workflows and use cases.
- **Infrastructure**: Implements domain interfaces with external technology concerns.
- **Presentation**: User interface and application hosting concerns.

## SOLID Principles Applied

1. **Single Responsibility**: Each class has one reason to change
   - `FileReader`: Only handles file I/O
   - `TextParser`: Only handles text parsing
   - `ApiClient`: Only handles HTTP communication
   - `DeviceExtractor`: Only orchestrates the workflow

2. **Open/Closed**: Easy to extend without modification
   - New device types can be added to `TextParser`
   - New file formats can be supported via new `IFileReader` implementations

3. **Liskov Substitution**: All interfaces can be safely swapped
   - Test implementations can replace production implementations

4. **Interface Segregation**: Small, focused interfaces
   - Each interface has a single, well-defined responsibility

5. **Dependency Inversion**: Depends on abstractions, not concretions
   - All dependencies are injected through interfaces

## Key Improvements from Original

- **95 lines** → **~400 lines** (significant enhancement with enterprise features)
- **Cryptic variables** → **Clear naming**
- **No error handling** → **Proper exception handling**
- **No logging** → **Structured logging with Application Insights**
- **No tests** → **Comprehensive unit tests (11 tests)**
- **Monolithic** → **Modular, testable components**
- **Manual parsing only** → **LLM integration with fallback**
- **Text files only** → **Multiple input formats (TXT, JSON)**
- **Hard-coded values** → **Configurable via appsettings.json**
- **Basic device support** → **Extended DME device types**

## ✨ Enhanced Features

### 🤖 LLM Integration
- **OpenAI Integration**: Uses Azure OpenAI or OpenAI API for intelligent text extraction
- **Fallback Strategy**: Automatically falls back to regex parser if LLM unavailable
- **Configurable**: Set API key in `appsettings.json` or environment variables

### 📊 Application Insights Logging
- **Structured Logging**: Serilog with console, file, and Application Insights sinks
- **Telemetry**: Comprehensive logging for production monitoring
- **Correlation IDs**: End-to-end request tracking

### 📝 Multiple Input Formats
- **Text Files**: Traditional `.txt` physician notes
- **JSON-Wrapped Notes**: Supports JSON with `note`, `physician_note`, `text`, or `content` properties
- **Auto-Detection**: Automatically detects and extracts content based on file extension

### 🏥 Extended Device Support
- **CPAP/BiPAP**: Sleep apnea devices with mask types and add-ons
- **Oxygen**: Tanks, concentrators with flow rates and usage patterns
- **Mobility**: Wheelchairs, walkers, rollators
- **Respiratory**: Nebulizers with medication and frequency
- **Home Medical**: Hospital beds, commodes, lift chairs

### ⚙️ Configuration Management
- **appsettings.json**: Centralized configuration
- **Environment Variables**: Override with `SIGNALBOOSTER_` prefix
- **Configurable APIs**: Base URLs, timeouts, retry policies
- **File Path Configuration**: Default input paths and supported extensions

## Running the Application

### Basic Usage
```bash
cd mvp_src
dotnet run
```

### With Custom File
```bash
dotnet run path/to/physician_note.txt
dotnet run test_note.json  # JSON-wrapped note
```

### 🔑 OpenAI Configuration (Local Development)

**Option 1: Local Settings File (Recommended)**
1. Create `appsettings.Local.json` (already gitignored):
```json
{
  "SignalBooster": {
    "OpenAI": {
      "ApiKey": "sk-your-actual-openai-api-key-here"
    }
  }
}
```

**Option 2: Development Settings**
Update `appsettings.Development.json`:
```json
{
  "SignalBooster": {
    "OpenAI": {
      "ApiKey": "your-openai-api-key-here"
    }
  }
}
```

**Option 3: Environment Variable**
```bash
export SIGNALBOOSTER_SignalBooster__OpenAI__ApiKey="your-api-key"
dotnet run
```

### With Application Insights
Set connection string in `appsettings.json`:
```json
{
  "ApplicationInsights": {
    "ConnectionString": "your-connection-string"
  }
}
```

## Running Tests

```bash
cd mvp_tests
dotnet test
```

**Test Results**: 11/11 tests passing
- Unit tests for all core components
- LLM integration testing with mocks
- JSON parsing validation
- Extended device type coverage

## Configuration

### appsettings.json
```json
{
  "SignalBooster": {
    "Api": {
      "BaseUrl": "https://alert-api.com",
      "TimeoutSeconds": 30
    },
    "Files": {
      "DefaultInputPath": "physician_note.txt",
      "SupportedExtensions": [".txt", ".json"]
    },
    "OpenAI": {
      "ApiKey": "",
      "Model": "gpt-3.5-turbo",
      "MaxTokens": 1000
    }
  },
  "ApplicationInsights": {
    "ConnectionString": ""
  }
}
```

### Environment Variables
Override any setting with `SIGNALBOOSTER_` prefix:
```bash
SIGNALBOOSTER_SignalBooster__OpenAI__ApiKey="your-key"
SIGNALBOOSTER_ApplicationInsights__ConnectionString="your-connection"
```

## Tools Used

- **IDE**: VS Code with Claude Code
- **AI Tools**: Claude Code for refactoring and test generation
- **Framework**: .NET 8.0
- **Testing**: xUnit with NSubstitute for mocking
- **Logging**: Serilog with Application Insights integration
- **LLM**: Azure OpenAI / OpenAI API integration
- **Configuration**: Microsoft.Extensions.Configuration

## Implementation Status

### ✅ Completed Stretch Goals
- **✅ LLM Integration**: OpenAI/Azure OpenAI for text extraction with regex fallback
- **✅ Multiple Input Formats**: JSON-wrapped notes support
- **✅ Configurability**: File paths, API endpoints, LLM settings
- **✅ Extended Device Support**: 8+ DME device types with qualifiers
- **✅ Application Insights**: Structured logging and telemetry

### 🚀 Enterprise Ready Features
- **Dependency Injection**: Full DI container with scoped lifetimes
- **Configuration Management**: Hierarchical config with environment overrides
- **Error Handling**: Graceful degradation and fallback strategies
- **Observability**: Structured logging with correlation tracking
- **Testability**: 100% mockable architecture with comprehensive tests

## Assumptions & Limitations

- Input files are UTF-8 encoded
- OpenAI API key required for LLM features (graceful fallback to regex)
- Application Insights optional for production telemetry
- Single-threaded processing (easily scalable to multi-threading)
- English language medical terminology (extensible to other languages)