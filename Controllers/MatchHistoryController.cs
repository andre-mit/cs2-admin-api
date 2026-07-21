using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cs2Admin.API.Services;

namespace Cs2Admin.API.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/history")]
public class MatchHistoryController(IMatchHistoryService historyService) : ControllerBase
{

    [HttpGet]
    public async Task<IActionResult> GetMatches([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var steamId = User.FindFirst("SteamId")?.Value;
        if (string.IsNullOrEmpty(steamId))
            return Unauthorized();

        var result = await historyService.GetMatchesAsync(steamId, page, pageSize);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetMatchDetail(int id)
    {
        var result = await historyService.GetMatchDetailAsync(id);
        return result != null ? Ok(result) : NotFound();
    }
}
