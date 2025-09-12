namespace SignalBooster.Models;

/// <summary>
/// Domain model representing a DME (Durable Medical Equipment) device order.
/// 
/// Design Pattern: Immutable Data Transfer Object (DTO)
/// - Uses C# 9+ record syntax for value-based equality and immutability
/// - Properties use 'init' accessors to prevent modification after creation
/// - Follows Domain-Driven Design principles by representing business concepts
/// 
/// Architecture Notes:
/// - Separates data structure from business logic (Single Responsibility Principle)
/// - Nullable properties allow flexible data extraction from various note formats
/// - Required fields (Device, OrderingProvider) have non-nullable defaults
/// </summary>
public record DeviceOrder
{
    /// <summary>Primary DME device type (e.g., "CPAP", "Oxygen Tank", "Hospital Bed")</summary>
    public string Device { get; init; } = string.Empty;
    
    /// <summary>Flow rate for respiratory devices (e.g., "2 L" for oxygen)</summary>
    public string? Liters { get; init; }
    
    /// <summary>Usage instructions or timing (e.g., "sleep and exertion")</summary>
    public string? Usage { get; init; }
    
    /// <summary>Medical diagnosis justifying the device order</summary>
    public string? Diagnosis { get; init; }
    
    /// <summary>Prescribing physician name (required field)</summary>
    public string OrderingProvider { get; init; } = string.Empty;
    
    /// <summary>Patient full name</summary>
    public string? PatientName { get; init; }
    
    /// <summary>Patient date of birth</summary>
    public string? Dob { get; init; }
    
    /// <summary>CPAP/BiPAP mask type (e.g., "full face", "nasal")</summary>
    public string? MaskType { get; init; }
    
    /// <summary>Additional accessories or modifications for the device</summary>
    public string[]? AddOns { get; init; }
    
    /// <summary>Medical qualifier or severity indicator (e.g., "AHI > 20")</summary>
    public string? Qualifier { get; init; }
}