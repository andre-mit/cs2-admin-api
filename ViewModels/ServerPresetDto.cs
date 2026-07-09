using System.ComponentModel.DataAnnotations;

namespace Cs2Admin.API.ViewModels
{
    public class ServerPresetDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Dictionary<string, string> ServerVariables { get; set; } = new();
        public List<int> PluginIds { get; set; } = new();
    }
}
