namespace Cs2Admin.API.Models
{
    public class ServerPlugin
    {
        public int Id { get; set; }
        public int ServerId { get; set; }
        public Server Server { get; set; } = null!;
        public int GamePluginId { get; set; }
        public GamePlugin GamePlugin { get; set; } = null!;
        
        /// <summary>
        /// JSON object keyed by config "key". Each value is the override for that config file.
        /// Ex: { "server_cfg": { "matchzy_knife_enabled_default": "false" } }
        /// </summary>
        public string? ConfigOverridesJson { get; set; }
    }
}
