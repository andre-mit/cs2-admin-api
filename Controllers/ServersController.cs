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

namespace Cs2Admin.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ServersController(
        ApplicationDbContext context,
        IRconService rconService,
        IServerService serverService,
        IConfiguration configuration,
        Cs2Admin.API.Services.BaseUpdateState baseUpdateState,
        IServiceScopeFactory scopeFactory,
        ILogger<ServersController> logger) : ControllerBase
    {

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Server>>> GetServers()
        {
            return await context.Servers.OrderByDescending(s => s.CreatedAt).ToListAsync();
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Server>> GetServer(int id)
        {
            var server = await context.Servers.FindAsync(id);

            if (server == null)
            {
                return NotFound();
            }

            return server;
        }

        [HttpGet("{id:int}/status")]
        public async Task<IActionResult> GetServerStatus(int id)
        {
            var server = await context.Servers.FindAsync(id);
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

        [HttpPost]
        public async Task<ActionResult<Server>> CreateServer(Server server)
        {
            context.Servers.Add(server);
            await context.SaveChangesAsync();

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

            return CreatedAtAction(nameof(GetServer), new { id = server.Id }, result);
        }

        /// <summary>
        /// Start a dynamic server container.
        /// </summary>
        [HttpPost("{id:int}/start")]
        public async Task<IActionResult> StartServer(int id, CancellationToken cancellationToken)
        {
            var server = await context.Servers.FindAsync(id);
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
            var server = await context.Servers.FindAsync(id);
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
            var server = await context.Servers.FindAsync(id);
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
            var server = await context.Servers.FindAsync(id);
            if (server == null) return NotFound();
            if (!server.IsDynamic || string.IsNullOrEmpty(server.ContainerId))
                return BadRequest("Only dynamic servers can be deleted via this endpoint.");

            await serverService.DeleteServerAsync(server.ContainerId, cancellationToken);

            context.Servers.Remove(server);
            await context.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateServer(int id, Server server)
        {
            if (id != server.Id)
            {
                return BadRequest();
            }

            context.Entry(server).State = EntityState.Modified;

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ServerExists(id))
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
            var server = await context.Servers.FindAsync(id);
            if (server == null)
            {
                return NotFound();
            }

            context.Servers.Remove(server);
            await context.SaveChangesAsync();

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

        private bool ServerExists(int id)
        {
            return context.Servers.Any(e => e.Id == id);
        }
    }
}
