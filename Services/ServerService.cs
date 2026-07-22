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
    BaseUpdateState baseUpdateState,
    ILogger<ServerService> logger)
    : IServerService
{
    private readonly ServersConfiguration _serversConfiguration = serversConfigurationOptions.Value;

    public async Task UpdateBaseServerAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting Base Server Update bypassing full hash validation...");

        var containerId = "cs2-base-updater";
        
        try
        {
            // Remove if already exists from a previous failed run
            await dockerClient.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true }, cancellationToken);
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Ignore
        }

        var createParams = new CreateContainerParameters
        {
            Image = "joedwards32/cs2",
            Name = containerId,
            Env = ["STEAMAPPVALIDATE=0", "CS2_ADDITIONAL_ARGS=+quit"], // Bypass validation and quit after launch
            HostConfig = new HostConfig
            {
                Binds = [$"{_serversConfiguration.GameBaseDir}:/home/steam/cs2-dedicated"],
                AutoRemove = true
            }
        };

        logger.LogInformation("Creating temporary update container...");
        CreateContainerResponse containerResponse;
        try
        {
            containerResponse = await dockerClient.Containers.CreateContainerAsync(createParams, cancellationToken);
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
            containerResponse = await dockerClient.Containers.CreateContainerAsync(createParams, cancellationToken);
        }
        
        logger.LogInformation("Starting temporary update container...");
        await dockerClient.Containers.StartContainerAsync(containerResponse.ID, new ContainerStartParameters(), cancellationToken);

        baseUpdateState.Status = "downloading";

        logger.LogInformation("Attaching to container logs...");
        using var logStream = await dockerClient.Containers.GetContainerLogsAsync(containerResponse.ID, false, new ContainerLogsParameters
        {
            ShowStdout = true,
            ShowStderr = true,
            Follow = true
        }, cancellationToken);

        var regex = new System.Text.RegularExpressions.Regex(@"progress:\s+(\d+\.\d+)\s+\(([^/]+)\s+/\s+([^\)]+)\)");
        var buffer = new byte[81920];

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await logStream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);
            if (result.EOF) break;

            var line = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
            
            // Log output can contain multiple matches
            var matches = regex.Matches(line);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Success)
                {
                    if (double.TryParse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var prog))
                        baseUpdateState.ProgressPercentage = prog;
                    
                    baseUpdateState.DownloadedBytes = match.Groups[2].Value.Trim();
                    baseUpdateState.TotalBytes = match.Groups[3].Value.Trim();
                }
            }
        }

        logger.LogInformation("Waiting for update container to finish...");
        await dockerClient.Containers.WaitContainerAsync(containerResponse.ID, cancellationToken);

        baseUpdateState.Reset();
        logger.LogInformation("Base Server Update completed successfully.");
    }

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
                { "name", new Dictionary<string, bool> { { $"^/cs2-server-{token.Memo}$", true } } }
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

        bool fastDlRequired = false;
        if (serverRequest.PluginSelections is { Count: > 0 })
        {
            var serializerOptions = new JsonSerializerOptions { WriteIndented = true };

            foreach (var selection in serverRequest.PluginSelections)
            {
                var plugin = await dbContext.GamePlugins.FindAsync([selection.PluginId], cancellationToken);
                if (plugin == null) continue;

                var templatePluginPath = Path.Combine(_serversConfiguration.PluginsBaseDir, plugin.Name);

                if (Directory.Exists(templatePluginPath))
                {
                    var subdirs = Directory.GetDirectories(templatePluginPath).Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var structuralRoots = new[] { "addons", "cfg", "materials", "models", "sound" };
                    bool hasStructuralRoots = subdirs.Intersect(structuralRoots).Any();

                    if (hasStructuralRoots)
                    {
                        var destinationPluginPath = Path.Combine(instanceUpperPath, "game/csgo");
                        CopyDirectory(templatePluginPath, destinationPluginPath);
                        
                        if (subdirs.Contains("materials") || subdirs.Contains("models") || subdirs.Contains("sound"))
                        {
                            fastDlRequired = true;
                            Directory.CreateDirectory(_serversConfiguration.FastDlBaseDir);
                            
                            foreach (var assetDir in new[] { "materials", "models", "sound" })
                            {
                                var assetSource = Path.Combine(templatePluginPath, assetDir);
                                if (Directory.Exists(assetSource))
                                {
                                    CopyDirectory(assetSource, Path.Combine(_serversConfiguration.FastDlBaseDir, assetDir));
                                }
                            }
                        }
                    }
                    else
                    {
                        var destinationPluginPath = Path.Combine(instanceUpperPath, "game/csgo/addons/counterstrikesharp/plugins", plugin.Name);
                        CopyDirectory(templatePluginPath, destinationPluginPath);
                    }

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

        var mergedEnvironment = new Dictionary<string, string>(_serversConfiguration.DefaultEnvVariables);

        mergedEnvironment["SRCDS_TOKEN"] = token.Token;
        mergedEnvironment["CS2_SERVERNAME"] = serverRequest.Name;
        
        if (!string.IsNullOrEmpty(serverRequest.Password))
        {
            mergedEnvironment["CS2_PW"] = serverRequest.Password;
        }
        else
        {
            mergedEnvironment.Remove("CS2_PW");
        }
        
        mergedEnvironment["CS2_MAXPLAYERS"] = serverRequest.MaxPlayers.ToString();
        
        string additionalArgs = "-tickrate 128";
        if (fastDlRequired && !string.IsNullOrWhiteSpace(_serversConfiguration.FastDlUrl))
        {
            additionalArgs += $" +sv_downloadurl \"{_serversConfiguration.FastDlUrl}\" +sv_allowdownload 1 +sv_allowupload 0";
        }
        mergedEnvironment["CS2_ADDITIONAL_ARGS"] = additionalArgs;

        mergedEnvironment["CS2_PORT"] = ports.GamePort.ToString();
        mergedEnvironment["TV_PORT"] = ports.TvPort.ToString();
        
        var finalRcon = string.IsNullOrEmpty(serverRequest.RconPassword) 
            ? Guid.NewGuid().ToString("N")[..16] 
            : serverRequest.RconPassword;
            
        mergedEnvironment["CS2_RCONPW"] = finalRcon;
        
        // Ensure we save the generated RCON password back so it can be saved to the database
        serverRequest.RconPassword = finalRcon;

        if (serverRequest.ServerVariables != null)
        {
            foreach (var kvp in serverRequest.ServerVariables)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                {
                    mergedEnvironment[kvp.Key] = kvp.Value;
                }
            }
        }

        // Generate server.cfg manually to bypass the container's broken template replacement
        var cfgDir = Path.Combine(instanceUpperPath, "game/csgo/cfg");
        Directory.CreateDirectory(cfgDir);
        var sb = new StringBuilder();
        sb.AppendLine($"hostname \"{serverRequest.Name}\"");
        sb.AppendLine($"rcon_password \"{finalRcon}\"");
        if (!string.IsNullOrEmpty(serverRequest.Password))
            sb.AppendLine($"sv_password \"{serverRequest.Password}\"");
        else
            sb.AppendLine("sv_password \"\"");

        sb.AppendLine($"sv_cheats {mergedEnvironment.GetValueOrDefault("CS2_CHEATS", "0")}");
        sb.AppendLine($"sv_hibernate_when_empty {mergedEnvironment.GetValueOrDefault("CS2_SERVER_HIBERNATE", "1")}");
        sb.AppendLine($"tv_autorecord {mergedEnvironment.GetValueOrDefault("TV_AUTORECORD", "1")}");
        sb.AppendLine($"tv_enable {mergedEnvironment.GetValueOrDefault("TV_ENABLE", "1")}");
        sb.AppendLine($"tv_maxrate {mergedEnvironment.GetValueOrDefault("TV_MAXRATE", "64")}");
        sb.AppendLine($"tv_port {ports.TvPort}");
        sb.AppendLine($"tv_password \"{mergedEnvironment.GetValueOrDefault("TV_PW", "")}\"");
        sb.AppendLine($"tv_relaypassword \"{mergedEnvironment.GetValueOrDefault("TV_RELAY_PW", "")}\"");
        sb.AppendLine($"log {mergedEnvironment.GetValueOrDefault("CS2_LOG", "on")}");
        sb.AppendLine($"sv_logfile {mergedEnvironment.GetValueOrDefault("CS2_LOG_FILE", "1")}");
        sb.AppendLine($"sv_logecho {mergedEnvironment.GetValueOrDefault("CS2_LOG_ECHO", "1")}");
        sb.AppendLine($"mp_logmoney {mergedEnvironment.GetValueOrDefault("CS2_LOG_MONEY", "1")}");
        sb.AppendLine($"mp_logdetail {mergedEnvironment.GetValueOrDefault("CS2_LOG_DETAIL", "3")}");
        sb.AppendLine($"mp_logdetail_items {mergedEnvironment.GetValueOrDefault("CS2_LOG_ITEMS", "0")}");
        sb.AppendLine($"mp_disconnect_kills_players {mergedEnvironment.GetValueOrDefault("CS2_DISCONNECT_KILLS", "0")}");

        // Write any custom CVARs directly to server.cfg
        if (serverRequest.ServerVariables != null)
        {
            foreach (var kvp in serverRequest.ServerVariables)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                {
                    if (!kvp.Key.StartsWith("CS2_") && !kvp.Key.StartsWith("TV_") && !kvp.Key.StartsWith("STEAM"))
                    {
                        sb.AppendLine($"{kvp.Key} \"{kvp.Value}\"");
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(serverRequest.CustomCfg))
        {
            var cfgName = string.IsNullOrWhiteSpace(serverRequest.CustomCfgName) ? "custom" : serverRequest.CustomCfgName.Trim();
            if (cfgName.EndsWith(".cfg")) cfgName = cfgName[..^4];
            
            await File.WriteAllTextAsync(Path.Combine(cfgDir, $"{cfgName}.cfg"), serverRequest.CustomCfg, Encoding.UTF8, cancellationToken);
            sb.AppendLine($"exec {cfgName}.cfg");
        }

        await File.WriteAllTextAsync(Path.Combine(cfgDir, "server.cfg"), sb.ToString(), Encoding.UTF8, cancellationToken);

        await GeneratePreShAsync(instanceUpperPath, cancellationToken);

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

        await steamTokenService.MarkTokenAsUsedAsync(token.Id, cancellationToken);

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

        await steamTokenService.MarkTokenAsAvailableByMemoAsync(memo, cancellationToken);

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

    private async Task GeneratePreShAsync(string upperPath, CancellationToken cancellationToken)
    {
        var preShPath = Path.Combine(upperPath, "pre.sh");
        
        // Ensure strictly LF endings
        var scriptContent = """
            #!/bin/bash
            # Redirect all output to Docker's PID 1 stdout so it appears in docker logs
            exec > /proc/1/fd/1 2>&1
            
            echo "[pre.sh] Starting custom initialization..."
            
            GAMEINFO="/home/steam/cs2-dedicated/game/csgo/gameinfo.gi"
            
            # Programmatically inject metamod if not present
            if [ -f "$GAMEINFO" ]; then
                if ! grep -q "Game csgo/addons/metamod" "$GAMEINFO"; then
                    echo "[pre.sh] Injecting Metamod into gameinfo.gi..."
                    sed -i '/Game_LowViolence csgo_lv/a \t\t\t\tGame csgo/addons/metamod' "$GAMEINFO"
                fi
            fi
            
            echo "[pre.sh] Initialization complete."
            """.Replace("\r\n", "\n");

        await File.WriteAllTextAsync(preShPath, scriptContent, new UTF8Encoding(false), cancellationToken);

#pragma warning disable CA1416
        // Apply execution permissions programmatically via .NET 7+ API
        File.SetUnixFileMode(preShPath, 
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | 
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute | 
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
#pragma warning restore CA1416
            
        logger.LogInformation("Successfully generated {PreShPath} with LF endings and execute permissions.", preShPath);
    }
}