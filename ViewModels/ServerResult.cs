namespace Cs2Admin.API.ViewModels;

public class ServerResult
{
    public string ServerId { get; set; } = null!;
    
    public int GamePort { get; set; }
    public int RconPort { get; set; }
    
    /// <summary>
    /// Connection URL in the format "host:port" for clients to connect.
    /// </summary>
    public string ConnectUrl { get; set; } = null!;
}