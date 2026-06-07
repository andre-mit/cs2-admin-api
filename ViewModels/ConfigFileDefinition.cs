using System.Text.Json;

namespace Cs2Admin.API.ViewModels
{
    public class ConfigFileDefinition
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string Format { get; set; } = "json";  // "json" | "cfg"
        public JsonElement? DefaultContent { get; set; }
    }
}
