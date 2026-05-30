using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using StackExchange.Redis;

namespace Cs2Admin.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class MapsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConnectionMultiplexer _redis;
        private readonly IConfiguration _config;

        public MapsController(ApplicationDbContext context, IConnectionMultiplexer redis, IConfiguration config)
        {
            _context = context;
            _redis = redis;
            _config = config;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<GameMap>>> GetMaps()
        {
            return await _context.Maps.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<GameMap>> GetMap(int id)
        {
            var map = await _context.Maps.FindAsync(id);
            if (map == null) return NotFound();
            return map;
        }

        [HttpPost]
        public async Task<ActionResult<GameMap>> CreateMap(GameMap map)
        {
            _context.Maps.Add(map);
            await _context.SaveChangesAsync();
            await ProtectS3Keys(map);
            return CreatedAtAction(nameof(GetMap), new { id = map.Id }, map);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMap(int id, GameMap map)
        {
            if (id != map.Id) return BadRequest();

            _context.Entry(map).State = EntityState.Modified;
            
            try
            {
                await _context.SaveChangesAsync();
                await ProtectS3Keys(map);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Maps.Any(e => e.Id == id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMap(int id)
        {
            var map = await _context.Maps.FindAsync(id);
            if (map == null) return NotFound();

            _context.Maps.Remove(map);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task ProtectS3Keys(GameMap map)
        {
            var db = _redis.GetDatabase();
            var serviceUrl = _config["S3:ServiceUrl"] ?? "";
            var bucket = _config["S3:BucketName"] ?? "cs2";
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
