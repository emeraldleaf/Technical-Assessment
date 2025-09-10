# 📊 Application Insights Log Examples - SignalBooster

This document shows exactly how the structured logs appear in Application Insights and how to use them for monitoring.

## 🔍 **1. Logs Table View in Application Insights**

When you go to **Application Insights → Logs** and run a basic query:

```kql
traces | where timestamp > ago(1h) | take 10
```

**You'll see a table like this:**

| timestamp | severityLevel | message | customDimensions |
|-----------|---------------|---------|------------------|
| 2025-01-15T14:23:45.123Z | 1 (Info) | PhysicianNoteProcessing started for physician_note1.txt | {"EventName":"ProcessingStarted","FileName":"physician_note1.txt","OperationId":"op-abc123",...} |
| 2025-01-15T14:23:46.456Z | 1 (Info) | Device parsed from note | {"EventName":"DeviceParsed","DeviceType":"CPAP","PatientName":"John Doe",...} |
| 2025-01-15T14:23:47.789Z | 1 (Info) | API call attempt | {"EventName":"ApiCallAttempt","Endpoint":"https://alert-api.com/DrExtract","AttemptNumber":"1",...} |
| 2025-01-15T14:23:48.012Z | 3 (Error) | API call failed | {"EventName":"ApiCallFailure","ErrorType":"NetworkError","ResponseTimeMs":"2500",...} |

## 🎯 **2. Expanded Custom Dimensions**

When you click on a log entry, the **customDimensions** expands to show:

### **Successful Processing Log:**
```json
{
  "EventName": "ProcessingSuccess",
  "FileName": "physician_note1.txt",
  "FilePath": "/app/data/physician_note1.txt",
  "DeviceType": "CPAP",
  "OrderId": "ORD-789456123",
  "Status": "Accepted",
  "ProcessingTimeMs": "1247.8",
  "OperationId": "op-abc123def",
  "CorrelationId": "corr-456789abc",
  "Application": "SignalBooster",
  "Environment": "Production",
  "MachineName": "signalbooster-prod-01",
  "ProcessId": "12345",
  "ThreadId": "8",
  "SessionId": "sess-98765"
}
```

### **Device Parsing Log:**
```json
{
  "EventName": "DeviceParsed", 
  "DeviceType": "CPAP",
  "PatientName": "John Doe",
  "OrderingProvider": "Dr. Sarah Mitchell",
  "SpecificationCount": "3",
  "Specifications": "MaskType, Pressure, Humidifier",
  "OperationId": "op-abc123def",
  "CorrelationId": "corr-456789abc"
}
```

### **API Failure Log:**
```json
{
  "EventName": "ApiCallFailure",
  "Endpoint": "https://alert-api.com/DrExtract", 
  "DeviceType": "CPAP",
  "ErrorType": "NetworkTimeout",
  "ErrorMessage": "The operation was canceled",
  "ResponseTimeMs": "30000",
  "AttemptNumber": "3",
  "MaxAttempts": "3",
  "OperationId": "op-abc123def",
  "CorrelationId": "corr-456789abc"
}
```

## 📈 **3. Analytics Queries with Results**

### **Success Rate Dashboard Query:**
```kql
traces
| where timestamp > ago(24h)
| where customDimensions.EventName in ("ProcessingSuccess", "ProcessingFailure")
| summarize 
    TotalProcessed = count(),
    SuccessCount = countif(customDimensions.EventName == "ProcessingSuccess"),
    FailureCount = countif(customDimensions.EventName == "ProcessingFailure")
| extend SuccessRate = round(100.0 * SuccessCount / TotalProcessed, 2)
```

**Results Table:**
| TotalProcessed | SuccessCount | FailureCount | SuccessRate |
|----------------|--------------|--------------|-------------|
| 1,247 | 1,089 | 158 | 87.33 |

### **Device Type Distribution:**
```kql
traces
| where timestamp > ago(7d)
| where customDimensions.EventName == "ProcessingSuccess"
| summarize ProcessedCount = count() by DeviceType = tostring(customDimensions.DeviceType)
| order by ProcessedCount desc
```

**Results Table:**
| DeviceType | ProcessedCount |
|------------|----------------|
| CPAP | 892 |
| Oxygen | 234 |
| Wheelchair | 156 |
| BiPAP | 89 |
| Nebulizer | 45 |
| Walker | 23 |

