using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.Authorization;

namespace Cs2Admin.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class MatchesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MatchesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Match>>> GetMatches()
        {
            // Include teams to return the names
            return await _context.Matches
                .Include(m => m.Team1)
                .Include(m => m.Team2)
                .Include(m => m.Server)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Match>> GetMatch(int id)
        {
            var match = await _context.Matches
                .Include(m => m.Team1)
                .Include(m => m.Team2)
                .Include(m => m.Server)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (match == null)
            {
                return NotFound();
            }

            return match;
        }

        [HttpPost]
        public async Task<ActionResult<Match>> CreateMatch(Match match)
        {
            _context.Matches.Add(match);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMatch), new { id = match.Id }, match);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMatch(int id, Match match)
        {
            if (id != match.Id)
            {
                return BadRequest();
            }

            _context.Entry(match).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MatchExists(id))
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
        public async Task<IActionResult> DeleteMatch(int id)
        {
            var match = await _context.Matches.FindAsync(id);
            if (match == null)
            {
                return NotFound();
            }

            _context.Matches.Remove(match);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool MatchExists(int id)
        {
            return _context.Matches.Any(e => e.Id == id);
        }
    }
}
