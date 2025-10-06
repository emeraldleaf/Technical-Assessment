# SignalBooster - Quick Verification Guide

## Verification Checklist (10 minutes)

- [ ] **Builds cleanly** without errors
- [ ] **Tests pass** (143 tests - 131 without API key, 143 with API key)
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
# 143/143 tests PASS (with API key) or 131/143 tests PASS (without API key)
# Snapshot tests may "fail" ONLY on first run (creates baseline files)
```

### Test Results Breakdown:

**Expected to PASS (All 143 tests with API key, 131 tests without):**
- Unit Tests: All core business logic
- Integration Tests: File processing workflows  
- Performance Tests: Speed and memory benchmarks
- Property Tests: Edge case handling (including device name variations)

**Sample Normal Test Output:**
```
Test Run Summary:
Total tests: 143
     Passed: 143 (with API key) or 131 (without API key)
     Failed: 0
     Skipped: 0
All tests pass - system is working correctly
```

**First-Run Snapshot Behavior:**
- **Snapshot Tests**: May show "failures" on very first run to create baseline `.verified.txt` files
- **Normal**: These files are created once, then subsequent runs compare against them

### Run Application (Mode determined by appsettings.json)

**Unix/Linux/macOS:**
```bash
# Run application (BatchProcessingMode: true by default)
dotnet run --project src

# Expected: Processes all files in tests/test_notes directory
```

**Windows:**
```cmd
REM Run application (BatchProcessingMode: true by default)
dotnet run --project src

REM Expected: Processes all files in tests/test_notes directory
```

---

## **2. Sample Verification** (2 minutes)

### Single File Processing

**Method 1: Edit appsettings.json (Recommended)**
```bash
# Edit src/appsettings.json and change:
# "BatchProcessingMode": false

# Then run with optional file override
dotnet run --project src tests/test_notes/physician_note1.txt

# Or run with configured DefaultInputPath
dotnet run --project src

# Check output
cat output.json        # Unix/Linux/macOS
type output.json       # Windows
```

**Method 2: Environment Variable Override**
```bash
# Unix/Linux/macOS
SIGNALBOOSTER_Files__BatchProcessingMode=false dotnet run --project src tests/test_notes/physician_note1.txt

# Windows Command Prompt
set SIGNALBOOSTER_Files__BatchProcessingMode=false
dotnet run --project src tests/test_notes/physician_note1.txt

# Windows PowerShell
$env:SIGNALBOOSTER_Files__BatchProcessingMode="false"
dotnet run --project src tests/test_notes/physician_note1.txt
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
- All 143 tests pass (with API key) or 131 tests pass (without API key)

**What's Normal:**
- 100% test pass rate (143/143 with API key, 131/143 without API key)
- API posting errors (demo endpoint doesn't exist)
- Snapshot tests may create baseline files on first run

**Complete Verification:**
- All core functionality works
- Batch processing completes
- LLM integration functional (with API key)
- Multiple device types supported

---

**If these steps pass, SignalBooster is working correctly!**