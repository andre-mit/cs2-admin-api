using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Cs2Admin.API.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cs2Admin.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/presets")]
    public class ServerPresetsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ServerPresetsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServerPresetDto>>> GetPresets()
        {
            var presets = await _context.ServerPresets.ToListAsync();
            return Ok(presets.Select(p => new ServerPresetDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                ServerVariables = p.ServerVariables,
                PluginIds = p.PluginIds
            }));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServerPresetDto>> GetPreset(int id)
        {
            var preset = await _context.ServerPresets.FindAsync(id);

            if (preset == null)
            {
                return NotFound();
            }

            return new ServerPresetDto
            {
                Id = preset.Id,
                Name = preset.Name,
                Description = preset.Description,
                ServerVariables = preset.ServerVariables,
                PluginIds = preset.PluginIds
            };
        }

        [HttpPost]
        public async Task<ActionResult<ServerPresetDto>> CreatePreset(CreateServerPresetDto dto)
        {
            var preset = new ServerPreset
            {
                Name = dto.Name,
                Description = dto.Description,
                ServerVariables = dto.ServerVariables,
                PluginIds = dto.PluginIds
            };

            _context.ServerPresets.Add(preset);
            await _context.SaveChangesAsync();

            var result = new ServerPresetDto
            {
                Id = preset.Id,
                Name = preset.Name,
                Description = preset.Description,
                ServerVariables = preset.ServerVariables,
                PluginIds = preset.PluginIds
            };

            return CreatedAtAction(nameof(GetPreset), new { id = preset.Id }, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePreset(int id, CreateServerPresetDto dto)
        {
            var preset = await _context.ServerPresets.FindAsync(id);

            if (preset == null)
            {
                return NotFound();
            }

            preset.Name = dto.Name;
            preset.Description = dto.Description;
            preset.ServerVariables = dto.ServerVariables;
            preset.PluginIds = dto.PluginIds;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePreset(int id)
        {
            var preset = await _context.ServerPresets.FindAsync(id);
            if (preset == null)
            {
                return NotFound();
            }

            _context.ServerPresets.Remove(preset);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
