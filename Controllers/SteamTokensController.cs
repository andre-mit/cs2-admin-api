using System.Collections.Generic;
using System.Threading.Tasks;
using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Cs2Admin.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Cs2Admin.API.Infrastructure.Repositories;
namespace Cs2Admin.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/steam-tokens")]
    public class SteamTokensController(
        ISteamTokenRepository steamTokenRepository,
        ISteamTokenService steamTokenService) : ControllerBase
    {

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SteamServerToken>>> GetTokens()
        {
            return Ok(await steamTokenRepository.GetAllAsync());
        }

        [HttpPost]
        public async Task<ActionResult<SteamServerToken>> CreateToken([FromBody] SteamServerToken tokenRequest)
        {
            var tokenId = await steamTokenService.CreateTokenAsync(tokenRequest.Memo, tokenRequest.Token, default);
            var createdToken = await steamTokenRepository.GetByIdAsync(tokenId);
            
            if (createdToken == null)
                return StatusCode(500, "Failed to create token.");

            return CreatedAtAction(nameof(GetTokens), new { id = createdToken.Id }, createdToken);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteToken(int id)
        {
            var token = await steamTokenRepository.GetByIdAsync(id);
            if (token == null)
            {
                return NotFound();
            }

            steamTokenRepository.Remove(token);
            await steamTokenRepository.SaveChangesAsync();

            return NoContent();
        }
    }
}
