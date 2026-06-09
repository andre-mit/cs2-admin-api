namespace Cs2Admin.API.Configurations;

public class ServersConfiguration
{
    public string GameBaseDir { get; set; } = "/var/lib/cs2-base";
    public string ServersBaseDir { get; set; } = "/var/lib/cs2-instances";
    public string PluginsBaseDir { get; set; } = "/var/lib/cs2-plugins";
    
    public NetworkConfiguration Network { get; set; } = new();

    public Dictionary<string, string> DefaultEnvVariables { get; set; } = [];

    public string UpperDir (string serverName) => Path.Combine(ServersBaseDir, serverName, "upper");
    public string WorkDir (string serverName) => Path.Combine(ServersBaseDir, serverName, "work");

    public class NetworkConfiguration
    {
        public string Name { get; set; } = "bridge";
        public string NetworkMode { get; set; } = string.Empty;
        public string AdditionalNetworks { get; set; } = string.Empty;
        public bool External { get; set; } = true;
        public List<string> DnsServers { get; set; } = [];
    }
}