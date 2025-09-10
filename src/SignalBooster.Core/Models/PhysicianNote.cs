using System.Text.Json.Serialization;

namespace SignalBooster.Core.Models;

public record PhysicianNote(
    [property: JsonPropertyName("patient_name")] string PatientName,
    [property: JsonPropertyName("dob")] string DateOfBirth,
    [property: JsonPropertyName("diagnosis")] string Diagnosis,
    [property: JsonPropertyName("prescription")] string Prescription,
    [property: JsonPropertyName("usage")] string Usage,
    [property: JsonPropertyName("ordering_provider")] string OrderingProvider,
    string RawText
)
{
    [JsonPropertyName("patient_id")]
    public string PatientId { get; init; } = string.Empty;
    
    [JsonPropertyName("physician_name")]
    public string PhysicianName => OrderingProvider;
    
    [JsonPropertyName("note_date")]
    public DateTime NoteDate { get; init; } = DateTime.UtcNow;
}