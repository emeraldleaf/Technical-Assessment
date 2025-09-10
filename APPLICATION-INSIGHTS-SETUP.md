# 📊 Application Insights Setup for SignalBooster

This guide shows how to configure Application Insights for optimal monitoring, troubleshooting, and analytics of the SignalBooster DME processing application.

## 🚀 Quick Setup

### 1. **Azure Application Insights Resource**
```bash
# Create Application Insights resource
az monitor app-insights component create \
    --app SignalBooster-DME-Processor \
    --location "East US" \
    --resource-group "your-resource-group" \
    --application-type web \
    --retention-time 90
```

### 2. **Get Connection String**
```bash
# Get the connection string
az monitor app-insights component show \
    --app SignalBooster-DME-Processor \
    --resource-group "your-resource-group" \
    --query "connectionString" -o tsv
```

### 3. **Update Configuration**
Replace the placeholder in `appsettings.ApplicationInsights.json`:
```json
{
  "ApplicationInsights": {
    "ConnectionString": "YOUR_ACTUAL_CONNECTION_STRING_HERE"
  }
}
```

### 4. **Install NuGet Package**
```bash
dotnet add package Serilog.Sinks.ApplicationInsights
```

### 5. **Use Application Insights Configuration**
```bash
# Run with Application Insights configuration
dotnet run --configuration ApplicationInsights
```

## 📈 **What You Get**

### **Structured Telemetry**
All logs include consistent dimensions for powerful querying:

```json
{
  "EventName": "ProcessingSuccess",
  "FileName": "physician_note1.txt",
  "DeviceType": "CPAP", 
  "OrderId": "12345",
  "ProcessingTimeMs": 1250,
  "OperationId": "op-123",
  "CorrelationId": "corr-456",
  "PatientName": "John Doe",
  "OrderingProvider": "Dr. Smith"
}
```

### **Key Event Types**
- `ProcessingSuccess` / `ProcessingFailure` - End-to-end processing
- `ApiCallSuccess` / `ApiCallFailure` - External API interactions  
- `DeviceParsed` - Device extraction from notes
- `ValidationFailure` - Input validation errors

## 🔍 **Essential Queries**

### **Success Rate Overview**
```kql
traces
| where timestamp > ago(24h)
| where customDimensions.EventName in ("ProcessingSuccess", "ProcessingFailure")
| summarize 
    SuccessRate = round(100.0 * countif(customDimensions.EventName == "ProcessingSuccess") / count(), 2),
    TotalProcessed = count()
```

### **Performance Monitoring**
```kql
traces
| where customDimensions.EventName == "ProcessingSuccess"
| extend ProcessingTime = todouble(customDimensions.ProcessingTimeMs)
| summarize 
    AvgTime = avg(ProcessingTime),
    P95Time = percentile(ProcessingTime, 95)
by bin(timestamp, 5m)
| render timechart
```

### **Error Analysis**
```kql
traces 
| where customDimensions.EventName == "ProcessingFailure"
| summarize count() by 
    ErrorCode = tostring(customDimensions.ErrorCode),
    ErrorDescription = tostring(customDimensions.ErrorDescription)
| order by count_ desc
```

### **Device Type Distribution**
```kql
traces
| where customDimensions.EventName == "ProcessingSuccess"
| summarize count() by DeviceType = tostring(customDimensions.DeviceType)
| render piechart
```

## 📊 **Dashboards & Alerts**

### **Performance Alert**
```kql
// Alert when processing time > 5 seconds
traces
| where timestamp > ago(5m)
| where todouble(customDimensions.ProcessingTimeMs) > 5000
| summarize count()
```

### **Error Rate Alert** 
```kql
// Alert when error rate > 5%
traces
| where timestamp > ago(15m)
| where customDimensions.EventName in ("ProcessingSuccess", "ProcessingFailure")
| summarize ErrorRate = 100.0 * countif(customDimensions.EventName == "ProcessingFailure") / count()
| where ErrorRate > 5
```

### **API Health Check**
```kql
// Monitor API availability
traces
| where customDimensions.EventName in ("ApiCallSuccess", "ApiCallFailure")
| summarize SuccessRate = 100.0 * countif(customDimensions.EventName == "ApiCallSuccess") / count()
by bin(timestamp, 5m)
| render timechart
```

## 🔧 **Advanced Configuration**

### **Custom Sampling**
```json
{
  "ApplicationInsights": {
    "SamplingSettings": {
      "SamplingPercentage": 100.0,
      "MaxTelemetryItemsPerSecond": 20
    }
  }
}
```

### **Performance Counters**
```json
{
  "ApplicationInsights": {
    "EnablePerformanceCounters": true,
    "EnableDependencyTracking": true
  }
}
```

## 📋 **Best Practices**

### **1. Use Correlation IDs**
Every operation gets a unique correlation ID for end-to-end tracking:
```csharp
logger.LogPhysicianNoteProcessingStarted(filePath, operationId, correlationId);
```

### **2. Include Business Context**
Log business-relevant data for analytics:
- Device types processed
- Provider information  
- Patient demographics (anonymized)
- Processing outcomes

### **3. Structure for Queries**
Use consistent property names and values:
- `EventName` for categorizing events
- `DeviceType` for medical device classification
- `ProcessingTimeMs` for performance metrics

### **4. Error Categorization**
Structure error information:
- `ErrorCode` - Machine readable error type
- `ErrorDescription` - Human readable description  
- `ErrorType` - High level category (Validation, Network, etc.)

## 🏥 **Healthcare-Specific Monitoring**

### **Compliance Tracking**
```kql
traces
| where customDimensions.EventName == "ProcessingSuccess"
| summarize 
    ProcessedCount = count(),
    UniqueProviders = dcount(tostring(customDimensions.OrderingProvider))
by bin(timestamp, 1d)
```

### **Device Utilization Analysis**
```kql
traces
| where customDimensions.EventName == "DeviceParsed"
| summarize DeviceCount = count() by 
    DeviceType = tostring(customDimensions.DeviceType),
    Provider = tostring(customDimensions.OrderingProvider)
| order by DeviceCount desc
```

### **Processing Volume Trends**
```kql
traces
| where customDimensions.EventName == "ProcessingSuccess"
| summarize HourlyVolume = count() by bin(timestamp, 1h)
| render columnchart
```

## 🔐 **Security & Privacy**

- Patient information is logged with anonymization
- Correlation IDs enable tracking without exposing PII
- Configurable retention periods (30-730 days)
- Role-based access to telemetry data

## 📞 **Support**

For issues with Application Insights integration:
1. Check connection string configuration
2. Verify NuGet package installation
3. Review sampling settings if missing data
4. Use correlation IDs for specific request tracking

**Query Collection:** All useful queries are provided in `ApplicationInsights-Queries.kql`