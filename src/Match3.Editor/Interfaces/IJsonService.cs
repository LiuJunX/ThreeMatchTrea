namespace Match3.Editor.Interfaces;

/// <summary>
/// Abstraction for JSON serialization
/// </summary>
public interface IJsonService
{
    string Serialize<T>(T obj);
    T Deserialize<T>(string json);
}
