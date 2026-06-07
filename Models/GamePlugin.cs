using System;

namespace Cs2Admin.API.Models
{
    public class GamePlugin
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        
        /// <summary>
        /// JSON array of ConfigFileDefinition. Each entry defines a config file
        /// with key, label, relativePath, format ("json"|"cfg"), defaultContent.
        /// </summary>
        public string ConfigFilesJson { get; set; } = "[]";
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
