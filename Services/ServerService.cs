using Cs2Admin.API.Configurations;
using Cs2Admin.API.Exceptions;
using Cs2Admin.API.Services.Interfaces;
using Cs2Admin.API.ViewModels;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Options;

namespace Cs2Admin.API.Services;

public class ServerService(
    ISteamTokenService steamTokenService,
    IPortAllocatorService portAllocatorService,
    IDockerClient dockerClient,
    IOptions<ServersConfiguration> serversConfigurationOptions,
    IConfiguration configuration,
    ILogger<ServerService> logger)
    : IServerService
{
    private readonly ServersConfiguration _serversConfiguration = serversConfigurationOptions.Value;

    public async Task<ServerResult> CreateServerAsync(ServerRequest serverRequest,
        CancellationToken cancellationToken = default)
    {
        var token = await steamTokenService.GetAvailableTokenAsync(cancellationToken);
        if (token == null)
        {
            logger.LogError("No available Steam tokens to create a new server instance.");
            throw new NoAvailableServersException("No available Steam tokens to create a new server instance.");
        }

        var parameters = new ContainersListParameters
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                { "name", new Dictionary<string, bool> { { token.Memo, true } } }
            }
        };

        var containers = await dockerClient.Containers.ListContainersAsync(parameters, cancellationToken);

        if (containers.Any())
        {
            throw new Exception($"Server {token.Memo} already exists.");
        }

        Directory.CreateDirectory(_serversConfiguration.UpperDir);
        Directory.CreateDirectory(_serversConfiguration.WorkDir);

        var volumeName = $"cs2-vol-instance-{token.Memo}";
        await dockerClient.Volumes.CreateAsync(new VolumesCreateParameters
        {
            Name = volumeName,
            Driver = "local",
            DriverOpts = new Dictionary<string, string>
            {
                { "type", "overlay" },
                { "device", "overlay" },
                {
                    "o",
                    $"lowerdir={_serversConfiguration.GameBaseDir},upperdir={_serversConfiguration.UpperDir},workdir={_serversConfiguration.WorkDir}"
                }
            }
        }, cancellationToken);

        var ports = await portAllocatorService.AllocateAvailablePortPairAsync(cancellationToken);

        var mergedEnvironment = _serversConfiguration.DefaultEnvVariables;

        mergedEnvironment["SRCDS_TOKEN"] = token.Token;
        mergedEnvironment["CS2_SERVERNAME"] = serverRequest.Name;
        mergedEnvironment["CS2_PW"] = serverRequest.Password;
        mergedEnvironment["CS2_MAXPLAYERS"] = serverRequest.MaxPlayers.ToString();
        mergedEnvironment["CS2_ADDITIONAL_ARGS"] = "-tickrate 128";

        mergedEnvironment["CS2_PORT"] = "27015";
        mergedEnvironment["TV_PORT"] = "27020";
        mergedEnvironment["CS2_RCONPW"] = serverRequest.RconPassword ?? Guid.NewGuid().ToString("N")[..16];

        var envList = mergedEnvironment.Select(kvp => $"{kvp.Key}={kvp.Value}").ToList();
        var containerId = $"cs2-server-{token.Memo}";
        var containerResponse = await dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = "joedwards32/cs2",
            Name = containerId,
            Env = envList,
            HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    { "27015/udp", new List<PortBinding> { new() { HostPort = ports.GamePort.ToString() } } },
                    { "27015/tcp", new List<PortBinding> { new() { HostPort = ports.GamePort.ToString() } } },
                    { "27020/udp", new List<PortBinding> { new() { HostPort = ports.TvPort.ToString() } } }
                },
                Binds = new List<string>
                {
                    $"{volumeName}:/home/steam/cs2-dedicated"
                },
                DNS = _serversConfiguration.Network.DnsServers,
                NetworkMode = _serversConfiguration.Network.NetworkMode,
            },
            Tty = true, OpenStdin = true
        }, cancellationToken);
        await dockerClient.Networks.ConnectNetworkAsync(_serversConfiguration.Network.Name, new NetworkConnectParameters
        {
            Container = containerResponse.ID
        }, cancellationToken);
        await dockerClient.Containers.StartContainerAsync(containerResponse.ID, new ContainerStartParameters(),
            cancellationToken);

        var serverHost = configuration["ServerHost"] ?? "localhost";
        var connectUrl = $"{serverHost}:{ports.GamePort}";

        return new ServerResult
        {
            ServerId = containerId,
            GamePort = ports.GamePort,
            RconPort = ports.TvPort,
            ConnectUrl = connectUrl
        };
    }

    public Task StartServerAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        return dockerClient.Containers.StartContainerAsync(instanceId, null, cancellationToken);
    }

    public Task StopServerAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        return dockerClient.Containers.StopContainerAsync(instanceId, new ContainerStopParameters(), cancellationToken);
    }

    public Task RestartServerAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        return dockerClient.Containers.RestartContainerAsync(instanceId, new ContainerRestartParameters(),
            cancellationToken);
    }

    public async Task DeleteServerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting dynamic server container: {ContainerId}", containerId);

        try
        {
            await dockerClient.Containers.StopContainerAsync(containerId, new ContainerStopParameters
            {
                WaitBeforeKillSeconds = 10
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Container {ContainerId} may already be stopped.", containerId);
        }

        await dockerClient.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters
        {
            Force = true,
            RemoveVolumes = true
        }, cancellationToken);

        logger.LogInformation("Successfully deleted container: {ContainerId}", containerId);
    }
}