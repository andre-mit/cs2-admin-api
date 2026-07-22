namespace Cs2Admin.API.Models;

public class SteamServerToken
{
    public int Id { get; set; }
    public required string  Memo { get; set; }
    public required string Token { get; set; }
    public bool IsAvailable { get; set; } = true;
}