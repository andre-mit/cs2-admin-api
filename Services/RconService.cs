using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using CoreRCON;
using Microsoft.Extensions.Logging;


namespace Cs2Admin.API.Services
{
    public interface IRconService
    {
        Task<string> SendCommandAsync(string ip, int port, string password, string command);
    }

    public class RconService : IRconService
    {
        private readonly ILogger<RconService> _logger;

        public RconService(ILogger<RconService> logger)
        {
            _logger = logger;
        }

        public async Task<string> SendCommandAsync(string ip, int port, string password, string command)
        {
            _logger.LogInformation("Sending RCON command '{Command}' to {Ip}:{Port}", command, ip, port);

            RCON? rcon = null;
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(ip);
                var ipAddress = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses[0];
                var endpoint = new IPEndPoint(ipAddress, port);
                rcon = new RCON(endpoint, password);
                
                await rcon.ConnectAsync();
                var result = await rcon.SendCommandAsync(command);
                
                _logger.LogInformation("RCON response: {Result}", result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send RCON command to {Ip}:{Port}", ip, port);
                throw new ApplicationException($"RCON connection failed: {ex.Message}");
            }
            finally
            {
                if (rcon != null)
                {
                    try
                    {
                        rcon.Dispose();
                    }
                    catch (Exception disposeEx)
                    {
                        _logger.LogDebug(disposeEx, "Error disposing RCON client for {Ip}:{Port}", ip, port);
                    }
                }
            }
        }
    }
}
