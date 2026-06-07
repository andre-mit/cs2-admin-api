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
    public class ServersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IRconService _rcon;
        private readonly IServerService _serverService;
        private readonly IConfiguration _configuration;

        public ServersController(
            ApplicationDbContext context,
            IRconService rcon,
            IServerService serverService,
            IConfiguration configuration)
        {
            _context = context;
            _rcon = rcon;
            _serverService = serverService;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Server>>> GetServers()
        {
            return await _context.Servers.OrderByDescending(s => s.CreatedAt).ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Server>> GetServer(int id)
        {
            var server = await _context.Servers.FindAsync(id);

            if (server == null)
            {
                return NotFound();
            }

            return server;
        }

        [HttpGet("{id}/status")]
        public async Task<IActionResult> GetServerStatus(int id)
        {
            var server = await _context.Servers.FindAsync(id);
            if (server == null) return NotFound();

            try
            {
                var response = await _rcon.SendCommandAsync(
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
            _context.Servers.Add(server);
            await _context.SaveChangesAsync();

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
            var result = await _serverService.CreateServerAsync(serverRequest, cancellationToken);

            var serverHost = _configuration["ServerHost"] ?? "localhost";

            var server = new Server
            {
                IpString = serverHost,
                Port = result.GamePort,
                TvPort = result.RconPort,
                RconPassword = serverRequest.RconPassword,
                DisplayName = serverRequest.Name,
                ContainerId = result.ServerId,
                IsDynamic = true,
                InUse = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Servers.Add(server);
            await _context.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(nameof(GetServer), new { id = server.Id }, result);
        }

        /// <summary>
        /// Start a dynamic server container.
        /// </summary>
        [HttpPost("{id}/start")]
        public async Task<IActionResult> StartServer(int id, CancellationToken cancellationToken)
        {
            var server = await _context.Servers.FindAsync(id);
            if (server == null) return NotFound();
            if (!server.IsDynamic || string.IsNullOrEmpty(server.ContainerId))
                return BadRequest("Only dynamic servers can be started via this endpoint.");

            await _serverService.StartServerAsync(server.ContainerId, cancellationToken);
            return Ok(new { message = "Server started successfully." });
        }

        /// <summary>
        /// Stop a dynamic server container.
        /// </summary>
        [HttpPost("{id}/stop")]
        public async Task<IActionResult> StopServer(int id, CancellationToken cancellationToken)
        {
            var server = await _context.Servers.FindAsync(id);
            if (server == null) return NotFound();
            if (!server.IsDynamic || string.IsNullOrEmpty(server.ContainerId))
                return BadRequest("Only dynamic servers can be stopped via this endpoint.");

            await _serverService.StopServerAsync(server.ContainerId, cancellationToken);
            return Ok(new { message = "Server stopped successfully." });
        }

        /// <summary>
        /// Restart a dynamic server container.
        /// </summary>
        [HttpPost("{id}/restart")]
        public async Task<IActionResult> RestartServer(int id, CancellationToken cancellationToken)
        {
            var server = await _context.Servers.FindAsync(id);
            if (server == null) return NotFound();
            if (!server.IsDynamic || string.IsNullOrEmpty(server.ContainerId))
                return BadRequest("Only dynamic servers can be restarted via this endpoint.");

            await _serverService.RestartServerAsync(server.ContainerId, cancellationToken);
            return Ok(new { message = "Server restarted successfully." });
        }

        /// <summary>
        /// Delete a dynamic server: removes the Docker container and database record.
        /// </summary>
        [HttpDelete("{id}/dynamic")]
        public async Task<IActionResult> DeleteDynamicServer(int id, CancellationToken cancellationToken)
        {
            var server = await _context.Servers.FindAsync(id);
            if (server == null) return NotFound();
            if (!server.IsDynamic || string.IsNullOrEmpty(server.ContainerId))
                return BadRequest("Only dynamic servers can be deleted via this endpoint.");

            await _serverService.DeleteServerAsync(server.ContainerId, cancellationToken);

            _context.Servers.Remove(server);
            await _context.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateServer(int id, Server server)
        {
            if (id != server.Id)
            {
                return BadRequest();
            }

            _context.Entry(server).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
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
            var server = await _context.Servers.FindAsync(id);
            if (server == null)
            {
                return NotFound();
            }

            _context.Servers.Remove(server);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ServerExists(int id)
        {
            return _context.Servers.Any(e => e.Id == id);
        }
    }
}
