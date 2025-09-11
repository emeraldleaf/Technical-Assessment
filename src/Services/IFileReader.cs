namespace SignalBooster.Mvp.Services;

public interface IFileReader
{
    Task<string> ReadTextAsync(string filePath);
}