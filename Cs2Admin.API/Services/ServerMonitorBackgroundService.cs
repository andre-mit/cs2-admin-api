using Cs2Admin.API.Infrastructure.Repositories;
using Cs2Admin.API.Services.Interfaces;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cs2Admin.API.Services;

public class ServerMonitorBackgroundService(
    IServiceProvider _serviceProvider,
    IServerEventService _eventService,
    IDockerClient _dockerClient,
    ILogger<ServerMonitorBackgroundService> _logger) : BackgroundService
{
    // Store the last known status for each server to only broadcast changes
    private readonly Dictionary<int, string> _lastKnownStatus = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ServerMonitorBackgroundService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckServersStatusAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while monitoring servers.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        _logger.LogInformation("ServerMonitorBackgroundService is stopping.");
    }

    private async Task CheckServersStatusAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var serverRepository = scope.ServiceProvider.GetRequiredService<IServerRepository>();
        var rconService = scope.ServiceProvider.GetRequiredService<IRconService>();

        var servers = await serverRepository.GetAllAsync();

        foreach (var server in servers)
        {
            string currentStatus = "offline";

            if (!server.IsDynamic || string.IsNullOrEmpty(server.ContainerId))
            {
                // Static servers
                try
                {
                    var rconTask = rconService.SendCommandAsync(server.IpString, server.Port, server.RconPassword ?? "", "status");
                    if (await Task.WhenAny(rconTask, Task.Delay(1500, stoppingToken)) == rconTask)
                    {
                        await rconTask; // throw if failed
                        currentStatus = "online";
                    }
                }
                catch
                {
                    currentStatus = "offline";
                }
            }
            else
            {
                // Dynamic servers using Docker
                try
                {
                    var containerInfo = await _dockerClient.Containers.InspectContainerAsync(server.ContainerId, stoppingToken);
                    if (containerInfo.State.Running)
                    {
                        // Container is running, check RCON
                        var rconTask = rconService.SendCommandAsync(server.IpString, server.Port, server.RconPassword ?? "", "status");
                        if (await Task.WhenAny(rconTask, Task.Delay(1500, stoppingToken)) == rconTask)
                        {
                            try
                            {
                                await rconTask;
                                currentStatus = "online";
                            }
                            catch
                            {
                                // If RCON fails but container is running, it's starting (or crashed)
                                currentStatus = "starting";
                            }
                        }
                        else
                        {
                            currentStatus = "starting";
                        }
                    }
                    else if (containerInfo.State.Restarting)
                    {
                        currentStatus = "restarting";
                    }
                    else
                    {
                        currentStatus = "offline";
                    }
                }
                catch
                {
                    currentStatus = "offline";
                }
            }

            // Check if status changed
            if (!_lastKnownStatus.TryGetValue(server.Id, out var lastStatus) || lastStatus != currentStatus)
            {
                _lastKnownStatus[server.Id] = currentStatus;
                
                await _eventService.BroadcastEventAsync("status_change", new 
                { 
                    serverId = server.Id, 
                    status = currentStatus,
                    isDynamic = server.IsDynamic
                });
            }
        }
    }
}
