namespace Cs2Admin.API.Services.Interfaces;

public interface IRconService
{
    Task<string> SendCommandAsync(string ip, int port, string password, string command);
}
