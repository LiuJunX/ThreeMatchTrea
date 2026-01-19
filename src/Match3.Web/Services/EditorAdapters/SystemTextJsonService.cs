using System.Text.Json;
using Match3.Editor.Interfaces;

namespace Match3.Web.Services.EditorAdapters
{
    public class SystemTextJsonService : IJsonService
    {
        private readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            IncludeFields = true
        };

        public string Serialize<T>(T obj)
        {
            return JsonSerializer.Serialize(obj, _options);
        }

        public T Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, _options)!;
        }
    }
}
