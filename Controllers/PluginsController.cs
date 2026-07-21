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
    [Authorize]
    public class PluginsController(IPluginService pluginService) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var plugins = await pluginService.GetAllAsync(HttpContext.RequestAborted);
            return Ok(plugins);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var plugin = await pluginService.GetByIdAsync(id, HttpContext.RequestAborted);
            if (plugin == null) return NotFound();
            return Ok(plugin);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] GamePlugin plugin)
        {
            var created = await pluginService.CreateAsync(plugin, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] GamePlugin plugin)
        {
            try
            {
                var updated = await pluginService.UpdateAsync(id, plugin, HttpContext.RequestAborted);
                return Ok(updated);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("{id}/upload")]
        public async Task<IActionResult> Upload(int id, IFormFile file)
        {
            try
            {
                var plugin = await pluginService.UploadAsync(id, file, HttpContext.RequestAborted);
                return Ok(plugin);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("{id}/upload-chunk")]
        public async Task<IActionResult> UploadChunk(int id, [FromForm] int chunkIndex, [FromForm] int totalChunks, IFormFile file)
        {
            try
            {
                var plugin = await pluginService.UploadChunkAsync(id, file, chunkIndex, totalChunks, HttpContext.RequestAborted);
                return Ok(plugin);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("{id}/files")]
        public async Task<IActionResult> GetFiles(int id)
        {
            var tree = await pluginService.GetFileTreeAsync(id, HttpContext.RequestAborted);
            if (tree == null) return NotFound();
            return Ok(tree);
        }

        [HttpGet("{id}/file")]
        public async Task<IActionResult> GetFileContent(int id, [FromQuery] string path)
        {
            try
            {
                var content = await pluginService.GetFileContentAsync(id, path, HttpContext.RequestAborted);
                if (content == null) return NotFound();
                return Ok(new { content });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("{id}/file")]
        public async Task<IActionResult> SaveFileContent(int id, [FromBody] FileEditRequest request)
        {
            try
            {
                await pluginService.SaveFileContentAsync(id, request.Path, request.Content, HttpContext.RequestAborted);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("{id}/file")]
        public async Task<IActionResult> DeleteFile(int id, [FromQuery] string path)
        {
            try
            {
                await pluginService.DeleteFileAsync(id, path, HttpContext.RequestAborted);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await pluginService.DeleteAsync(id, HttpContext.RequestAborted);
            return NoContent();
        }
    }
}
