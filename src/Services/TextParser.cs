using System.Text.RegularExpressions;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBooster.Configuration;
using SignalBooster.Models;
using System.Text.Json;

namespace SignalBooster.Services;

public class TextParser : ITextParser
{
    private readonly OpenAIClient? _openAIClient;
    private readonly OpenAIOptions _options;
    private readonly ILogger<TextParser> _logger;

    public TextParser(IOptions<SignalBoosterOptions> options, ILogger<TextParser> logger)
    {
        _options = options.Value.OpenAI;
        _logger = logger;
        
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _openAIClient = new OpenAIClient(_options.ApiKey);
        }
    }

    public DeviceOrder ParseDeviceOrder(string noteText)
    {
        var device = DetermineDeviceType(noteText);
        var orderingProvider = ExtractOrderingProvider(noteText);
        var patientName = ExtractPatientName(noteText);
        var dob = ExtractDateOfBirth(noteText);
        var diagnosis = ExtractDiagnosis(noteText);
        
        var deviceOrder = new DeviceOrder
        {
            Device = device,
            OrderingProvider = orderingProvider,
            PatientName = patientName,
            Dob = dob,
            Diagnosis = diagnosis
        };
        
        return device.ToUpperInvariant() switch
        {
            "CPAP" => ParseCpapDetails(noteText, deviceOrder),
            "OXYGEN TANK" => ParseOxygenDetails(noteText, deviceOrder),
            _ => deviceOrder
        };
    }

    public async Task<DeviceOrder> ParseDeviceOrderAsync(string noteText)
    {
        if (_openAIClient == null)
        {
            _logger.LogInformation("[{Class}.{Method}] OpenAI client not configured, using fallback regex parser",
                nameof(TextParser), nameof(ParseDeviceOrderAsync));
            return ParseDeviceOrder(noteText);
        }

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            _logger.LogInformation("[{Class}.{Method}] Step 1: Configuring OpenAI LLM extraction. Model: {Model}, MaxTokens: {MaxTokens}, Temperature: {Temperature}",
                nameof(TextParser), nameof(ParseDeviceOrderAsync), _options.Model, _options.MaxTokens, _options.Temperature);
            
            _logger.LogInformation("[{Class}.{Method}] Step 2: Creating structured extraction prompt for {NoteLength} character note",
                nameof(TextParser), nameof(ParseDeviceOrderAsync), noteText.Length);
            
            var prompt = CreateExtractionPrompt(noteText);
            
            var chatCompletionsOptions = new ChatCompletionsOptions()
            {
                DeploymentName = _options.Model,
                Messages = { 
                    new ChatRequestSystemMessage("You are a medical device extraction assistant."), 
                    new ChatRequestUserMessage(prompt) 
                },
                MaxTokens = _options.MaxTokens,
                Temperature = _options.Temperature
            };

            _logger.LogInformation("[{Class}.{Method}] Step 3: Calling OpenAI API for device extraction",
                nameof(TextParser), nameof(ParseDeviceOrderAsync));

            var response = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions);
            var content = response.Value.Choices[0].Message.Content;
            
            stopwatch.Stop();
            
            // Log OpenAI usage metrics
            var usage = response.Value.Usage;
            _logger.LogInformation("[{Class}.{Method}] Step 4: OpenAI API call completed. Duration: {DurationMs}ms, PromptTokens: {PromptTokens}, CompletionTokens: {CompletionTokens}, TotalTokens: {TotalTokens}",
                nameof(TextParser), nameof(ParseDeviceOrderAsync), stopwatch.ElapsedMilliseconds, usage?.PromptTokens ?? 0, usage?.CompletionTokens ?? 0, usage?.TotalTokens ?? 0);

            _logger.LogInformation("[{Class}.{Method}] Step 5: Parsing LLM JSON response into DeviceOrder object",
                nameof(TextParser), nameof(ParseDeviceOrderAsync));

            var deviceOrder = ParseLlmResponse(content);
            _logger.LogInformation("[{Class}.{Method}] Step 6: Successfully extracted device order using OpenAI. Device: {DeviceType}, Patient: {PatientName}",
                nameof(TextParser), nameof(ParseDeviceOrderAsync), deviceOrder.Device, deviceOrder.PatientName);
            
            return deviceOrder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Class}.{Method}] Step FAILED: OpenAI extraction failed, falling back to regex parser. Error: {ErrorMessage}",
                nameof(TextParser), nameof(ParseDeviceOrderAsync), ex.Message);
            return ParseDeviceOrder(noteText);
        }
    }

    private string CreateExtractionPrompt(string noteText)
    {
        return $@"Extract medical device information from the following physician note and return as JSON:

Physician Note:
{noteText}

Return ONLY a JSON object with these fields (omit null/empty fields):
- device: Device type (""CPAP"", ""Oxygen Tank"", ""Wheelchair"", ""Hospital Bed"", ""Ventilator"", ""TENS Unit"", ""Commode"", ""Blood Glucose Monitor"", etc.)
- patient_name, dob, diagnosis, ordering_provider
- liters: For oxygen (""2 L"")
- usage: When used (""sleep and exertion"")
- mask_type: For CPAP (""full face"")
- add_ons: Array of features ([""humidifier"", ""side rails"", ""pressure relief""])
- qualifier: Medical qualifiers (""AHI > 20"", ""pressure sore risk"", ""diabetes management"")

Return valid JSON only.";
    }

    private DeviceOrder ParseLlmResponse(string llmResponse)
    {
        try
        {
            // Clean up the LLM response - remove markdown code blocks and extra formatting
            var cleanedResponse = CleanLlmResponse(llmResponse);
            var jsonDoc = JsonDocument.Parse(cleanedResponse);
            var root = jsonDoc.RootElement;

            var addOns = new List<string>();
            if (root.TryGetProperty("add_ons", out var addOnsElement) && addOnsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in addOnsElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && item.GetString() is string addOn)
                        addOns.Add(addOn);
                }
            }

            return new DeviceOrder
            {
                Device = GetStringProperty(root, "device") ?? "Unknown",
                PatientName = GetStringProperty(root, "patient_name"),
                Dob = GetStringProperty(root, "dob"),
                Diagnosis = GetStringProperty(root, "diagnosis"),
                OrderingProvider = GetStringProperty(root, "ordering_provider") ?? "Dr. Unknown",
                Liters = GetStringProperty(root, "liters"),
                Usage = GetStringProperty(root, "usage"),
                MaskType = GetStringProperty(root, "mask_type"),
                Qualifier = GetStringProperty(root, "qualifier"),
                AddOns = addOns.Count > 0 ? addOns.ToArray() : null
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse LLM JSON response, falling back to regex parser");
            return ParseDeviceOrder(llmResponse);
        }
    }

    private static string CleanLlmResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return response;

        // Remove markdown code block markers
        response = response.Trim();
        if (response.StartsWith("```json"))
            response = response.Substring(7);
        else if (response.StartsWith("```"))
            response = response.Substring(3);
            
        if (response.EndsWith("```"))
            response = response.Substring(0, response.Length - 3);
            
        return response.Trim();
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            var value = prop.GetString();
            if (value != null)
            {
                // Clean up common JSON parsing issues
                value = value.Trim()
                           .TrimEnd(',')  // Remove trailing commas
                           .Trim('"')     // Remove extra quotes
                           .Trim();       // Final trim
                
                // Handle empty strings
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }
        return null;
    }
    
    private static string DetermineDeviceType(string text)
    {
        // CPAP and BiPAP devices
        if (text.Contains("CPAP", StringComparison.OrdinalIgnoreCase) || 
            text.Contains("continuous positive airway pressure", StringComparison.OrdinalIgnoreCase))
            return "CPAP";
        
        if (text.Contains("BiPAP", StringComparison.OrdinalIgnoreCase) || 
            text.Contains("bilevel", StringComparison.OrdinalIgnoreCase))
            return "BiPAP";
        
        // Oxygen devices
        if (text.Contains("oxygen", StringComparison.OrdinalIgnoreCase) || 
            text.Contains("O2", StringComparison.OrdinalIgnoreCase))
            return "Oxygen Tank";
        
        // Other devices
        if (text.Contains("wheelchair", StringComparison.OrdinalIgnoreCase))
            return "Wheelchair";
        
        if (text.Contains("walker", StringComparison.OrdinalIgnoreCase) || 
            text.Contains("rollator", StringComparison.OrdinalIgnoreCase))
            return "Walker";
        
        if (text.Contains("nebulizer", StringComparison.OrdinalIgnoreCase))
            return "Nebulizer";
        
        // Hospital bed and mattress
        if (text.Contains("hospital bed", StringComparison.OrdinalIgnoreCase) || 
            text.Contains("adjustable bed", StringComparison.OrdinalIgnoreCase))
            return "Hospital Bed";
        
        if (text.Contains("mattress", StringComparison.OrdinalIgnoreCase) || 
            text.Contains("pressure relieving", StringComparison.OrdinalIgnoreCase))
            return "Pressure Relief Mattress";
        
        // Mobility devices
        if (text.Contains("crutches", StringComparison.OrdinalIgnoreCase))
            return "Crutches";
        
        if (text.Contains("cane", StringComparison.OrdinalIgnoreCase) || 
            text.Contains("walking stick", StringComparison.OrdinalIgnoreCase))
            return "Cane";
        
        if (text.Contains("scooter", StringComparison.OrdinalIgnoreCase) || 
            text.Contains("mobility scooter", StringComparison.OrdinalIgnoreCase))
            return "Mobility Scooter";
        
        // Respiratory devices
        if (text.Contains("suction", StringComparison.OrdinalIgnoreCase) || 
            text.Contains("aspirator", StringComparison.OrdinalIgnoreCase))
            return "Suction Machine";
        
        if (text.Contains("ventilator", StringComparison.OrdinalIgnoreCase) || 
            text.Contains("respirator", StringComparison.OrdinalIgnoreCase))
            return "Ventilator";
        
        if (text.Contains("pulse oximeter", StringComparison.OrdinalIgnoreCase) || 
            text.Contains("oxygen monitor", StringComparison.OrdinalIgnoreCase))
            return "Pulse Oximeter";
        
        // Therapy devices
        if (text.Contains("tens", StringComparison.OrdinalIgnoreCase) || 
            text.Contains("electrical stimulation", StringComparison.OrdinalIgnoreCase))
            return "TENS Unit";
        
        if (text.Contains("compression pump", StringComparison.OrdinalIgnoreCase) || 
            text.Contains("lymphedema pump", StringComparison.OrdinalIgnoreCase))
            return "Compression Pump";
        
        // Bathroom safety
        if (text.Contains("commode", StringComparison.OrdinalIgnoreCase) || 
            text.Contains("bedside toilet", StringComparison.OrdinalIgnoreCase))
            return "Commode";
        
        if (text.Contains("shower chair", StringComparison.OrdinalIgnoreCase) || 
            text.Contains("bath bench", StringComparison.OrdinalIgnoreCase))
            return "Shower Chair";
        
        if (text.Contains("toilet seat", StringComparison.OrdinalIgnoreCase) && 
            text.Contains("raised", StringComparison.OrdinalIgnoreCase))
            return "Raised Toilet Seat";
        
        // Monitoring devices
        if (text.Contains("blood glucose", StringComparison.OrdinalIgnoreCase) || 
            text.Contains("glucometer", StringComparison.OrdinalIgnoreCase))
            return "Blood Glucose Monitor";
        
        if (text.Contains("blood pressure", StringComparison.OrdinalIgnoreCase) && 
            text.Contains("monitor", StringComparison.OrdinalIgnoreCase))
            return "Blood Pressure Monitor";
        
        return "Unknown";
    }
    
    private static DeviceOrder ParseCpapDetails(string text, DeviceOrder order)
    {
        var maskType = text.Contains("full face", StringComparison.OrdinalIgnoreCase) ? "full face" : order.MaskType;
        var addOns = text.Contains("humidifier", StringComparison.OrdinalIgnoreCase) ? new[] { "humidifier" } : order.AddOns;
        var qualifier = text.Contains("AHI > 20") ? "AHI > 20" : order.Qualifier;
        
        return order with { MaskType = maskType, AddOns = addOns, Qualifier = qualifier };
    }
    
    private static DeviceOrder ParseOxygenDetails(string text, DeviceOrder order)
    {
        var literMatch = Regex.Match(text, @"(\d+(?:\.\d+)?)\s*L(?:\s*per\s*minute)?", RegexOptions.IgnoreCase);
        var liters = literMatch.Success ? literMatch.Groups[1].Value + " L" : order.Liters;
        
        var usage = new List<string>();
        if (text.Contains("sleep", StringComparison.OrdinalIgnoreCase))
            usage.Add("sleep");
        if (text.Contains("exertion", StringComparison.OrdinalIgnoreCase))
            usage.Add("exertion");
        
        var usageString = usage.Count > 0 ? string.Join(" and ", usage) : order.Usage;
        
        return order with { Liters = liters, Usage = usageString };
    }
    
    private static string ExtractOrderingProvider(string text)
    {
        var patterns = new[]
        {
            @"Ordering Physician:\s*(.+?)(?:\r?\n|$)",
            @"(?:Ordered by|Dr\.)\s*(.+?)\.?$"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (match.Success)
            {
                var provider = match.Groups[1].Value.Trim().TrimEnd('.');
                return provider.StartsWith("Dr.") ? provider : $"Dr. {provider}";
            }
        }
        return "Dr. Unknown";
    }
    
    private static string ExtractPatientName(string text)
    {
        var match = Regex.Match(text, @"Patient\s*Name:\s*(.+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "Unknown";
    }
    
    private static string ExtractDateOfBirth(string text)
    {
        var match = Regex.Match(text, @"DOB:\s*(.+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "Unknown";
    }
    
    private static string ExtractDiagnosis(string text)
    {
        var match = Regex.Match(text, @"Diagnosis:\s*(.+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "Unknown";
    }
}