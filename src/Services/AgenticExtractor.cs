using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBooster.Configuration;
using SignalBooster.Models;
using Azure.AI.OpenAI;
using System.Text.Json;
using System.Diagnostics;

namespace SignalBooster.Services;

/// <summary>
/// Advanced agentic AI extraction system with multi-agent reasoning
/// Implements autonomous agents for extraction, validation, and self-correction
/// </summary>
public class AgenticExtractor : IAgenticExtractor
{
    private readonly OpenAIClient? _openAIClient;
    private readonly OpenAIOptions _options;
    private readonly ILogger<AgenticExtractor> _logger;
    private readonly ITextParser _fallbackParser;

    // Agent definitions with specialized roles
    private readonly Dictionary<string, AgentDefinition> _agents;

    public AgenticExtractor(
        IOptions<SignalBoosterOptions> options,
        ILogger<AgenticExtractor> logger,
        ITextParser fallbackParser)
    {
        _options = options.Value.OpenAI;
        _logger = logger;
        _fallbackParser = fallbackParser;

        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _openAIClient = new OpenAIClient(_options.ApiKey);
        }

        _agents = InitializeAgents();
    }

    public async Task<AgenticExtractionResult> ExtractWithAgentsAsync(string noteText, ExtractionContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = Guid.NewGuid().ToString();
        var reasoningSteps = new List<AgentStep>();

        _logger.LogInformation("[AgenticExtractor] Starting multi-agent extraction. CorrelationId: {CorrelationId}, Mode: {Mode}",
            correlationId, context.Mode);

        try
        {
            // Step 1: Document Analysis Agent
            var analysisStep = await ExecuteAgentAsync("document_analyzer", noteText, context, correlationId);
            reasoningSteps.Add(analysisStep);

            // Step 2: Primary Extraction Agent
            var extractionStep = await ExecuteAgentAsync("primary_extractor", noteText, context, correlationId, analysisStep.Outputs);
            reasoningSteps.Add(extractionStep);

            // Step 3: Medical Validation Agent
            var validationStep = await ExecuteAgentAsync("medical_validator", noteText, context, correlationId, extractionStep.Outputs);
            reasoningSteps.Add(validationStep);

            // Step 4: Confidence Assessment Agent
            var confidenceStep = await ExecuteAgentAsync("confidence_assessor", noteText, context, correlationId,
                CombineOutputs(extractionStep.Outputs, validationStep.Outputs));
            reasoningSteps.Add(confidenceStep);

            // Debug: Log what the primary extractor returned
            _logger.LogInformation("[AgenticExtractor] Primary extractor outputs: {Outputs}",
                JsonSerializer.Serialize(extractionStep.Outputs, new JsonSerializerOptions { WriteIndented = true }));

            // Parse final device order from extraction outputs
            var deviceOrder = ParseDeviceOrderFromOutputs(extractionStep.Outputs);
            var confidenceScore = double.Parse(confidenceStep.Outputs.GetValueOrDefault("overall_confidence", "0.8").ToString()!);

            // Perform validation if required
            ValidationResult? validationResult = null;
            if (context.RequireValidation)
            {
                validationResult = await ValidateExtractionAsync(deviceOrder, noteText);

                // Self-correct if validation fails
                if (validationResult.ValidationScore < 0.7)
                {
                    _logger.LogInformation("[AgenticExtractor] Low validation score ({Score}), attempting self-correction",
                        validationResult.ValidationScore);
                    deviceOrder = await SelfCorrectAsync(deviceOrder, validationResult, noteText);
                }
            }

            stopwatch.Stop();

            var metadata = new ExtractionMetadata
            {
                ExtractorVersion = "AgenticExtractor_v1.0",
                ProcessingDuration = stopwatch.Elapsed,
                TokensUsed = reasoningSteps.Sum(s => Convert.ToInt32(s.Outputs.GetValueOrDefault("tokens_used", 0))),
                AgentsUsed = reasoningSteps.Select(s => s.AgentName).ToList(),
                AdditionalData = new Dictionary<string, object>
                {
                    ["ExtractionMode"] = context.Mode.ToString(),
                    ["RequiredValidation"] = context.RequireValidation,
                    ["ProcessingTimestamp"] = DateTime.UtcNow
                }
            };

            return new AgenticExtractionResult
            {
                DeviceOrder = deviceOrder,
                ConfidenceScore = confidenceScore,
                Metadata = metadata,
                ReasoningSteps = reasoningSteps,
                ValidationResult = validationResult
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AgenticExtractor] Multi-agent extraction failed, falling back to simple parser");

            // Fallback to simple extraction
            var fallbackOrder = await _fallbackParser.ParseDeviceOrderAsync(noteText);
            return new AgenticExtractionResult
            {
                DeviceOrder = fallbackOrder,
                ConfidenceScore = 0.5, // Lower confidence for fallback
                Metadata = new ExtractionMetadata
                {
                    ExtractorVersion = "FallbackParser",
                    ProcessingDuration = stopwatch.Elapsed,
                    AgentsUsed = new List<string> { "fallback_parser" }
                }
            };
        }
    }

    public async Task<ValidationResult> ValidateExtractionAsync(DeviceOrder order, string originalText)
    {
        if (_openAIClient == null)
        {
            return CreateBasicValidation(order);
        }

        try
        {
            var validationPrompt = CreateValidationPrompt(order, originalText);
            var response = await CallOpenAIAsync(validationPrompt, "validation_agent");
            return ParseValidationResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AgenticExtractor] Validation failed, using basic validation");
            return CreateBasicValidation(order);
        }
    }

    public async Task<DeviceOrder> SelfCorrectAsync(DeviceOrder order, ValidationResult validation, string originalText)
    {
        if (_openAIClient == null || !validation.Issues.Any())
        {
            return order;
        }

        try
        {
            var correctionPrompt = CreateCorrectionPrompt(order, validation, originalText);
            var response = await CallOpenAIAsync(correctionPrompt, "correction_agent");
            var correctedOrder = ParseCorrectedOrder(response);

            _logger.LogInformation("[AgenticExtractor] Self-correction completed. Fixed {IssueCount} issues",
                validation.Issues.Count);

            return correctedOrder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AgenticExtractor] Self-correction failed, returning original order");
            return order;
        }
    }

    #region Private Methods

    private Dictionary<string, AgentDefinition> InitializeAgents()
    {
        return new Dictionary<string, AgentDefinition>
        {
            ["document_analyzer"] = new AgentDefinition
            {
                Name = "Document Analysis Agent",
                Role = "Analyze document structure and identify key sections",
                Instructions = "Analyze the medical note structure, identify sections containing device information, patient data, and clinical context. Return structured analysis."
            },
            ["primary_extractor"] = new AgentDefinition
            {
                Name = "Primary Extraction Agent",
                Role = "Extract device order information with medical context",
                Instructions = @"Extract detailed device order information from the medical note. Return a JSON object with this exact structure:

{
  ""device_order"": {
    ""device"": ""[device type like CPAP, Oxygen Tank, etc.]"",
    ""ordering_provider"": ""[doctor name]"",
    ""patient_name"": ""[patient name if mentioned]"",
    ""diagnosis"": ""[medical condition/justification]"",
    ""mask_type"": ""[for CPAP devices]"",
    ""liters"": ""[for oxygen devices]"",
    ""usage"": ""[when/how used]"",
    ""add_ons"": [""list"", ""of"", ""accessories"", ""like"", ""humidifier""]
  }
}

CRITICAL: Always include add_ons array for ANY accessories mentioned (humidifiers, side rails, etc.). If no add-ons mentioned, use empty array []."
            },
            ["medical_validator"] = new AgentDefinition
            {
                Name = "Medical Validation Agent",
                Role = "Validate medical accuracy and completeness",
                Instructions = "Validate the extracted information for medical accuracy, completeness, and consistency with standard medical practices."
            },
            ["confidence_assessor"] = new AgentDefinition
            {
                Name = "Confidence Assessment Agent",
                Role = "Assess extraction confidence and identify uncertainties",
                Instructions = "Evaluate the confidence level of each extracted field and overall extraction quality. Identify areas of uncertainty."
            }
        };
    }

    private async Task<AgentStep> ExecuteAgentAsync(string agentName, string noteText, ExtractionContext context,
        string correlationId, Dictionary<string, object>? previousOutputs = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var agent = _agents[agentName];

        _logger.LogInformation("[AgenticExtractor] Executing agent: {AgentName}, CorrelationId: {CorrelationId}",
            agentName, correlationId);

        if (_openAIClient == null)
        {
            // Fallback behavior for each agent type
            return CreateFallbackAgentStep(agentName, agent, noteText);
        }

        try
        {
            var prompt = CreateAgentPrompt(agent, noteText, context, previousOutputs);
            var response = await CallOpenAIAsync(prompt, agentName);
            var outputs = ParseAgentResponse(response, agentName);

            stopwatch.Stop();

            return new AgentStep
            {
                AgentName = agentName,
                Action = agent.Role,
                Reasoning = outputs.GetValueOrDefault("reasoning", "Agent reasoning not provided").ToString()!,
                Confidence = double.Parse(outputs.GetValueOrDefault("confidence", "0.8").ToString()!),
                Outputs = outputs,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AgenticExtractor] Agent {AgentName} execution failed", agentName);
            stopwatch.Stop();
            return CreateFallbackAgentStep(agentName, agent, noteText, stopwatch.Elapsed);
        }
    }

    private async Task<string> CallOpenAIAsync(string prompt, string agentName)
    {
        var chatCompletionsOptions = new ChatCompletionsOptions()
        {
            DeploymentName = _options.Model,
            Messages = {
                new ChatRequestSystemMessage($"You are a {agentName} specialized in medical device extraction."),
                new ChatRequestUserMessage(prompt)
            },
            MaxTokens = _options.MaxTokens,
            Temperature = (float)Math.Min(_options.Temperature, 0.3) // Lower temperature for more consistent agent behavior
        };

        var response = await _openAIClient!.GetChatCompletionsAsync(chatCompletionsOptions);
        return response.Value.Choices[0].Message.Content;
    }

    private string CreateAgentPrompt(AgentDefinition agent, string noteText, ExtractionContext context,
        Dictionary<string, object>? previousOutputs)
    {
        var prompt = $@"
{agent.Instructions}

Context:
- Document Type: {context.DocumentType}
- Processing Mode: {context.Mode}
- Source File: {context.SourceFile}

{(previousOutputs?.Any() == true ? $"Previous Agent Outputs:\n{JsonSerializer.Serialize(previousOutputs, new JsonSerializerOptions { WriteIndented = true })}\n" : "")}

Medical Note:
{noteText}

Return your analysis as a JSON object with these fields:
- reasoning: Your step-by-step analysis
- confidence: Confidence score (0.0-1.0)
- findings: Key findings relevant to your role
- recommendations: Recommendations for next steps
- tokens_used: Approximate tokens used
{GetAgentSpecificFields(agent.Name)}

Return only valid JSON.";

        return prompt;
    }

    private string GetAgentSpecificFields(string agentName)
    {
        return agentName switch
        {
            "document_analyzer" => @"
- document_structure: Analysis of document sections
- key_sections: Important sections identified
- data_quality: Assessment of data completeness",

            "primary_extractor" => @"
- device_order: Complete device order extraction in JSON format with fields: device, patient_name, dob, diagnosis, ordering_provider, liters, usage, mask_type, qualifier, add_ons
- extraction_certainty: Per-field certainty scores
- missing_fields: Fields that couldn't be extracted",

            "medical_validator" => @"
- validation_issues: List of medical accuracy concerns
- completeness_score: How complete is the extraction
- medical_flags: Any medical red flags",

            "confidence_assessor" => @"
- overall_confidence: Overall extraction confidence
- field_confidences: Per-field confidence scores
- uncertainty_areas: Areas needing attention",

            _ => ""
        };
    }

    private Dictionary<string, object> ParseAgentResponse(string response, string agentName)
    {
        try
        {
            var cleanedResponse = CleanJsonResponse(response);
            var jsonDoc = JsonDocument.Parse(cleanedResponse);
            var result = new Dictionary<string, object>();

            foreach (var property in jsonDoc.RootElement.EnumerateObject())
            {
                result[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? "",
                    JsonValueKind.Number => property.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Array => property.Value.EnumerateArray().Select(x => x.ToString()).ToList(),
                    JsonValueKind.Object => property.Value.ToString(),
                    _ => property.Value.ToString()
                };
            }

            return result;
        }
        catch (JsonException)
        {
            _logger.LogWarning("[AgenticExtractor] Failed to parse agent response as JSON for {AgentName}", agentName);
            return new Dictionary<string, object>
            {
                ["reasoning"] = "Response parsing failed",
                ["confidence"] = 0.3,
                ["findings"] = response,
                ["tokens_used"] = 0
            };
        }
    }

    private AgentStep CreateFallbackAgentStep(string agentName, AgentDefinition agent, string noteText, TimeSpan? duration = null)
    {
        return new AgentStep
        {
            AgentName = agentName,
            Action = agent.Role,
            Reasoning = "OpenAI client not available, using fallback logic",
            Confidence = 0.5,
            Outputs = new Dictionary<string, object>
            {
                ["reasoning"] = "Fallback execution - no AI analysis performed",
                ["confidence"] = 0.5,
                ["findings"] = "Limited analysis available without AI",
                ["tokens_used"] = 0
            },
            Duration = duration ?? TimeSpan.Zero
        };
    }

    private static Dictionary<string, object> CombineOutputs(params Dictionary<string, object>[] outputs)
    {
        var combined = new Dictionary<string, object>();
        foreach (var output in outputs)
        {
            foreach (var kvp in output)
            {
                combined[kvp.Key] = kvp.Value;
            }
        }
        return combined;
    }

    private DeviceOrder ParseDeviceOrderFromOutputs(Dictionary<string, object> outputs)
    {
        try
        {
            // Try to parse the device_order field as JSON
            if (outputs.TryGetValue("device_order", out var deviceOrderObj))
            {
                string deviceOrderJson;
                if (deviceOrderObj is string str)
                {
                    deviceOrderJson = str;
                }
                else
                {
                    deviceOrderJson = deviceOrderObj.ToString() ?? "";
                }

                if (!string.IsNullOrEmpty(deviceOrderJson))
                {
                    deviceOrderJson = CleanJsonResponse(deviceOrderJson);
                    var jsonDoc = JsonDocument.Parse(deviceOrderJson);
                    return ParseDeviceOrderFromJson(jsonDoc.RootElement);
                }
            }

            // Try to parse findings as JSON (fallback)
            if (outputs.TryGetValue("findings", out var findingsObj))
            {
                string findingsJson = findingsObj.ToString() ?? "";
                if (!string.IsNullOrEmpty(findingsJson))
                {
                    try
                    {
                        findingsJson = CleanJsonResponse(findingsJson);
                        var jsonDoc = JsonDocument.Parse(findingsJson);

                        // Check if findings contains device_order
                        if (jsonDoc.RootElement.TryGetProperty("device_order", out var deviceOrderElement))
                        {
                            return ParseDeviceOrderFromFindings(deviceOrderElement);
                        }
                        else
                        {
                            return ParseDeviceOrderFromJson(jsonDoc.RootElement);
                        }
                    }
                    catch
                    {
                        // Continue to fallback
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AgenticExtractor] Failed to parse device order from agent outputs");
        }

        // Enhanced fallback: try to extract individual fields
        return new DeviceOrder
        {
            Device = outputs.GetValueOrDefault("device",
                outputs.GetValueOrDefault("device_type", "Unknown")).ToString() ?? "Unknown",
            OrderingProvider = outputs.GetValueOrDefault("ordering_provider",
                outputs.GetValueOrDefault("provider", "Dr. Unknown")).ToString() ?? "Dr. Unknown",
            PatientName = outputs.GetValueOrDefault("patient_name",
                outputs.GetValueOrDefault("patient", null))?.ToString(),
            Diagnosis = outputs.GetValueOrDefault("diagnosis", null)?.ToString(),
            MaskType = outputs.GetValueOrDefault("mask_type", null)?.ToString(),
            Liters = outputs.GetValueOrDefault("liters", null)?.ToString(),
            Usage = outputs.GetValueOrDefault("usage", null)?.ToString()
        };
    }

    private DeviceOrder ParseDeviceOrderFromFindings(JsonElement element)
    {
        // Extract values first
        string? device = null;
        string? orderingProvider = null;
        string? diagnosis = null;
        string? maskType = null;
        string[]? addOns = null;

        // Handle the specific structure from AI findings
        if (element.TryGetProperty("device_type", out var deviceType))
        {
            device = deviceType.GetString();
        }
        else if (element.TryGetProperty("device", out var deviceProp))
        {
            device = deviceProp.GetString();
        }

        if (element.TryGetProperty("ordering_provider", out var provider))
        {
            orderingProvider = provider.GetString();
        }

        if (element.TryGetProperty("diagnosis_or_medical_justification", out var diagnosisProp))
        {
            diagnosis = diagnosisProp.GetString();
        }

        // Handle medical specifications nested object
        if (element.TryGetProperty("medical_specifications", out var specs))
        {
            if (specs.TryGetProperty("mask_type", out var maskTypeProp))
            {
                maskType = maskTypeProp.GetString();
            }

            if (specs.TryGetProperty("additional_features", out var features))
            {
                var featuresList = new List<string>();
                if (features.ValueKind == JsonValueKind.String)
                {
                    featuresList.Add(features.GetString() ?? "");
                }
                else if (features.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in features.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                            featuresList.Add(item.GetString() ?? "");
                    }
                }
                if (featuresList.Count > 0)
                    addOns = featuresList.ToArray();
            }
        }

        return new DeviceOrder
        {
            Device = device,
            OrderingProvider = orderingProvider,
            Diagnosis = diagnosis,
            MaskType = maskType,
            AddOns = addOns
        };
    }

    private DeviceOrder ParseDeviceOrderFromJson(JsonElement element)
    {
        var addOns = new List<string>();
        if (element.TryGetProperty("add_ons", out var addOnsElement) && addOnsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in addOnsElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && item.GetString() is string addOn)
                    addOns.Add(addOn);
            }
        }

        return new DeviceOrder
        {
            Device = GetStringProperty(element, "device") ?? "Unknown",
            PatientName = GetStringProperty(element, "patient_name"),
            Dob = GetStringProperty(element, "dob"),
            Diagnosis = GetStringProperty(element, "diagnosis"),
            OrderingProvider = GetStringProperty(element, "ordering_provider") ?? "Dr. Unknown",
            Liters = GetStringProperty(element, "liters"),
            Usage = GetStringProperty(element, "usage"),
            MaskType = GetStringProperty(element, "mask_type"),
            Qualifier = GetStringProperty(element, "qualifier"),
            AddOns = addOns.Count > 0 ? addOns.ToArray() : null
        };
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString()?.Trim();
        }
        return null;
    }

    private string CreateValidationPrompt(DeviceOrder order, string originalText)
    {
        return $@"
Validate this extracted device order against the original medical note:

Extracted Order:
{JsonSerializer.Serialize(order, new JsonSerializerOptions { WriteIndented = true })}

Original Note:
{originalText}

Analyze for:
1. Medical accuracy and consistency
2. Completeness of critical fields
3. Logical consistency between fields
4. Compliance with medical standards

Return JSON with:
- is_valid: boolean
- validation_score: 0.0-1.0
- issues: array of validation issues
- field_confidences: per-field confidence scores
- suggestions: improvement recommendations

Return only valid JSON.";
    }

    private ValidationResult ParseValidationResponse(string response)
    {
        try
        {
            var cleanedResponse = CleanJsonResponse(response);
            var jsonDoc = JsonDocument.Parse(cleanedResponse);
            var root = jsonDoc.RootElement;

            var issues = new List<ValidationIssue>();
            if (root.TryGetProperty("issues", out var issuesElement) && issuesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var issueElement in issuesElement.EnumerateArray())
                {
                    issues.Add(new ValidationIssue
                    {
                        Field = GetStringProperty(issueElement, "field") ?? "",
                        Issue = GetStringProperty(issueElement, "issue") ?? "",
                        Severity = Enum.TryParse<ValidationSeverity>(GetStringProperty(issueElement, "severity"), true, out var severity) ? severity : ValidationSeverity.Warning,
                        SuggestedFix = GetStringProperty(issueElement, "suggested_fix")
                    });
                }
            }

            return new ValidationResult
            {
                IsValid = root.TryGetProperty("is_valid", out var validProp) && validProp.GetBoolean(),
                ValidationScore = root.TryGetProperty("validation_score", out var scoreProp) ? scoreProp.GetDouble() : 0.5,
                Issues = issues
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AgenticExtractor] Failed to parse validation response");
            return CreateBasicValidation(null);
        }
    }

    private string CreateCorrectionPrompt(DeviceOrder order, ValidationResult validation, string originalText)
    {
        return $@"
Fix the following device order based on validation issues:

Current Order:
{JsonSerializer.Serialize(order, new JsonSerializerOptions { WriteIndented = true })}

Validation Issues:
{JsonSerializer.Serialize(validation.Issues, new JsonSerializerOptions { WriteIndented = true })}

Original Note:
{originalText}

Return the corrected device order as JSON with the same structure as the input order.
Focus on fixing the identified issues while maintaining accuracy to the original note.

Return only valid JSON.";
    }

    private DeviceOrder ParseCorrectedOrder(string response)
    {
        try
        {
            var cleanedResponse = CleanJsonResponse(response);
            var jsonDoc = JsonDocument.Parse(cleanedResponse);
            return ParseDeviceOrderFromJson(jsonDoc.RootElement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AgenticExtractor] Failed to parse corrected order");
            return new DeviceOrder();
        }
    }

    private static ValidationResult CreateBasicValidation(DeviceOrder? order)
    {
        var issues = new List<ValidationIssue>();

        if (order != null)
        {
            if (string.IsNullOrEmpty(order.Device) || order.Device == "Unknown")
                issues.Add(new ValidationIssue { Field = "Device", Issue = "Device type not identified", Severity = ValidationSeverity.Error });

            if (string.IsNullOrEmpty(order.PatientName))
                issues.Add(new ValidationIssue { Field = "PatientName", Issue = "Patient name missing", Severity = ValidationSeverity.Warning });
        }

        return new ValidationResult
        {
            IsValid = issues.Count(i => i.Severity == ValidationSeverity.Error) == 0,
            ValidationScore = Math.Max(0.1, 1.0 - (issues.Count * 0.2)),
            Issues = issues
        };
    }

    private static string CleanJsonResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return response;

        response = response.Trim();
        if (response.StartsWith("```json"))
            response = response.Substring(7);
        else if (response.StartsWith("```"))
            response = response.Substring(3);

        if (response.EndsWith("```"))
            response = response.Substring(0, response.Length - 3);

        return response.Trim();
    }

    #endregion
}

/// <summary>
/// Agent definition for specialized AI agents
/// </summary>
public record AgentDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string Instructions { get; init; } = string.Empty;
}