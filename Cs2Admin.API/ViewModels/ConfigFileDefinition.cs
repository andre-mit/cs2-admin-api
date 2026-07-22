using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cs2Admin.API.ViewModels
{
    public class ConfigFileDefinition
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("relativePath")]
        public string RelativePath { get; set; } = string.Empty;

        [JsonPropertyName("format")]
        public string Format { get; set; } = "json";  // "json" | "cfg"

        [JsonPropertyName("defaultContent")]
        public JsonElement? DefaultContent { get; set; }
    }
}
