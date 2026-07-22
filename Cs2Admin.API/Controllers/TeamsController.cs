using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.Authorization;

using Cs2Admin.API.Infrastructure.Repositories;
namespace Cs2Admin.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class TeamsController(ITeamRepository teamRepository) : ControllerBase
    {

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Team>>> GetTeams()
        {
            return Ok(await teamRepository.GetAllAsync());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Team>> GetTeam(int id)
        {
            var team = await teamRepository.GetByIdAsync(id);

            if (team == null)
            {
                return NotFound();
            }

            return team;
        }

        [HttpPost]
        public async Task<ActionResult<Team>> CreateTeam(Team team)
        {
            await teamRepository.AddAsync(team);
            await teamRepository.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTeam), new { id = team.Id }, team);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTeam(int id, Team team)
        {
            if (id != team.Id)
            {
                return BadRequest();
            }

            teamRepository.Update(team);

            try
            {
                await teamRepository.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await TeamExistsAsync(id))
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
        public async Task<IActionResult> DeleteTeam(int id)
        {
            var team = await teamRepository.GetByIdAsync(id);
            if (team == null)
            {
                return NotFound();
            }

            teamRepository.Remove(team);
            await teamRepository.SaveChangesAsync();

            return NoContent();
        }

        private async Task<bool> TeamExistsAsync(int id)
        {
            return await teamRepository.ExistsAsync(e => e.Id == id);
        }
    }
}
