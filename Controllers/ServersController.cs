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

        private bool ServerExists(int id)
        {
            return context.Servers.Any(e => e.Id == id);
        }
    }
}
