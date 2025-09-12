namespace SignalBooster.Services;

public interface IFileReader
{
    Task<string> ReadTextAsync(string filePath);
}