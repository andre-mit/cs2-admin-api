using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Cs2Admin.API.Models
{
    public class ServerPreset
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? CustomCfg { get; set; }

        public string? CustomCfgName { get; set; }

        // JSON string containing Dictionary<string, string> of CVARs
        public string ServerVariablesJson { get; set; } = "{}";

        // JSON string containing int[] of Plugin Ids
        public string PluginIdsJson { get; set; } = "[]";

        [NotMapped]
        public Dictionary<string, string> ServerVariables
        {
            get => string.IsNullOrEmpty(ServerVariablesJson) ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(ServerVariablesJson) ?? new Dictionary<string, string>();
            set => ServerVariablesJson = JsonSerializer.Serialize(value);
        }

        [NotMapped]
        public List<int> PluginIds
        {
            get => string.IsNullOrEmpty(PluginIdsJson) ? new List<int>() : JsonSerializer.Deserialize<List<int>>(PluginIdsJson) ?? new List<int>();
            set => PluginIdsJson = JsonSerializer.Serialize(value);
        }
    }
}
