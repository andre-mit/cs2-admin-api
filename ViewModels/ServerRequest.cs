namespace Cs2Admin.API.ViewModels;

public class ServerRequest
{
    public string Name { get; set; } = "CS2 Server";
    public string Password { get; set; } = string.Empty;
    public string? RconPassword { get; set; }
    public byte MaxPlayers { get; set; }
    
    public List<PluginSelectionItem> PluginSelections { get; set; } = [];
    public Dictionary<string, string> ServerVariables { get; set; } = new();
}

public class PluginSelectionItem
{
    /// <summary>
    /// ID of the plugin to install
    /// </summary>
    public int PluginId { get; set; }
    
    /// <summary>
    /// JSON object keyed by config "key", with user overrides.
    /// Null = use all defaults from plugin.
    /// </summary>
    public string? ConfigOverridesJson { get; set; }
}