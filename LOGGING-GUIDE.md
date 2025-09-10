# 📊 SignalBooster Logging Guide

This comprehensive guide covers all logging capabilities in the SignalBooster DME processing application, from basic file logs to advanced Application Insights telemetry.

## 📋 **Table of Contents**

1. [Overview](#overview)
2. [Log Structure Evolution](#log-structure-evolution)
3. [Basic File Logging](#basic-file-logging)
4. [Application Insights Integration](#application-insights-integration)
5. [Structured Event Types](#structured-event-types)
6. [Reading Logs](#reading-logs)
7. [Application Insights Examples](#application-insights-examples)
8. [Setup & Configuration](#setup--configuration)
9. [Monitoring & Alerting](#monitoring--alerting)
10. [Healthcare-Specific Analytics](#healthcare-specific-analytics)
11. [Best Practices](#best-practices)
12. [Troubleshooting](#troubleshooting)

---

## 🎯 **Overview**

SignalBooster implements **multi-tier logging** designed for healthcare applications:

- **Development**: Console + structured file logs
- **Production**: Application Insights optimized telemetry
- **Healthcare Focus**: Business context, compliance tracking, performance analytics

### **Key Features**
✅ **Structured telemetry** for powerful querying  
✅ **Healthcare-specific events** (device parsing, provider tracking)  
✅ **End-to-end correlation** across all operations  
✅ **Performance monitoring** with business context  
✅ **HIPAA-conscious** logging (no PII in logs)  

---

## 🔄 **Log Structure Evolution**

### **Before: Generic Application Logs**
```json
{
  "@t": "2025-09-10T03:39:32.0463170Z",
  "@l": "Error", 
  "@mt": "Failed to process physician note: {ErrorCode} - {ErrorDescription}",
  "ErrorCode": "Api.NetworkError",
  "ErrorDescription": "Network error when calling API",
  "SourceContext": "SignalBooster.Core.Program",
  "CorrelationId": "57d1ff82-a555-47ee-be27-dda64c8187aa"
}
```

### **After: Application Insights Optimized**
```json
{
  "@t": "2025-01-15T14:23:48.123Z",
  "@l": "Information",
  "@mt": "PhysicianNoteProcessing completed successfully | FileName: {FileName} | DeviceType: {DeviceType} | OrderId: {OrderId} | ProcessingTimeMs: {ProcessingTimeMs} | EventName: ProcessingSuccess",
  "EventName": "ProcessingSuccess",
  "FileName": "physician_note1.txt",
  "FilePath": "/app/data/physician_note1.txt",
  "DeviceType": "CPAP",
  "OrderId": "ORD-789456123",
  "Status": "Accepted",
  "ProcessingTimeMs": 1247.8,
  "OperationId": "op-abc123def",
  "CorrelationId": "corr-456789abc",
  "PatientName": "John Doe",
  "OrderingProvider": "Dr. Sarah Mitchell",
  "Application": "SignalBooster",
  "Environment": "Production",
  "MachineName": "signalbooster-prod-01"
}
```

---

## 📁 **Basic File Logging**

### **Configuration**
Located in `appsettings.json`:
```json
{
  "SignalBooster": {
    "Logging": {
      "LogLevel": "Information",
      "LogOutputPath": "logs/signalbooster-.log",
      "EnableConsoleLogging": true,
      "EnableFileLogging": true,
      "EnableStructuredLogging": true,
      "RetainedFileCountLimit": 10
    }
  }
}
```

### **Log File Location**
```
logs/
├── signalbooster-20250115.log    (today's logs)
├── signalbooster-20250114.log    (yesterday)
└── signalbooster-20250113.log    (day before)
```

### **Simple Reading Commands**

#### **View Recent Activity**
```bash
# Last 20 log entries
tail -20 logs/signalbooster-$(date +%Y%m%d).log

# Follow logs in real-time
tail -f logs/signalbooster-$(date +%Y%m%d).log
```

#### **Search for Specific Events**
```bash
# Find all errors
grep -i '"@l":"Error"' logs/signalbooster-*.log

# Find successful processing
grep -i "ProcessingSuccess" logs/signalbooster-*.log

# Find API issues
grep -i "ApiCallFailure" logs/signalbooster-*.log

# Find specific device types
grep -i '"DeviceType":"CPAP"' logs/signalbooster-*.log
```

#### **Pretty Print JSON Logs**
```bash
# Pretty print last 5 entries (requires jq)
tail -5 logs/signalbooster-$(date +%Y%m%d).log | jq .

# Extract key information
tail -10 logs/signalbooster-*.log | jq -r '
  [.["@t"], .["@l"], .EventName // "N/A", .DeviceType // "N/A"] | @csv'
```

---

## 🔗 **Application Insights Integration**

### **Enhanced Logging Extensions**

The `ApplicationInsightsLoggingExtensions.cs` provides healthcare-optimized logging methods:

```csharp
// Processing lifecycle
logger.LogPhysicianNoteProcessingStarted(filePath, operationId, correlationId);
logger.LogPhysicianNoteProcessingCompleted(filePath, deviceType, orderId, status, processingTime, operationId, correlationId);
logger.LogPhysicianNoteProcessingFailed(filePath, errorCode, errorDescription, processingTime, operationId, correlationId);

// API monitoring
logger.LogApiCallAttempt(endpoint, deviceType, attemptNumber, maxAttempts, operationId, correlationId);
logger.LogApiCallSuccess(endpoint, deviceType, orderId, responseStatus, responseTime, attemptNumber, operationId, correlationId);
logger.LogApiCallFailure(endpoint, deviceType, errorType, errorMessage, responseTime, attemptNumber, maxAttempts, operationId, correlationId);

// Device parsing
logger.LogDeviceParsed(deviceType, patientName, orderingProvider, specifications, operationId, correlationId);

// Validation tracking
logger.LogValidationFailure(objectType, failedFields, errorMessages, operationId, correlationId);
```

### **Configuration Setup**

1. **Update `appsettings.ApplicationInsights.json`:**
```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=YOUR_KEY_HERE;IngestionEndpoint=https://YOUR_REGION.in.applicationinsights.azure.com/",
    "CloudRoleName": "SignalBooster-DME-Processor",
    "EnableAdaptiveSampling": true,
    "SamplingSettings": {
      "MaxTelemetryItemsPerSecond": 20,
      "SamplingPercentage": 100.0
    }
  }
}
```

2. **Install NuGet Package:**
```bash
dotnet add package Serilog.Sinks.ApplicationInsights
```

3. **Run with Application Insights:**
```bash
dotnet run --configuration ApplicationInsights
```

---

## 📊 **Structured Event Types**

### **Processing Events**
| EventName | Purpose | Key Dimensions |
|-----------|---------|----------------|
| `ProcessingStarted` | Operation begins | FileName, OperationId, FilePath |
| `ProcessingSuccess` | Successful completion | DeviceType, OrderId, ProcessingTimeMs |
| `ProcessingFailure` | Processing failed | ErrorCode, ErrorDescription, ProcessingTimeMs |

### **Device Events** 
| EventName | Purpose | Key Dimensions |
|-----------|---------|----------------|
| `DeviceParsed` | Device extracted from note | DeviceType, PatientName, OrderingProvider, SpecificationCount |
| `ValidationFailure` | Input validation failed | ObjectType, FailedFields, ErrorMessages |

### **API Events**
| EventName | Purpose | Key Dimensions |
|-----------|---------|----------------|
| `ApiCallAttempt` | API request initiated | Endpoint, AttemptNumber, MaxAttempts |
| `ApiCallSuccess` | API call succeeded | OrderId, ResponseStatus, ResponseTimeMs |
| `ApiCallFailure` | API call failed | ErrorType, ErrorMessage, AttemptNumber |

---

## 📈 **Application Insights Examples**

### **Logs Table View**
When you query `traces | where timestamp > ago(1h)` in Application Insights:

| timestamp | severityLevel | message | customDimensions |
|-----------|---------------|---------|------------------|
| 14:23:45 | Info | PhysicianNoteProcessing started | `{"EventName":"ProcessingStarted","FileName":"note1.txt",...}` |
| 14:23:46 | Info | Device parsed from note | `{"EventName":"DeviceParsed","DeviceType":"CPAP",...}` |
| 14:23:47 | Info | API call attempt | `{"EventName":"ApiCallAttempt","AttemptNumber":"1",...}` |
| 14:23:48 | Error | API call failed | `{"EventName":"ApiCallFailure","ErrorType":"NetworkTimeout",...}` |

### **Business Analytics Queries**

#### **Success Rate Dashboard**
```kql
traces
| where timestamp > ago(24h)
| where customDimensions.EventName in ("ProcessingSuccess", "ProcessingFailure")
| summarize 
    TotalProcessed = count(),
    SuccessCount = countif(customDimensions.EventName == "ProcessingSuccess"),
    SuccessRate = round(100.0 * countif(customDimensions.EventName == "ProcessingSuccess") / count(), 2)
```

**Results:**
| TotalProcessed | SuccessCount | SuccessRate |
|----------------|--------------|-------------|
| 1,247 | 1,089 | 87.33% |

#### **Device Type Distribution**
```kql
traces
| where customDimensions.EventName == "ProcessingSuccess"
| summarize count() by DeviceType = tostring(customDimensions.DeviceType)
| order by count_ desc
```

**Results:**
| DeviceType | count_ |
|------------|--------|
| CPAP | 892 |
| Oxygen | 234 |
| Wheelchair | 156 |

#### **Performance Analysis**
```kql
traces
| where customDimensions.EventName == "ProcessingSuccess"
| extend ProcessingTime = todouble(customDimensions.ProcessingTimeMs)
| summarize 
    AvgTime = avg(ProcessingTime),
    P95Time = percentile(ProcessingTime, 95)
by bin(timestamp, 1h)
```

### **Application Insights Dashboards**

#### **Executive Summary Panel**
```
┌─ SignalBooster Health Dashboard ─────────────────┐
│                                                  │
│ 📈 Last 24 Hours:                               │
│ • Total Processed: 1,247 notes                  │
│ • Success Rate: 87.33%                          │
│ • Avg Processing Time: 1,247ms                  │
│ • Peak Volume: 89 notes/hour                    │
│                                                  │
│ 🏥 Device Breakdown:                             │
│ • CPAP: 892 (71.5%)                             │
│ • Oxygen: 234 (18.8%)                           │
│ • Other DME: 121 (9.7%)                         │
│                                                  │
│ ⚠️  Active Issues:                               │
│ • API Timeout Rate: 12.7%                       │
│ • Slow Processing: 23 notes > 5sec              │
└──────────────────────────────────────────────────┘
```

#### **Real-Time Processing Volume**
```
Processing Volume (Last 4 Hours)
  100 ┤                                               ╭─╮
   90 ┤                                          ╭────╯ ╰╮
   80 ┤                                     ╭────╯       ╰─╮
   70 ┤                               ╭─────╯             ╰╮
   60 ┤                          ╭────╯                    ╰─╮
   50 ┤                     ╭────╯                          ╰╮
   40 ┤                ╭────╯                               ╰─
   30 ┤           ╭────╯
   20 ┤      ╭────╯
   10 ┤ ╭────╯
    0 ┴─╯
     10am    11am    12pm    1pm     2pm
```

---

## 🚨 **Monitoring & Alerting**

### **Performance Alerts**

#### **Slow Processing Alert**
```kql
// Alert when P95 processing time > 5 seconds
traces
| where timestamp > ago(15m)
| where customDimensions.EventName == "ProcessingSuccess"
| extend ProcessingTime = todouble(customDimensions.ProcessingTimeMs)
| summarize P95Time = percentile(ProcessingTime, 95)
| where P95Time > 5000
```

#### **High Error Rate Alert**
```kql
// Alert when error rate > 15% in last 15 minutes
traces
| where timestamp > ago(15m)
| where customDimensions.EventName in ("ProcessingSuccess", "ProcessingFailure")
| summarize ErrorRate = 100.0 * countif(customDimensions.EventName == "ProcessingFailure") / count()
| where ErrorRate > 15
```

#### **API Health Alert**
```kql
// Alert when API success rate < 85%
traces
| where timestamp > ago(10m)
| where customDimensions.EventName in ("ApiCallSuccess", "ApiCallFailure")
| summarize ApiSuccessRate = 100.0 * countif(customDimensions.EventName == "ApiCallSuccess") / count()
| where ApiSuccessRate < 85
```

### **Alert Examples**

#### **High Error Rate Alert**
```
🚨 SignalBooster Alert: High Failure Rate

Condition: Error rate > 15% in last 15 minutes
Current: 23.4% (47 failures out of 201 attempts)
Time: 2025-01-15 14:45:00 UTC

Top Errors:
• NetworkTimeout: 28 occurrences
• ValidationFailure: 12 occurrences  
• ParseError: 7 occurrences

Investigate with:
traces | where timestamp > ago(15m) 
| where customDimensions.EventName == "ProcessingFailure"
| summarize count() by tostring(customDimensions.ErrorCode)
```

#### **Performance Degradation Alert**
```
⚡ SignalBooster Alert: Slow Processing

Condition: P95 processing time > 5 seconds
Current: P95 = 7.2 seconds (threshold: 5.0s)
Affected: 23 requests in last 10 minutes

Slow Operations:
• CorrelationId: corr-abc123 → 12.3s
• CorrelationId: corr-def456 → 8.9s
• CorrelationId: corr-ghi789 → 7.8s
```

---

## 🏥 **Healthcare-Specific Analytics**

### **Provider Performance Analysis**
```kql
traces
| where customDimensions.EventName == "ProcessingSuccess"
| summarize 
    ProcessedCount = count(),
    AvgProcessingTime = avg(todouble(customDimensions.ProcessingTimeMs)),
    DeviceTypes = dcount(tostring(customDimensions.DeviceType))
by OrderingProvider = tostring(customDimensions.OrderingProvider)
| order by ProcessedCount desc
```

### **Device Utilization Trends**
```kql
traces
| where timestamp > ago(30d)
| where customDimensions.EventName == "DeviceParsed"
| summarize DeviceCount = count() by 
    DeviceType = tostring(customDimensions.DeviceType),
    Week = bin(timestamp, 7d)
| order by Week desc, DeviceCount desc
```

### **Compliance Monitoring**
```kql
traces
| where customDimensions.EventName == "ProcessingSuccess"
| summarize 
    TotalProcessed = count(),
    UniqueProviders = dcount(tostring(customDimensions.OrderingProvider)),
    UniquePatients = dcount(tostring(customDimensions.PatientName))
by bin(timestamp, 1d)
| extend ProcessingVolume = TotalProcessed
```

---

## 🔍 **Troubleshooting Workflow**

### **Step 1: Identify Failed Request**
```kql
traces
| where customDimensions.EventName == "ProcessingFailure"
| where timestamp > ago(1h)
| project timestamp, 
          CorrelationId = tostring(customDimensions.CorrelationId), 
          ErrorCode = tostring(customDimensions.ErrorCode),
          FileName = tostring(customDimensions.FileName)
| take 10
```

### **Step 2: Get End-to-End Flow**
```kql
// Use CorrelationId from step 1
let correlationId = "corr-456789abc";
traces
| where customDimensions.CorrelationId == correlationId
| project timestamp, 
          EventName = tostring(customDimensions.EventName), 
          message, 
          ProcessingTimeMs = tostring(customDimensions.ProcessingTimeMs),
          ErrorCode = tostring(customDimensions.ErrorCode)
| order by timestamp asc
```

**Example trace flow:**
```
14:23:45 ProcessingStarted    → "Started processing physician_note1.txt"
14:23:46 DeviceParsed        → "CPAP device identified"  
14:23:47 ApiCallAttempt      → "Attempting API call (1/3)"
14:23:49 ApiCallAttempt      → "Attempting API call (2/3)"
14:23:52 ApiCallAttempt      → "Attempting API call (3/3)"
14:23:55 ApiCallFailure      → "Network timeout after 30s"
14:23:55 ProcessingFailure   → "Failed with Api.NetworkError"
```

### **Step 3: Analyze Similar Issues**
```kql
traces
| where timestamp > ago(24h)
| where customDimensions.EventName == "ProcessingFailure"
| where customDimensions.ErrorCode == "Api.NetworkError"  // From step 2
| summarize 
    FailureCount = count(),
    AffectedFiles = dcount(tostring(customDimensions.FileName)),
    AvgProcessingTime = avg(todouble(customDimensions.ProcessingTimeMs))
by bin(timestamp, 1h)
| order by timestamp desc
```

---

## 🎯 **Best Practices**

### **1. Correlation Tracking**
✅ **Always use correlation IDs** for end-to-end request tracking  
✅ **Include operation IDs** for individual operation tracking  
✅ **Pass correlation context** through all service calls  

```csharp
var operationId = Guid.NewGuid().ToString();
var correlationId = Guid.NewGuid().ToString();

logger.LogPhysicianNoteProcessingStarted(filePath, operationId, correlationId);
// ... processing ...
logger.LogPhysicianNoteProcessingCompleted(filePath, deviceType, orderId, status, elapsed, operationId, correlationId);
```

### **2. Business Context**
✅ **Include healthcare-specific data** (device types, providers)  
✅ **Log business outcomes** not just technical events  
✅ **Provide enough context** for business users to understand logs  

```csharp
logger.LogDeviceParsed("CPAP", "John Doe", "Dr. Mitchell", specifications, operationId, correlationId);
```

### **3. Performance Metrics**
✅ **Always log processing times** with business context  
✅ **Include attempt counts** for retry scenarios  
✅ **Track response times** for external dependencies  

```csharp
logger.LogApiCallSuccess(endpoint, deviceType, orderId, status, responseTime, attemptNumber, operationId, correlationId);
```

### **4. Error Details**
✅ **Categorize errors** by type and severity  
✅ **Include actionable information** in error messages  
✅ **Provide context** for troubleshooting  

```csharp
logger.LogApiCallFailure(endpoint, deviceType, "NetworkTimeout", errorMessage, responseTime, attempt, maxAttempts, operationId, correlationId, exception);
```

### **5. Privacy & Security**
✅ **No PII in logs** (use patient IDs, not names for external logs)  
✅ **Sanitize sensitive data** before logging  
✅ **Use appropriate log levels** for sensitive operations  

---

## ⚙️ **Setup & Configuration**

### **File Logging (Default)**
```bash
# Uses appsettings.json configuration
dotnet run

# Logs written to: logs/signalbooster-YYYYMMDD.log
# Console output: Enabled by default
```

### **Application Insights Setup**

1. **Create Azure Resource:**
```bash
az monitor app-insights component create \
    --app SignalBooster-DME-Processor \
    --location "East US" \
    --resource-group "your-rg"
```

2. **Install Package:**
```bash
dotnet add package Serilog.Sinks.ApplicationInsights
```

3. **Update Configuration:**
```json
{
  "ApplicationInsights": {
    "ConnectionString": "YOUR_CONNECTION_STRING_HERE"
  }
}
```

4. **Run with Application Insights:**
```bash
dotnet run --configuration ApplicationInsights
```

---

## 📞 **Support & Resources**

### **Useful Queries**
- Complete query collection: `ApplicationInsights-Queries.kql`
- All 15 pre-built queries for monitoring and analytics

### **Configuration Files**
- **Basic logging**: `appsettings.json`
- **Application Insights**: `appsettings.ApplicationInsights.json`
- **Examples**: `ApplicationInsights-Examples.md`

### **Troubleshooting**
1. **Missing logs**: Check file permissions and log directory creation
2. **Application Insights not receiving data**: Verify connection string and network connectivity
3. **Performance issues**: Adjust sampling rates in configuration
4. **Query timeouts**: Use time filters and limit result sets

### **Log File Locations**
```
📁 Project Root/
├── logs/                              # Log files directory
│   ├── signalbooster-20250115.log     # Daily log files
│   └── signalbooster-20250114.log
├── appsettings.json                   # Basic configuration
├── appsettings.ApplicationInsights.json # AI configuration
├── ApplicationInsights-Queries.kql    # Query collection
└── LOGGING-GUIDE.md                   # This document
```

---

**💡 Remember**: This logging system transforms Application Insights from a generic monitoring tool into a **healthcare-specific business intelligence platform** with rich context, powerful analytics, and proactive alerting! 🏥📊