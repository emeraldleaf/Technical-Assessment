# 📊 SignalBooster MVP - Logging Guide

This guide covers the logging implementation in the SignalBooster MVP, focusing on practical logging for development and production monitoring.

## 📋 **Table of Contents**

1. [Overview](#overview)
2. [Current Logging Implementation](#current-logging-implementation)
3. [Log Structure & Patterns](#log-structure--patterns)
4. [Reading & Monitoring Logs](#reading--monitoring-logs)
5. [Configuration](#configuration)
6. [Application Insights Integration](#application-insights-integration)
7. [Best Practices](#best-practices)
8. [Troubleshooting](#troubleshooting)

---

## 🎯 **Overview**

SignalBooster MVP implements **structured logging** with Serilog for development debugging and production monitoring:

- **Development**: Console + file logging for local debugging
- **Production**: File logging + Application Insights (when configured)
- **Pattern**: Structured logging with class/method context and performance tracking

### **Current Logging Features**
✅ **Console & File Output** for development  
✅ **Class/Method Context** in every log entry  
✅ **Correlation ID Tracking** for request tracing  
✅ **Performance Timing** with duration measurements  
✅ **Step-by-Step Processing** logging  
✅ **Error Context** with detailed exception information  

---

## 🏗️ **Current Logging Implementation**

### **Logging Pattern Used**

All services use this consistent logging pattern:
```csharp
_logger.LogInformation("[{Class}.{Method}] {Message}", 
    nameof(ClassName), nameof(MethodName), "Descriptive message");
```

### **Actual Service Logging Examples**

**DeviceExtractor.cs:**
```csharp
_logger.LogInformation("[{Class}.{Method}] Step 1: Starting device order processing for {FilePath} with {ProcessingMode}, NoteLength: {NoteLength} chars",
    nameof(DeviceExtractor), nameof(ProcessNoteAsync), filePath, processingMode, noteText.Length);

_logger.LogInformation("[{Class}.{Method}] Step 2: Invoking text parser with {ProcessingMode} mode",
    nameof(DeviceExtractor), nameof(ProcessNoteAsync), processingMode);

_logger.LogError(ex, "[{Class}.{Method}] Step FAILED: Processing failed after {DurationMs}ms for {FilePath}, CorrelationId: {CorrelationId}",
    nameof(DeviceExtractor), nameof(ProcessNoteAsync), stopwatch.ElapsedMilliseconds, filePath, correlationId);
```

**TextParser.cs:**
```csharp
_logger.LogInformation("[{Class}.{Method}] Step 1: Configuring OpenAI LLM extraction. Model: {Model}, MaxTokens: {MaxTokens}, Temperature: {Temperature}",
    nameof(TextParser), nameof(ParseDeviceOrderAsync), _options.Model, _options.MaxTokens, _options.Temperature);

_logger.LogWarning(ex, "[{Class}.{Method}] LLM extraction failed, falling back to regex parsing", 
    nameof(TextParser), nameof(ParseDeviceOrder));
```

**FileReader.cs:**
```csharp
_logger.LogInformation("[{Class}.{Method}] Reading file {FilePath}",
    nameof(FileReader), nameof(ReadFileAsync), filePath);

_logger.LogError("[{Class}.{Method}] File not found {FilePath}",
    nameof(FileReader), nameof(ReadFileAsync), filePath);
```

---

## 📝 **Log Structure & Patterns**

### **Current File Structure**
```
logs/
├── signal-booster-20250911.txt    (today's logs)
├── signal-booster-20250910.txt    (yesterday)
└── signal-booster-20250909.txt    (day before)
```

### **Log Entry Format**

**Console Output:**
```
[16:57:00 INF] [DeviceExtractor.ProcessNoteAsync] Step 1: Starting device order processing for test_notes/physician_note1.txt with LLM, NoteLength: 196 chars
[16:57:01 INF] [TextParser.ParseDeviceOrderAsync] Step 1: Configuring OpenAI LLM extraction. Model: gpt-4o, MaxTokens: 1000, Temperature: 0.1
[16:57:02 INF] [DeviceExtractor.ProcessNoteAsync] Step 2: Device order extracted successfully. Device: Oxygen Tank, Patient: Harold Finch, Provider: Dr. Cuddy, ParseDuration: 847ms
```

**File Output (JSON structured):**
```json
{
  "@t": "2025-09-11T16:57:00.1234567Z",
  "@l": "Information",
  "@mt": "[{Class}.{Method}] Step 1: Starting device order processing for {FilePath} with {ProcessingMode}, NoteLength: {NoteLength} chars",
  "Class": "DeviceExtractor",
  "Method": "ProcessNoteAsync", 
  "FilePath": "test_notes/physician_note1.txt",
  "ProcessingMode": "LLM",
  "NoteLength": 196,
  "SourceContext": "SignalBooster.Mvp.Services.DeviceExtractor"
}
```

---

## 📖 **Reading & Monitoring Logs**

### **Development - Console Logs**
During development, logs are displayed in the console with timestamps and levels:
```bash
dotnet run test_notes/physician_note1.txt

# Output:
[16:57:00 INF] [DeviceExtractor.ProcessNoteAsync] Step 1: Starting device order processing...
[16:57:01 INF] [TextParser.ParseDeviceOrderAsync] Step 1: Configuring OpenAI LLM extraction...
[16:57:02 INF] [DeviceExtractor.ProcessNoteAsync] Processing completed successfully...
```

### **File Log Commands**

**View Recent Activity:**
```bash
# Last 20 log entries
tail -20 logs/signal-booster-$(date +%Y%m%d).txt

# Follow logs in real-time
tail -f logs/signal-booster-$(date +%Y%m%d).txt

# View today's logs
cat logs/signal-booster-$(date +%Y%m%d).txt
```

**Search for Specific Events:**
```bash
# Find all errors
grep -i '"@l":"Error"' logs/signal-booster-*.txt

# Find successful processing
grep -i "Processing completed successfully" logs/signal-booster-*.txt

# Find LLM vs Regex usage
grep -i "ProcessingMode.*LLM" logs/signal-booster-*.txt
grep -i "falling back to regex" logs/signal-booster-*.txt

# Find specific device types
grep -i "Device.*CPAP" logs/signal-booster-*.txt
grep -i "Device.*Oxygen" logs/signal-booster-*.txt
```

**Filter by Class/Method:**
```bash
# View all DeviceExtractor logs
grep -i "DeviceExtractor" logs/signal-booster-*.txt

# View all TextParser logs  
grep -i "TextParser" logs/signal-booster-*.txt

# View all API-related logs
grep -i "ApiClient" logs/signal-booster-*.txt
```

**Pretty Print JSON Logs:**
```bash
# Pretty print last 5 entries (requires jq)
tail -5 logs/signal-booster-$(date +%Y%m%d).txt | jq .

# Extract key information
grep -h . logs/signal-booster-*.txt | jq -r '
  [.["@t"], .["@l"], .Class // "N/A", .Method // "N/A"] | @csv'
```

---

## ⚙️ **Configuration**

### **Current appsettings.json Configuration**
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/signal-booster-.txt",
          "rollingInterval": "Day"
        }
      }
    ]
  },
  "ApplicationInsights": {
    "ConnectionString": ""
  }
}
```

### **Log Levels**
- **Information**: Normal processing flow, step-by-step operations
- **Warning**: LLM fallback scenarios, missing optional data
- **Error**: Processing failures, file not found, API errors
- **Critical**: Application startup issues, configuration problems

### **Environment-Specific Configuration**

**Development (appsettings.Development.json):**
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    }
  }
}
```

**Production (appsettings.Production.json):**
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Warning",
      "Override": {
        "SignalBooster": "Information"
      }
    }
  }
}
```

---

## 🔗 **Application Insights Integration**

### **Setup for Production Monitoring**

1. **Add Application Insights Connection String:**
```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=YOUR_KEY;IngestionEndpoint=https://YOUR_REGION.in.applicationinsights.azure.com/"
  }
}
```

2. **Install Application Insights Serilog Sink:**
```bash
dotnet add package Serilog.Sinks.ApplicationInsights
```

3. **Update Serilog Configuration:**
```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File", 
        "Args": {
          "path": "logs/signal-booster-.txt",
          "rollingInterval": "Day"
        }
      },
      {
        "Name": "ApplicationInsights",
        "Args": {
          "connectionString": "YOUR_CONNECTION_STRING_HERE",
          "telemetryConverter": "Serilog.Sinks.ApplicationInsights.TelemetryConverters.TraceTelemetryConverter, Serilog.Sinks.ApplicationInsights"
        }
      }
    ]
  }
}
```

### **Key Telemetry Data Points**

Current logging captures these metrics for Application Insights:
- **Processing Duration**: End-to-end timing per file
- **Device Types**: Distribution of processed DME devices
- **LLM vs Regex Usage**: Fallback pattern analytics
- **Error Rates**: Failure patterns and causes
- **File Processing Volume**: Throughput metrics

---

## 🎯 **Step-by-Step Processing Logs**

### **Single File Processing Flow**
```
[DeviceExtractor.ProcessNoteAsync] Step 1: Starting device order processing
[DeviceExtractor.ProcessNoteAsync] Step 2: Invoking text parser with LLM mode
[TextParser.ParseDeviceOrderAsync] Step 1: Configuring OpenAI LLM extraction
[TextParser.ParseDeviceOrderAsync] Step 2: Creating structured extraction prompt
[TextParser.ParseDeviceOrderAsync] Step 3: Calling OpenAI API for device extraction
[TextParser.ParseDeviceOrderAsync] Step 4: OpenAI API call completed
[TextParser.ParseDeviceOrderAsync] Step 5: Parsing LLM JSON response
[DeviceExtractor.ProcessNoteAsync] Step 3: Device order extracted successfully
[DeviceExtractor.ProcessNoteAsync] Step 4: Posting device order to external API
[DeviceExtractor.ProcessNoteAsync] Step 5: Processing completed successfully
```

### **Batch Processing Flow**
```
[DeviceExtractor.ProcessAllNotesAsync] Step 1: Starting batch processing mode
[DeviceExtractor.ProcessAllNotesAsync] Step 2: Found 10 files to process
[DeviceExtractor.ProcessAllNotesAsync] Step 3.1: Processing file physician_note1 (1/10)
[DeviceExtractor.ProcessAllNotesAsync] Step 4.1: Successfully processed physician_note1
[DeviceExtractor.ProcessAllNotesAsync] Step 3.2: Processing file physician_note2 (2/10)
... (continues for all files)
[DeviceExtractor.ProcessAllNotesAsync] Batch processing completed
```

---

## 💡 **Best Practices**

### **Current Implementation Best Practices**

1. **Consistent Context**: Every log includes `[{Class}.{Method}]` for easy filtering
2. **Structured Parameters**: Use named parameters like `{FilePath}`, `{DeviceType}` for queryability
3. **Performance Tracking**: Include duration measurements for optimization
4. **Error Context**: Full exception details with processing context
5. **Step Numbering**: Sequential step logging for debugging workflows

### **Log Message Guidelines**

**Good Examples:**
```csharp
// Clear context and structured data
_logger.LogInformation("[{Class}.{Method}] Step 1: Starting processing for {FilePath} with {Mode}",
    nameof(DeviceExtractor), nameof(ProcessNoteAsync), filePath, mode);

