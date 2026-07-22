using System.Net.NetworkInformation;
using Cs2Admin.API.Services.Interfaces;
using Cs2Admin.API.ViewModels;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace Cs2Admin.API.Services;

public class PortAllocatorService(IDockerClient dockerClient, ILogger<PortAllocatorService> logger)
    : IPortAllocatorService
{
    private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);

    private const int StartPort = 27015;
    private const int EndPort = 27155;

    public async Task<AllocatedPortResult> AllocateAvailablePortPairAsync(CancellationToken cancellationToken)
    {
        await Semaphore.WaitAsync(cancellationToken);
        try
        {
            var occupiedDockerPorts = await GetOccupiedDockerPortsAsync(cancellationToken);
            var activeSystemPorts = GetActiveSystemPorts();

            for (var port = StartPort; port <= EndPort - 1; port += 2)
            {
                var rconPort = port + 1;

                var isGamePortFree = !occupiedDockerPorts.Contains(port) && !activeSystemPorts.Contains(port);
                var isRconPortFree = !occupiedDockerPorts.Contains(rconPort) && !activeSystemPorts.Contains(rconPort);

                if (!isGamePortFree || !isRconPortFree) continue;
                logger.LogInformation("Allocated port pair: GamePort={GamePort}, TvPort={TvPort}", port, rconPort);
                return new AllocatedPortResult
                {
                    GamePort = port,
                    TvPort = rconPort
                };
            }

            logger.LogError("Port exhaustion! No free port pairs available in the configured range.");
            throw new InvalidOperationException(
                "Port exhaustion! No free port pairs available in the configured range.");
        }
        finally
        {
            logger.LogDebug("Releasing port allocation semaphore.");
            Semaphore.Release();
        }
    }

    private async Task<HashSet<int>> GetOccupiedDockerPortsAsync(CancellationToken cancellationToken)
    {
        var occupiedPorts = new HashSet<int>();
        var containers =
            await dockerClient.Containers.ListContainersAsync(new ContainersListParameters { All = true },
                cancellationToken);

        foreach (var container in containers)
        {
            if (container.Ports is null) continue;

            foreach (var portMapping in container.Ports)
            {
                if (portMapping.PublicPort != 0)
                {
                    occupiedPorts.Add(portMapping.PublicPort);
                }
            }
        }

        return occupiedPorts;
    }

    private static HashSet<int> GetActiveSystemPorts()
    {
        var activePorts = new HashSet<int>();
        var ipProperties = IPGlobalProperties.GetIPGlobalProperties();

        var tcpListeners = ipProperties.GetActiveTcpListeners();
        var udpListeners = ipProperties.GetActiveUdpListeners();

        foreach (var endpoint in tcpListeners)
        {
            activePorts.Add(endpoint.Port);
        }

        foreach (var endpoint in udpListeners)
        {
            activePorts.Add(endpoint.Port);
        }

        return activePorts;
    }
}