### **Performance Analysis:**
```kql
traces
| where timestamp > ago(24h)
| where customDimensions.EventName == "ProcessingSuccess"
| extend ProcessingTime = todouble(customDimensions.ProcessingTimeMs)
| summarize 
    AvgTime = round(avg(ProcessingTime), 1),
    P50Time = round(percentile(ProcessingTime, 50), 1),
    P95Time = round(percentile(ProcessingTime, 95), 1),
    MaxTime = round(max(ProcessingTime), 1)
by bin(timestamp, 1h)
| order by timestamp desc
```

**Results showing hourly performance:**
| timestamp | AvgTime | P50Time | P95Time | MaxTime |
|-----------|---------|---------|---------|---------|
| 2025-01-15T14:00:00Z | 1,247.3 | 1,156.0 | 2,890.5 | 8,945.2 |
| 2025-01-15T13:00:00Z | 987.6 | 923.4 | 2,156.8 | 4,567.1 |
| 2025-01-15T12:00:00Z | 1,534.7 | 1,423.9 | 3,456.2 | 12,334.5 |

## 📊 **4. Application Insights Workbook View**

In **Application Insights → Workbooks**, you can create dashboards that look like:

### **Executive Summary Panel:**
```
┌─ SignalBooster Health Dashboard ─────────────────────────┐
│                                                          │
│ 📈 Last 24 Hours:                                       │
│ • Total Processed: 1,247 notes                          │
│ • Success Rate: 87.33%                                   │
│ • Avg Processing Time: 1,247ms                          │ 
│ • Peak Volume: 89 notes/hour                            │
│                                                          │
│ 🏥 Device Breakdown:                                     │
│ • CPAP: 892 (71.5%)                                     │
│ • Oxygen: 234 (18.8%)                                   │
│ • Other DME: 121 (9.7%)                                 │
│                                                          │
│ ⚠️  Active Issues:                                       │
│ • API Timeout Rate: 12.7% (elevated)                    │
│ • Slow Processing: 23 notes > 5sec                      │
└──────────────────────────────────────────────────────────┘
```

### **Real-Time Processing Chart:**
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

## 🚨 **5. Alert Examples**

### **High Error Rate Alert:**
```
🚨 SignalBooster Alert: High Failure Rate

Condition: Error rate > 15% in last 15 minutes
Current: 23.4% (47 failures out of 201 attempts)
Time: 2025-01-15 14:45:00 UTC

Top Errors:
• NetworkTimeout: 28 occurrences
• ValidationFailure: 12 occurrences  
• ParseError: 7 occurrences

Query to investigate:
traces | where timestamp > ago(15m) 
| where customDimensions.EventName == "ProcessingFailure"
| summarize count() by tostring(customDimensions.ErrorCode)
```

### **Performance Degradation Alert:**
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

## 🔍 **6. Troubleshooting Workflow**

### **Step 1: Find Failing Request**
```kql
traces
| where customDimensions.EventName == "ProcessingFailure"
| where timestamp > ago(1h)
| project timestamp, CorrelationId = tostring(customDimensions.CorrelationId), 
          ErrorCode = tostring(customDimensions.ErrorCode)
| take 10
```

### **Step 2: Get Full Request Context**
```kql
// Use CorrelationId from step 1
let correlationId = "corr-456789abc";
traces
| where customDimensions.CorrelationId == correlationId
| project timestamp, EventName = tostring(customDimensions.EventName), 
          message, customDimensions
| order by timestamp asc
```

**This shows the complete flow:**
```
14:23:45 ProcessingStarted    → "Started processing physician_note1.txt"
14:23:46 DeviceParsed        → "CPAP device identified"  
14:23:47 ApiCallAttempt      → "Attempting API call (1/3)"
14:23:49 ApiCallAttempt      → "Attempting API call (2/3)"
14:23:52 ApiCallAttempt      → "Attempting API call (3/3)"
14:23:55 ApiCallFailure      → "Network timeout after 30s"
14:23:55 ProcessingFailure   → "Failed with Api.NetworkError"
```

## 📱 **7. Mobile Application Insights View**

On the **Application Insights mobile app**, you'll see:

```
📱 SignalBooster Overview

🟢 Health: Good
📊 Last Hour: 89 processed
⚡ Avg Time: 1.2s
❌ Errors: 8 (9.0%)

Recent Issues:
🔴 NetworkTimeout x3
🟡 SlowProcessing x2
🟠 ValidationError x3

Tap for details →
```

This structured approach makes Application Insights incredibly powerful for monitoring your healthcare application! 🏥📊