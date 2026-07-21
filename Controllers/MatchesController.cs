using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.Authorization;

using Mediator;
using Cs2Admin.API.Application.Matches.Queries;
using Cs2Admin.API.Application.Matches.Commands;

namespace Cs2Admin.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class MatchesController(IMediator mediator) : ControllerBase
    {

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Match>>> GetMatches()
        {
            var matches = await mediator.Send(new GetMatchesQuery());
            return Ok(matches);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Match>> GetMatch(int id)
        {
            var match = await mediator.Send(new GetMatchQuery(id));

            if (match == null)
            {
                return NotFound();
            }

            return match;
        }

        [HttpPost]
        public async Task<ActionResult<Match>> CreateMatch(Match match)
        {
            var createdMatch = await mediator.Send(new CreateMatchCommand(match));
            return CreatedAtAction(nameof(GetMatch), new { id = createdMatch.Id }, createdMatch);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMatch(int id, Match match)
        {
            var success = await mediator.Send(new UpdateMatchCommand(id, match));
            
            if (!success)
            {
                return BadRequest("Failed to update match. Check ID and data.");
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMatch(int id)
        {
            var success = await mediator.Send(new DeleteMatchCommand(id));
            if (!success)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
