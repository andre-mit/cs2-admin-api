using System.Collections.Generic;
using System.Threading.Tasks;
using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Cs2Admin.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cs2Admin.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/steam-tokens")]
    public class SteamTokensController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ISteamTokenService _steamTokenService;

        public SteamTokensController(ApplicationDbContext context, ISteamTokenService steamTokenService)
        {
            _context = context;
            _steamTokenService = steamTokenService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SteamServerToken>>> GetTokens()
        {
            return await _context.SteamServerTokens.ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<SteamServerToken>> CreateToken([FromBody] SteamServerToken tokenRequest)
        {
            var tokenId = await _steamTokenService.CreateTokenAsync(tokenRequest.Memo, tokenRequest.Token, default);
            var createdToken = await _context.SteamServerTokens.FindAsync(tokenId);
            
            if (createdToken == null)
                return StatusCode(500, "Failed to create token.");

            return CreatedAtAction(nameof(GetTokens), new { id = createdToken.Id }, createdToken);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteToken(int id)
        {
            var token = await _context.SteamServerTokens.FindAsync(id);
            if (token == null)
            {
                return NotFound();
            }

            _context.SteamServerTokens.Remove(token);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
