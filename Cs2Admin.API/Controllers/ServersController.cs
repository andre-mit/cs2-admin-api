using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Cs2Admin.API.Services.Interfaces;
using Cs2Admin.API.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.Authorization;

using Cs2Admin.API.Infrastructure.Repositories;
namespace Cs2Admin.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ServersController(
        IServerRepository serverRepository,
        ApplicationDbContext context, // Temporarily keeping for complex queries
        IRconService rconService,
        IServerService serverService,
        IServerEventService eventService,
        IConfiguration configuration,
        Cs2Admin.API.Services.BaseUpdateState baseUpdateState,
        IServiceScopeFactory scopeFactory,
        Docker.DotNet.IDockerClient dockerClient,
        ILogger<ServersController> logger) : ControllerBase
    {

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Server>>> GetServers()
        {
            var servers = await serverRepository.GetServersOrderByDescendingAsync();
            return Ok(servers);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Server>> GetServer(int id)
        {
            var server = await serverRepository.GetByIdAsync(id);

            if (server == null)
            {
                return NotFound();
            }

            return server;
        }

        [HttpGet("events")]
        public async Task GetEvents(CancellationToken cancellationToken)
        {
            await eventService.RegisterClientAsync(Response, cancellationToken);
        }

        [HttpGet("{id:int}/status")]
        public async Task<IActionResult> GetServerStatus(int id)
        {
            var server = await serverRepository.GetByIdAsync(id);
            if (server == null) return NotFound();

            try
            {
                var response = await rconService.SendCommandAsync(
                    server.IpString,
                    server.Port,
                    server.RconPassword ?? "",
                    "status"
                );
                return Ok(new { online = true, response });
            }
            catch
            {
                return Ok(new { online = false, response = "" });
            }
        }

        [HttpGet("{id:int}/health")]
        public async Task<IActionResult> GetServerHealth(int id, CancellationToken cancellationToken)
        {
            var server = await serverRepository.GetByIdAsync(id);
            if (server == null) return NotFound();

            if (!server.IsDynamic || string.IsNullOrEmpty(server.ContainerId))
            {
                // For static servers, just ping RCON
                try
                {
                    // Add timeout manually since IRconService doesn't accept one
                    var rconTask = rconService.SendCommandAsync(server.IpString, server.Port, server.RconPassword ?? "", "status");
                    if (await Task.WhenAny(rconTask, Task.Delay(2000, cancellationToken)) == rconTask)
                    {
                        await rconTask; // throw if failed
                        return Ok(new { status = "online", isDynamic = false });
                    }
                    else
                    {
                        return Ok(new { status = "offline", isDynamic = false });
                    }
                }
                catch
                {
                    return Ok(new { status = "offline", isDynamic = false });
                }
            }

            // For dynamic servers, inspect container
            try
            {
                var inspect = await dockerClient.Containers.InspectContainerAsync(server.ContainerId, cancellationToken);
                var state = inspect.State.Status.ToLower(); // "running", "restarting", "exited", etc.

                if (state == "running")
                {
                    // Check if RCON is reachable
                    try
                    {
                        var rconTask = rconService.SendCommandAsync(server.IpString, server.Port, server.RconPassword ?? "", "status");
                        if (await Task.WhenAny(rconTask, Task.Delay(2000, cancellationToken)) == rconTask)
                        {
                            await rconTask; // throw if failed
                            return Ok(new { status = "online", isDynamic = true });
                        }
                        else
                        {
                            return Ok(new { status = "starting", isDynamic = true });
                        }
                    }
                    catch
                    {
                        // Container is running but RCON failed. Likely starting up.
                        return Ok(new { status = "starting", isDynamic = true });
                    }
                }
                else if (state == "restarting")
                {
                    return Ok(new { status = "restarting", isDynamic = true });
                }
                else
                {
                    return Ok(new { status = "offline", isDynamic = true });
                }
            }
            catch (Docker.DotNet.DockerContainerNotFoundException)
            {
                return Ok(new { status = "offline", isDynamic = true });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to inspect container {ContainerId}", server.ContainerId);
                return StatusCode(500, new { message = "Failed to inspect container status." });
            }
        }

        [HttpGet("{id:int}/logs")]
        public async Task GetServerLogs(int id, CancellationToken cancellationToken)
        {
            var server = await serverRepository.GetByIdAsync(id);
            if (server == null || string.IsNullOrEmpty(server.ContainerId))
            {
                Response.StatusCode = 404;
                return;
            }

            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");

            try
            {
                using var logStream = await dockerClient.Containers.GetContainerLogsAsync(server.ContainerId, false, new Docker.DotNet.Models.ContainerLogsParameters
                {
                    ShowStdout = true,
                    ShowStderr = true,
                    Follow = true,
                    Tail = "200"
                }, cancellationToken);

                var buffer = new byte[8192];
                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = await logStream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (result.EOF)
                    {
                        var pingData = System.Text.Json.JsonSerializer.Serialize(new { ping = true });
                        await Response.WriteAsync($"data: {pingData}\n\n", cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);
                        await Task.Delay(2000, cancellationToken);
                        continue;
                    }

                    if (result.Count > 0)
                    {
                        var logLine = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                        var data = System.Text.Json.JsonSerializer.Serialize(new { log = logLine });
                        await Response.WriteAsync($"data: {data}\n\n", cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error streaming logs for container {ContainerId}", server.ContainerId);
            }
        }

        [HttpGet("{id:int}/logs/download")]
        public async Task<IActionResult> DownloadServerLogs(int id, CancellationToken cancellationToken)
        {
            var server = await serverRepository.GetByIdAsync(id);
            if (server == null || string.IsNullOrEmpty(server.ContainerId))
            {
                return NotFound();
            }

            try
            {
                using var logStream = await dockerClient.Containers.GetContainerLogsAsync(server.ContainerId, false, new Docker.DotNet.Models.ContainerLogsParameters
                {
                    ShowStdout = true,
                    ShowStderr = true,
                    Follow = false,
                    Tail = "all"
                }, cancellationToken);

                using var ms = new System.IO.MemoryStream();
                var buffer = new byte[8192];
                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = await logStream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (result.EOF) break;
                    if (result.Count > 0)
                    {
                        await ms.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken);
                    }
                }

                var logsBytes = ms.ToArray();
                return File(logsBytes, "text/plain", $"server-{id}-logs.txt");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to download logs for container {ContainerId}", server.ContainerId);
                return StatusCode(500, new { message = "Failed to retrieve logs." });
            }
        }

        [HttpPost]
        public async Task<ActionResult<Server>> CreateServer(Server server)
        {
            await serverRepository.AddAsync(server);
            await serverRepository.SaveChangesAsync();

            await eventService.BroadcastEventAsync("server_list_changed", new { });
            return CreatedAtAction(nameof(GetServer), new { id = server.Id }, server);
        }

        /// <summary>
        /// Creates a dynamic server via Docker container and persists it in the database.
        /// </summary>
        [HttpPost("dynamic")]
        public async Task<ActionResult<ServerResult>> CreateDynamicServer(
            [FromBody] ServerRequest serverRequest,
            CancellationToken cancellationToken)
        {
            ServerResult result;
            try
            {
                result = await serverService.CreateServerAsync(serverRequest, cancellationToken);
            }
            catch (Cs2Admin.API.Exceptions.NoAvailableServersException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Docker.DotNet.DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                return StatusCode(409, new { message = "A server with this name or token already exists. Please try another token or clear old servers." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create dynamic server. Request Name: {Name}", serverRequest.Name);
                return StatusCode(500, new { message = ex.Message });
            }

            var serverHost = configuration["ServerHost"] ?? "localhost";

            var server = new Server
            {
                IpString = serverHost,
                Port = result.GamePort,
                TvPort = result.RconPort,
                RconPassword = serverRequest.RconPassword,
                ServerPassword = serverRequest.Password,
                DisplayName = serverRequest.Name,
                ContainerId = result.ServerId,
                IsDynamic = true,
                InUse = false,
                CreatedAt = DateTime.UtcNow
            };

            context.Servers.Add(server);
            await context.SaveChangesAsync(cancellationToken);

            if (serverRequest.PluginSelections.Count != 0)
            {
                foreach (var selection in serverRequest.PluginSelections)
                {
                    context.ServerPlugins.Add(new ServerPlugin
                    {
                        ServerId = server.Id,
                        GamePluginId = selection.PluginId,
                        ConfigOverridesJson = selection.ConfigOverridesJson
                    });
                }
                await context.SaveChangesAsync(cancellationToken);
            }

            await eventService.BroadcastEventAsync("server_list_changed", new { });
            return CreatedAtAction(nameof(GetServer), new { id = server.Id }, result);
        }

        /// <summary>
        /// Start a dynamic server container.
        /// </summary>
        [HttpPost("{id:int}/start")]
        public async Task<IActionResult> StartServer(int id, CancellationToken cancellationToken)
        {
            var server = await serverRepository.GetByIdAsync(id);
            if (server == null) return NotFound();
            if (!server.IsDynamic || string.IsNullOrEmpty(server.ContainerId))
                return BadRequest("Only dynamic servers can be started via this endpoint.");

            await serverService.StartServerAsync(server.ContainerId, cancellationToken);
            return Ok(new { message = "Server started successfully." });
        }

        /// <summary>
        /// Stop a dynamic server container.
        /// </summary>
        [HttpPost("{id}/stop")]
        public async Task<IActionResult> StopServer(int id, CancellationToken cancellationToken)
        {
            var server = await serverRepository.GetByIdAsync(id);
            if (server == null) return NotFound();
            if (!server.IsDynamic || string.IsNullOrEmpty(server.ContainerId))
                return BadRequest("Only dynamic servers can be stopped via this endpoint.");

            await serverService.StopServerAsync(server.ContainerId, cancellationToken);
            return Ok(new { message = "Server stopped successfully." });
        }

        /// <summary>
        /// Restart a dynamic server container.
        /// </summary>
        [HttpPost("{id}/restart")]
        public async Task<IActionResult> RestartServer(int id, CancellationToken cancellationToken)
        {
            var server = await serverRepository.GetByIdAsync(id);
            if (server == null) return NotFound();
            if (!server.IsDynamic || string.IsNullOrEmpty(server.ContainerId))
                return BadRequest("Only dynamic servers can be restarted via this endpoint.");

            await serverService.RestartServerAsync(server.ContainerId, cancellationToken);
            return Ok(new { message = "Server restarted successfully." });
        }

        /// <summary>
        /// Delete a dynamic server: removes the Docker container and database record.
        /// </summary>
        [HttpDelete("{id}/dynamic")]
        public async Task<IActionResult> DeleteDynamicServer(int id, CancellationToken cancellationToken)
        {
            var server = await serverRepository.GetByIdAsync(id);
            if (server == null) return NotFound();
            if (!server.IsDynamic || string.IsNullOrEmpty(server.ContainerId))
                return BadRequest("Only dynamic servers can be deleted via this endpoint.");

            await serverService.DeleteServerAsync(server.ContainerId, cancellationToken);

            serverRepository.Remove(server);
            await serverRepository.SaveChangesAsync();

            await eventService.BroadcastEventAsync("server_list_changed", new { });

            return NoContent();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateServer(int id, Server server)
        {
            if (id != server.Id)
            {
                return BadRequest();
            }

            serverRepository.Update(server);

            try
            {
                await serverRepository.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await ServerExistsAsync(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteServer(int id)
        {
            var server = await serverRepository.GetByIdAsync(id);
            if (server == null)
            {
                return NotFound();
            }

            serverRepository.Remove(server);
            await serverRepository.SaveChangesAsync();

            await eventService.BroadcastEventAsync("server_list_changed", new { });

            return NoContent();
        }

        [HttpPost("update-base")]
        public IActionResult UpdateBaseServer()
        {
            if (baseUpdateState.IsUpdating)
            {
                return BadRequest(new { message = "Update is already in progress." });
            }

            baseUpdateState.IsUpdating = true;
            baseUpdateState.Status = "stopping_servers";

            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var scopedContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var scopedServerService = scope.ServiceProvider.GetRequiredService<IServerService>();
                var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<ServersController>>();

                try
                {
                    var runningServers = await scopedContext.Servers
                        .Where(s => s.IsDynamic && !string.IsNullOrEmpty(s.ContainerId))
                        .ToListAsync();

                    var stoppedServerIds = new List<string>();

                    foreach (var server in runningServers)
                    {
                        try
                        {
                            await scopedServerService.StopServerAsync(server.ContainerId!, CancellationToken.None);
                            stoppedServerIds.Add(server.ContainerId!);
                        }
                        catch (Exception ex)
                        {
                            scopedLogger.LogWarning(ex, "Could not stop server {ContainerId} or it was already stopped.", server.ContainerId);
                        }
                    }

                    await scopedServerService.UpdateBaseServerAsync(CancellationToken.None);

                    baseUpdateState.Status = "restarting_servers";
                    foreach (var containerId in stoppedServerIds)
                    {
                        try
                        {
                            await scopedServerService.StartServerAsync(containerId, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            scopedLogger.LogError(ex, "Failed to automatically restart server {ContainerId} after update.", containerId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    scopedLogger.LogError(ex, "Background base server update failed.");
                }
                finally
                {
                    baseUpdateState.Reset();
                }
            });

            return Accepted(new { message = "Base game update started in the background." });
        }

        [HttpGet("update-base-stream")]
        public async Task GetUpdateBaseStream(CancellationToken cancellationToken)
        {
            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");

            while (!cancellationToken.IsCancellationRequested)
            {
                var data = System.Text.Json.JsonSerializer.Serialize(new
                {
                    isUpdating = baseUpdateState.IsUpdating,
                    progressPercentage = baseUpdateState.ProgressPercentage,
                    downloadedBytes = baseUpdateState.DownloadedBytes,
                    totalBytes = baseUpdateState.TotalBytes,
                    status = baseUpdateState.Status
                });

                await Response.WriteAsync($"data: {data}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);

                if (!baseUpdateState.IsUpdating)
                {
                    break;
                }

                await Task.Delay(5000, cancellationToken);
            }
        }

        private async Task<bool> ServerExistsAsync(int id)
        {
            return await serverRepository.ExistsAsync(e => e.Id == id);
        }
    }
}