// Error with full context
_logger.LogError(ex, "[{Class}.{Method}] Processing failed after {Duration}ms for {FilePath}",
    nameof(DeviceExtractor), nameof(ProcessNoteAsync), duration, filePath);
```

**Avoid:**
```csharp
// Too generic, no context
_logger.LogInformation("Processing started");

// No structured data
_logger.LogInformation($"Processing {filePath}");
```

---

## 🔍 **Troubleshooting**

### **Common Log Analysis Scenarios**

**1. Find Processing Failures:**
```bash
grep -i '"@l":"Error"' logs/signal-booster-*.txt | jq -r '.["@mt"]'
```

**2. Track Specific File Processing:**
```bash
grep -i "physician_note1.txt" logs/signal-booster-*.txt
```

**3. Monitor LLM vs Regex Usage:**
```bash
grep -i "ProcessingMode.*LLM" logs/signal-booster-*.txt | wc -l
grep -i "falling back to regex" logs/signal-booster-*.txt | wc -l
```

**4. Find Performance Issues:**
```bash
grep -i "Duration.*ms" logs/signal-booster-*.txt | grep -E '[0-9]{4,}ms'
```

### **Log File Locations**

- **Development**: Console + `logs/signal-booster-YYYYMMDD.txt`
- **Production**: `logs/signal-booster-YYYYMMDD.txt` + Application Insights
- **Docker**: Container logs accessible via `docker logs <container>`

---

## 📊 **Monitoring with KQL Queries**

When Application Insights is configured, use the queries from `../SignalBooster-Queries.kql`:

**Processing Performance:**
```kql
traces
| where message contains "Processing completed successfully"
| extend Duration = toint(customDimensions.TotalDurationMs)
| summarize AvgDuration = avg(Duration), MaxDuration = max(Duration)
```

**Device Type Distribution:**
```kql
traces
| where message contains "Device order extracted successfully"
| extend Device = tostring(customDimensions.DeviceType)
| summarize count() by Device
```

**Error Analysis:**
```kql
traces
| where severityLevel >= 3
| summarize count() by bin(timestamp, 1h)
| render timechart
```

---

## 🎯 **Summary**

The SignalBooster MVP logging implementation provides:

✅ **Structured console and file logging** for development  
✅ **Consistent class/method context** for easy debugging  
✅ **Step-by-step processing tracking** for workflow visibility  
✅ **Performance monitoring** with duration measurements  
✅ **Error context** with full exception details  
✅ **Application Insights ready** for production telemetry  

The logging is designed to be **developer-friendly** for debugging while providing **production monitoring** capabilities when Application Insights is configured.