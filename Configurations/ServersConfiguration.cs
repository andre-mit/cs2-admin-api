namespace Cs2Admin.API.Configurations;

public class ServersConfiguration
{
    public string GameBaseDir { get; set; } = "/var/lib/cs2-base";
    public string ServersBaseDir { get; set; } = "/var/lib/cs2-instances/";
    public NetworkConfiguration Network { get; set; } = new NetworkConfiguration();

    public Dictionary<string, string> DefaultEnvVariables { get; set; } = [];
    
    public string UpperDir => Path.Combine(ServersBaseDir, "upper");
    public string WorkDir => Path.Combine(ServersBaseDir, "work");

    public class NetworkConfiguration
    {
        public string Name { get; set; } = "default";
        public string NetworkMode { get; set; } = string.Empty;
        public bool External { get; set; } = true;
        public List<string> DnsServers { get; set; } = [];
    }
}