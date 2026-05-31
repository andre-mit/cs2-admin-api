using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Cs2Admin.API.Services;
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

        public ServersController(ApplicationDbContext context, IRconService rcon)
        {
            _context = context;
            _rcon = rcon;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Server>>> GetServers()
        {
            return await _context.Servers.ToListAsync();
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
