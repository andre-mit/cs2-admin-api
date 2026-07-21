using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using StackExchange.Redis;

using Microsoft.AspNetCore.Authorization;

using Cs2Admin.API.Infrastructure.Repositories;
namespace Cs2Admin.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class MapsController(
        IMapRepository mapRepository,
        IConnectionMultiplexer redis,
        IConfiguration config) : ControllerBase
    {

        [HttpGet]
        public async Task<ActionResult<IEnumerable<GameMap>>> GetMaps()
        {
            return Ok(await mapRepository.GetAllAsync());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<GameMap>> GetMap(int id)
        {
            var map = await mapRepository.GetByIdAsync(id);
            if (map == null) return NotFound();
            return map;
        }

        [HttpPost]
        public async Task<ActionResult<GameMap>> CreateMap(GameMap map)
        {
            await mapRepository.AddAsync(map);
            await mapRepository.SaveChangesAsync();
            await ProtectS3Keys(map);
            return CreatedAtAction(nameof(GetMap), new { id = map.Id }, map);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMap(int id, GameMap map)
        {
            if (id != map.Id) return BadRequest();

            mapRepository.Update(map);
            
            try
            {
                await mapRepository.SaveChangesAsync();
                await ProtectS3Keys(map);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await mapRepository.ExistsAsync(e => e.Id == id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMap(int id)
        {
            var map = await mapRepository.GetByIdAsync(id);
            if (map == null) return NotFound();

            mapRepository.Remove(map);
            await mapRepository.SaveChangesAsync();

            return NoContent();
        }

        private async Task ProtectS3Keys(GameMap map)
        {
            var db = redis.GetDatabase();
            var serviceUrl = config["S3:ServiceUrl"] ?? "";
            var bucket = config["S3:BucketName"] ?? "cs2";
            var prefix = $"{serviceUrl}/{bucket}/";

            foreach (var url in new[] { map.ImageUrl, map.BadgeUrl })
            {
                if (!string.IsNullOrEmpty(url) && url.StartsWith(prefix))
                {
                    var s3Key = url[prefix.Length..];
                    await db.SortedSetRemoveAsync("pending_uploads", s3Key);
                }
            }
        }
    }
}
