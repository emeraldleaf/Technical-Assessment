using System.Text.RegularExpressions;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBooster.Mvp.Configuration;
using SignalBooster.Mvp.Models;
using System.Text.Json;

namespace SignalBooster.Mvp.Services;

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
            _logger.LogInformation("Using fallback regex parser");
            return ParseDeviceOrder(noteText);
        }

        try
        {
            _logger.LogInformation("Using OpenAI LLM for device order extraction");
            
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

            var response = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions);
            var content = response.Value.Choices[0].Message.Content;

            var deviceOrder = ParseLlmResponse(content);
            _logger.LogInformation("Successfully extracted device order using OpenAI");
            
            return deviceOrder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to use OpenAI for extraction, falling back to regex parser");
            return ParseDeviceOrder(noteText);
        }
    }

    private string CreateExtractionPrompt(string noteText)
    {
        return $@"Extract medical device information from the following physician note and return as JSON:

Physician Note:
{noteText}

Return ONLY a JSON object with these fields (omit null/empty fields):
- device: Device type (""CPAP"", ""Oxygen Tank"", ""Wheelchair"", etc.)
- patient_name, dob, diagnosis, ordering_provider
- liters: For oxygen (""2 L"")
- usage: When used (""sleep and exertion"")
- mask_type: For CPAP (""full face"")
- add_ons: Array of features ([""humidifier""])
- qualifier: Medical qualifiers (""AHI > 20"")

Return valid JSON only.";
    }

    private DeviceOrder ParseLlmResponse(string llmResponse)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(llmResponse);
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

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String 
            ? prop.GetString() 
            : null;
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