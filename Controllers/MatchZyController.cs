using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Cs2Admin.API.Services;

namespace Cs2Admin.API.Controllers;

[ApiController]
[Route("api/v1/matchzy")]
public class MatchZyController(IMatchZyService matchZyService, ILogger<MatchZyController> logger) : ControllerBase
{
    [HttpPost("events")]
    public async Task<IActionResult> ReceiveEvent([FromBody] JsonElement payload)
    {
        try
        {
            await matchZyService.ProcessEventAsync(payload);
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process MatchZy event.");
            return BadRequest();
        }
    }

    [HttpPost("demo")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> UploadDemo()
    {
        if (!Request.HasFormContentType)
            return BadRequest("Expected form content type.");

        var matchIdStr = Request.Headers["MatchZy-MatchId"].ToString();
        var fileName = Request.Headers["MatchZy-FileName"].ToString();
        var file = Request.Form.Files.FirstOrDefault();

        if (file == null || file.Length == 0)
            return BadRequest("No demo file provided.");

        if (!int.TryParse(matchIdStr, out int matchId))
            return BadRequest("Invalid MatchId.");

        using var stream = file.OpenReadStream();
        var result = await matchZyService.UploadDemoAsync(matchId, fileName, stream, file.ContentType);

        return result != null
            ? Ok(new { success = true })
            : NotFound("Match not found.");
    }
}
