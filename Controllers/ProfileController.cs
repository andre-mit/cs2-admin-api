using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cs2Admin.API.Services;

namespace Cs2Admin.API.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/profile")]
public class ProfileController(IProfileService profileService) : ControllerBase
{

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var steamId = User.FindFirst("SteamId")?.Value;
        if (string.IsNullOrEmpty(steamId))
            return Unauthorized();

        var profile = await profileService.GetProfileAsync(steamId);
        return profile != null ? Ok(profile) : NotFound();
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var steamId = User.FindFirst("SteamId")?.Value;
        if (string.IsNullOrEmpty(steamId))
            return Unauthorized();

        if (request.InternalNick != null && request.InternalNick.Length > 100)
            return BadRequest("Nickname must be at most 100 characters.");

        var result = await profileService.UpdateProfileAsync(steamId, request.InternalNick);
        return result != null ? Ok(result) : NotFound();
    }
}

public class UpdateProfileRequest
{
    public string? InternalNick { get; set; }
}
