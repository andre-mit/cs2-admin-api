using System;
using System.Threading.Tasks;
using Cs2Admin.API.Models;
using Cs2Admin.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cs2Admin.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    // [Authorize] - Uncomment in production
    public class PluginsController : ControllerBase
    {
        private readonly IPluginService _pluginService;

        public PluginsController(IPluginService pluginService)
        {
            _pluginService = pluginService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var plugins = await _pluginService.GetAllAsync(HttpContext.RequestAborted);
            return Ok(plugins);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var plugin = await _pluginService.GetByIdAsync(id, HttpContext.RequestAborted);
            if (plugin == null) return NotFound();
            return Ok(plugin);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] GamePlugin plugin)
        {
            var created = await _pluginService.CreateAsync(plugin, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPost("{id}/upload")]
        public async Task<IActionResult> Upload(int id, IFormFile file)
        {
            try
            {
                var plugin = await _pluginService.UploadAsync(id, file, HttpContext.RequestAborted);
                return Ok(plugin);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _pluginService.DeleteAsync(id, HttpContext.RequestAborted);
            return NoContent();
        }
    }
}
