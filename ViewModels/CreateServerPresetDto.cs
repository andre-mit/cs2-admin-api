using System.ComponentModel.DataAnnotations;

namespace Cs2Admin.API.ViewModels
{
    public class CreateServerPresetDto
    {
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
        
        public string? Description { get; set; }
        
        public string? CustomCfg { get; set; }
        
        public string? CustomCfgName { get; set; }
        
        public Dictionary<string, string> ServerVariables { get; set; } = new();
        
        public List<int> PluginIds { get; set; } = new();
    }
}
