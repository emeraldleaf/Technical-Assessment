# SignalBooster - Quick Verification Guide

## Verification Checklist (10 minutes)

- [ ] **Builds cleanly** without errors
- [ ] **Tests pass** (89 tests)
- [ ] **Application runs** and processes files
- [ ] **Output generated** as JSON

---

## **1. Build & Run** (3 minutes)

### Build and Test Commands

**Unix/Linux/macOS:**
```bash
# Build and test from root
dotnet build SignalBooster.sln
dotnet test

# Run tests with coverage (matches CI pipeline)
dotnet test --configuration Release --logger trx --collect:"XPlat Code Coverage"
```

**Windows (Command Prompt or PowerShell):**
```cmd
REM Build and test from root
dotnet build SignalBooster.sln
dotnet test

REM Run tests with coverage (matches CI pipeline)  
dotnet test --configuration Release --logger trx --collect:"XPlat Code Coverage"
```

**Expected Results:**
```
# 89/89 tests PASS (normal operation)
# Snapshot tests may "fail" ONLY on first run (creates baseline files)
```

### Test Results Breakdown:

**Expected to PASS (All 89 tests):**
- Unit Tests: All core business logic
- Integration Tests: File processing workflows  
- Performance Tests: Speed and memory benchmarks
- Property Tests: Edge case handling (including device name variations)

**Sample Normal Test Output:**
```
Test Run Summary:
Total tests: 89
     Passed: 89
     Failed: 0
     Skipped: 0
All tests pass - system is working correctly
```

**First-Run Snapshot Behavior:**
- **Snapshot Tests**: May show "failures" on very first run to create baseline `.verified.txt` files
- **Normal**: These files are created once, then subsequent runs compare against them

### Batch Processing Mode (Default)

**Unix/Linux/macOS:**
```bash
# Run application (must be from src directory for config files)
cd src && dotnet run

# Expected: Processes test files, generates JSON outputs
```

**Windows:**
```cmd
REM Run application (must be from src directory for config files)
cd src
dotnet run

REM Expected: Processes test files, generates JSON outputs
```

---

## **2. Sample Verification** (2 minutes)

### Single File Processing

**Method 1: Environment Variable Override (Recommended)**
```bash
# Unix/Linux/macOS
cd src && SIGNALBOOSTER_Files__BatchProcessingMode=false dotnet run ../tests/test_notes/physician_note1.txt

# Windows Command Prompt
cd src
set SIGNALBOOSTER_Files__BatchProcessingMode=false
dotnet run ../tests/test_notes/physician_note1.txt

# Windows PowerShell
cd src
$env:SIGNALBOOSTER_Files__BatchProcessingMode="false"
dotnet run ../tests/test_notes/physician_note1.txt

# Check output
cat output.json        # Unix/Linux/macOS
type output.json       # Windows
```

**Method 2: Configuration Override**
```bash
# Create local config override
echo '{"SignalBooster":{"Files":{"BatchProcessingMode":false}}}' > src/appsettings.Local.json

# Windows PowerShell
'{"SignalBooster":{"Files":{"BatchProcessingMode":false}}}' | Out-File -FilePath src/appsettings.Local.json

# Then run normally
cd src && dotnet run ../tests/test_notes/physician_note1.txt
```

**Expected Output:**
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

---

## **3. Optional: OpenAI Setup** (5 minutes)

For enhanced LLM processing:

**Unix/Linux/macOS:**
```bash
# Copy template
cp src/appsettings.Local.json.template src/appsettings.Local.json

# Edit file and add your OpenAI API key:
# Change "ApiKey": "" to "ApiKey": "sk-your-actual-api-key"

# Run again for LLM-powered extraction
dotnet run --project src
```

**Windows Command Prompt:**
```cmd
REM Copy template
copy src\appsettings.Local.json.template src\appsettings.Local.json

REM Edit file and add your OpenAI API key:
REM Change "ApiKey": "" to "ApiKey": "sk-your-actual-api-key"

REM Run again for LLM-powered extraction
dotnet run --project src
```

**Windows PowerShell:**
```powershell
# Copy template
Copy-Item src/appsettings.Local.json.template src/appsettings.Local.json

# Edit file and add your OpenAI API key:
# Change "ApiKey": "" to "ApiKey": "sk-your-actual-api-key"

# Run again for LLM-powered extraction
dotnet run --project src
```

---

## **Troubleshooting**

### Build Errors

**Unix/Linux/macOS:**
```bash
dotnet clean && dotnet restore && dotnet build
```

**Windows:**
```cmd
dotnet clean
dotnet restore  
dotnet build
```

### Missing OpenAI Key
- **Expected**: App gracefully falls back to regex parsing
- **Enhancement**: Add API key for improved accuracy

### Snapshot Tests - First Run Only
- **Snapshot Tests**: May create baseline files on first run only
  - Creates `.verified.txt` files like `SnapshotRegressionTests.ProcessNote_OxygenTankScenario_MatchesSnapshot.verified.txt`
  - Future runs compare against these baselines to detect changes
  - **Action**: No action needed - this is expected behavior

### API Errors
- **Expected**: `alert-api.com` is demo endpoint - API calls will fail
- **Normal**: Application continues processing despite API failures

---

## Success Criteria

**Minimum (Must Pass):**
- Builds without errors  
- Processes physician_note1.txt correctly
- Generates valid JSON output
- All 89 tests pass

**What's Normal:**
- 100% test pass rate (89/89)
- API posting errors (demo endpoint doesn't exist)
- Snapshot tests may create baseline files on first run

**Complete Verification:**
- All core functionality works
- Batch processing completes
- LLM integration functional (with API key)
- Multiple device types supported

---

**If these steps pass, SignalBooster is working correctly!**