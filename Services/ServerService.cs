using System.Text;
using System.Text.Json;
using Cs2Admin.API.Configurations;
using Cs2Admin.API.Data;
using Cs2Admin.API.Exceptions;
using Cs2Admin.API.Services.Interfaces;
using Cs2Admin.API.ViewModels;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Options;

namespace Cs2Admin.API.Services;

public class ServerService(
    ApplicationDbContext dbContext,
    ISteamTokenService steamTokenService,
    IPortAllocatorService portAllocatorService,
    IDockerClient dockerClient,
    IOptions<ServersConfiguration> serversConfigurationOptions,
    IConfiguration configuration,
    ILogger<ServerService> logger)
    : IServerService
{
    private readonly ServersConfiguration _serversConfiguration = serversConfigurationOptions.Value;

    public async Task<ServerResult> CreateServerAsync(ServerRequest serverRequest, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting dynamic server creation...");
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
            logger.LogWarning("Server {Memo} already exists.", token.Memo);
            throw new Exception($"Server {token.Memo} already exists.");
        }

        logger.LogInformation("Creating directories for instance: {Memo}", token.Memo);
        var instanceUpperPath = _serversConfiguration.UpperDir(token.Memo);
        var instanceWorkPath = _serversConfiguration.WorkDir(token.Memo);

        Directory.CreateDirectory(instanceUpperPath);
        Directory.CreateDirectory(instanceWorkPath);
        
        logger.LogInformation("Directories  created: [upper] {DirectoryUpper}, [work] {DirectoryWork}", instanceUpperPath, instanceWorkPath);

        if (serverRequest.PluginSelections is { Count: > 0 })
        {
            var serializerOptions = new JsonSerializerOptions { WriteIndented = true };

            foreach (var selection in serverRequest.PluginSelections)
            {
                var plugin = await dbContext.GamePlugins.FindAsync(new object[] { selection.PluginId }, cancellationToken);
                if (plugin == null) continue;

                var templatePluginPath = Path.Combine(_serversConfiguration.PluginsBaseDir, plugin.Name);
                var destinationPluginPath = Path.Combine(
                    instanceUpperPath,
                    "game/csgo");

                if (Directory.Exists(templatePluginPath))
                {
                    CopyDirectory(templatePluginPath, destinationPluginPath);
                    logger.LogInformation("Plugin {PluginName} dynamically injected for instance {InstanceMemo}",
                        plugin.Name, token.Memo);
                }

                List<ConfigFileDefinition> configFiles = new();
                
                if (!string.IsNullOrWhiteSpace(plugin.ConfigFilesJson))
                {
                    try { configFiles = JsonSerializer.Deserialize<List<ConfigFileDefinition>>(plugin.ConfigFilesJson) ?? new(); } catch { }
                }

                if (!string.IsNullOrWhiteSpace(selection.ConfigOverridesJson))
                {
                    try
                    {
                        var editedDefinitions = JsonSerializer.Deserialize<List<ConfigFileDefinition>>(selection.ConfigOverridesJson);
                        if (editedDefinitions != null && editedDefinitions.Count > 0)
                        {
                            configFiles = editedDefinitions;
                        }
                    }
                    catch
                    {
                        try
                        {
                            var overrides = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(selection.ConfigOverridesJson) ?? new();
                            foreach (var file in configFiles)
                            {
                                if (overrides.TryGetValue(file.Key, out var overrideElement))
                                {
                                    file.DefaultContent = MergeConfigs(file.DefaultContent, overrideElement);
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            throw new ArgumentException($"Invalid JSON format in overrides for plugin {plugin.Name}. Ensure it is either a valid JSON array matching templates or a JSON object. Raw input: {selection.ConfigOverridesJson}", ex);
                        }
                    }
                }

                foreach (var configFile in configFiles)
                {
                    var relativePath = configFile.RelativePath?.TrimStart('/', '\\') ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(relativePath))
                    {
                        logger.LogWarning("Skipping config file {Key} because RelativePath is empty.", configFile.Key);
                        continue;
                    }

                    var merged = configFile.DefaultContent ?? JsonSerializer.SerializeToElement(new { });
                    var fullPath = Path.Combine(instanceUpperPath, relativePath);
                    var dir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    if (configFile.Format.ToLower() == "cfg")
                    {
                        var cfgStr = WriteCfgFormat(merged);
                        await File.WriteAllTextAsync(fullPath, cfgStr, Encoding.UTF8, cancellationToken);
                    }
                    else
                    {
                        await File.WriteAllTextAsync(fullPath, JsonSerializer.Serialize(merged, serializerOptions), cancellationToken);
                    }
                }
            }
        }

        var volumeName = $"cs2-vol-instance-{token.Memo}";
        logger.LogInformation("Creating Docker volume {VolumeName}", volumeName);
        var volumeOptions = $"lowerdir={_serversConfiguration.GameBaseDir},upperdir={instanceUpperPath},workdir={instanceWorkPath}";
        logger.LogInformation("Volume driver options: {VolumeOptions}", volumeOptions);
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
                    volumeOptions
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

        mergedEnvironment["CS2_PORT"] = ports.GamePort.ToString();
        mergedEnvironment["TV_PORT"] = ports.TvPort.ToString();
        mergedEnvironment["CS2_RCONPW"] = serverRequest.RconPassword ?? Guid.NewGuid().ToString("N")[..16];

        var envList = mergedEnvironment.Select(kvp => $"{kvp.Key}={kvp.Value}").ToList();
        var containerId = $"cs2-server-{token.Memo}";
        logger.LogInformation("Creating Docker container {ContainerId}", containerId);
        CreateContainerResponse containerResponse;
        try
        {
            containerResponse = await dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Image = "joedwards32/cs2",
                Name = containerId,
                Env = envList,
                HostConfig = new HostConfig
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        { $"{ports.GamePort}/udp", new List<PortBinding> { new() { HostPort = ports.GamePort.ToString() } } },
                        { $"{ports.GamePort}/tcp", new List<PortBinding> { new() { HostPort = ports.GamePort.ToString() } } },
                        { $"{ports.TvPort}/udp", new List<PortBinding> { new() { HostPort = ports.TvPort.ToString() } } }
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
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            logger.LogInformation("Image joedwards32/cs2 not found. Pulling from Docker Hub...");
            await dockerClient.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = "joedwards32/cs2", Tag = "latest" },
                null,
                new Progress<JSONMessage>(msg => { }),
                cancellationToken);
            
            logger.LogInformation("Image pulled successfully. Retrying container creation...");
            containerResponse = await dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Image = "joedwards32/cs2",
                Name = containerId,
                Env = envList,
                HostConfig = new HostConfig
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        { $"{ports.GamePort}/udp", new List<PortBinding> { new() { HostPort = ports.GamePort.ToString() } } },
                        { $"{ports.GamePort}/tcp", new List<PortBinding> { new() { HostPort = ports.GamePort.ToString() } } },
                        { $"{ports.TvPort}/udp", new List<PortBinding> { new() { HostPort = ports.TvPort.ToString() } } }
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
        }
        if (!string.IsNullOrWhiteSpace(_serversConfiguration.Network.Name))
        {
            await dockerClient.Networks.ConnectNetworkAsync(_serversConfiguration.Network.Name, new NetworkConnectParameters
            {
                Container = containerResponse.ID
            }, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(_serversConfiguration.Network.AdditionalNetworks))
        {
            var networks = _serversConfiguration.Network.AdditionalNetworks.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var net in networks)
            {
                var netName = net.Trim();
                if (!string.IsNullOrEmpty(netName) && netName != _serversConfiguration.Network.Name)
                {
                    await dockerClient.Networks.ConnectNetworkAsync(netName, new NetworkConnectParameters
                    {
                        Container = containerResponse.ID
                    }, cancellationToken);
                }
            }
        }
        logger.LogInformation("Starting Docker container {ContainerId}", containerResponse.ID);
        await dockerClient.Containers.StartContainerAsync(containerResponse.ID, new ContainerStartParameters(),
            cancellationToken);

        var serverHost = configuration["ServerHost"] ?? "localhost";
        var connectUrl = $"{serverHost}:{ports.GamePort}";

        logger.LogInformation("Dynamic server {ContainerId} successfully started on {ConnectUrl}", containerId, connectUrl);
        return new ServerResult
        {
            ServerId = containerId,
            GamePort = ports.GamePort,
            RconPort = ports.TvPort,
            ConnectUrl = connectUrl
        };
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        var dirs = dir.GetDirectories();
        Directory.CreateDirectory(destinationDir);

        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        foreach (var subDir in dirs)
        {
            var newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
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

        var memo = containerId.Replace("cs2-server-", "");
        var volumeName = $"cs2-vol-instance-{memo}";
        
        try
        {
            logger.LogInformation("Deleting Docker volume: {VolumeName}", volumeName);
            await Task.Delay(1000, cancellationToken); // Wait for Docker to fully detach the volume
            await dockerClient.Volumes.RemoveAsync(volumeName, true, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete volume {VolumeName} or it did not exist.", volumeName);
        }

        try
        {
            var instanceUpperPath = _serversConfiguration.UpperDir(memo);
            var instanceWorkPath = _serversConfiguration.WorkDir(memo);

            if (Directory.Exists(instanceUpperPath))
            {
                logger.LogInformation("Deleting upper directory: {Path}", instanceUpperPath);
                Directory.Delete(instanceUpperPath, true);
            }
            if (Directory.Exists(instanceWorkPath))
            {
                logger.LogInformation("Deleting work directory: {Path}", instanceWorkPath);
                Directory.Delete(instanceWorkPath, true);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete overlay directories for container {ContainerId}", containerId);
        }

        logger.LogInformation("Successfully deleted container: {ContainerId}", containerId);
    }

    private JsonElement MergeConfigs(JsonElement? defaults, JsonElement? overrides)
    {
        if (defaults == null || defaults.Value.ValueKind != JsonValueKind.Object)
        {
            if (overrides == null || overrides.Value.ValueKind != JsonValueKind.Object)
                return JsonSerializer.SerializeToElement(new { });
            return overrides.Value;
        }

        if (overrides == null || overrides.Value.ValueKind != JsonValueKind.Object)
            return defaults.Value;

        var mergedDict = new Dictionary<string, object?>();

        foreach (var prop in defaults.Value.EnumerateObject())
        {
            mergedDict[prop.Name] = GetElementValue(prop.Value);
        }

        foreach (var prop in overrides.Value.EnumerateObject())
        {
            mergedDict[prop.Name] = GetElementValue(prop.Value);
        }

        return JsonSerializer.SerializeToElement(mergedDict);
    }

    private object? GetElementValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => element // Keep as JsonElement for nested objects/arrays (shallow merge for now)
        };
    }

    private string WriteCfgFormat(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object) return "";
        var sb = new StringBuilder();
        foreach (var prop in json.EnumerateObject())
        {
            var key = prop.Name;
            var val = prop.Value;
            var valStr = val.ValueKind == JsonValueKind.String ? val.GetString() : val.GetRawText();
            sb.AppendLine($"{key} {valStr}");
        }
        return sb.ToString();
    }
}