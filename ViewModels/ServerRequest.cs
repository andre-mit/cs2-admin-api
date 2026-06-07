namespace Cs2Admin.API.ViewModels;

public class ServerRequest
{
    public string Name { get; set; } = "CS2 Server";
    public string Password { get; set; } = string.Empty;
    public string? RconPassword { get; set; }
    public byte MaxPlayers { get; set; }
}