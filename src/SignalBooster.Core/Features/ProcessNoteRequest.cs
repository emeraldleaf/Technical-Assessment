namespace SignalBooster.Core.Features;

public sealed record ProcessNoteRequest(
    string FilePath,
    bool SaveOutput = false,
    string? OutputFileName = null
);

public sealed record ProcessNoteResponse(
    string DeviceType,
    string OrderId,
    string Status,
    string ProcessedFilePath,
    string? OutputFilePath = null,
    DateTime ProcessedAt = default
)
{
    public ProcessNoteResponse() : this(string.Empty, string.Empty, string.Empty, string.Empty)
    {
        ProcessedAt = DateTime.UtcNow;
    }
}