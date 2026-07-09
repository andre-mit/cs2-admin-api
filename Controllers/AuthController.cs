using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace Cs2Admin.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet("steam")]
    public IActionResult SteamLogin([FromQuery] string? returnUrl = null)
    {
        var redirectUrl = Url.Action("SteamCallback", "Auth", new { returnUrl });
        return Challenge(new AuthenticationProperties { RedirectUri = redirectUrl }, "Steam");
    }

    [HttpGet("steam/callback")]
    public async Task<IActionResult> SteamCallback([FromQuery] string? returnUrl = null)
    {
        var result = await HttpContext.AuthenticateAsync("TempCookie");

        if (!result.Succeeded)
        {
            return BadRequest("Steam authentication failed.");
        }

        var steamIdClaim = result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(steamIdClaim))
        {
            return BadRequest("Steam ID not found.");
        }

        // Steam NameIdentifier looks like "https://steamcommunity.com/openid/id/76561198000000000"
        var steamId = steamIdClaim.Split('/').LastOrDefault();
        if (string.IsNullOrEmpty(steamId))
        {
            return BadRequest("Invalid Steam ID format.");
        }

        var nameClaim = result.Principal.FindFirst(ClaimTypes.Name)?.Value ?? $"Player_{steamId.Substring(steamId.Length - 4)}";

        // Create or update user
        var user = await _context.Users.FirstOrDefaultAsync(u => u.SteamId == steamId);
        if (user == null)
        {
            user = new User
            {
                SteamId = steamId,
                InternalNick = nameClaim,
                RegisteredAt = DateTime.UtcNow,
                Elo = 1000
            };
            _context.Users.Add(user);
        }
        else
        {
            // Optionally update AvatarUrl if we can fetch it (OpenID might not provide it directly without Steam API key).
            // Steam OpenID provides some basic claims, but Steam Web API is better for avatars. We leave it for now.
        }

        await _context.SaveChangesAsync();

        // Sign out of the temporary cookie
        await HttpContext.SignOutAsync("TempCookie");

        // Generate JWT
        var token = GenerateJwtToken(user);

        var frontendUrl = _configuration["FrontendUrl"] ?? "http://localhost:3000";
        var finalRedirect = string.IsNullOrEmpty(returnUrl) ? $"{frontendUrl}/auth/success?token={token}" : $"{frontendUrl}{returnUrl}?token={token}";

        return Redirect(finalRedirect);
    }

    private string GenerateJwtToken(User user)
    {
        var secretKey = _configuration["JwtSettings:SecretKey"] ?? "MySuperSecretKeyForDevelopmentOnly123!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.SteamId),
            new Claim("SteamId", user.SteamId),
            new Claim("InternalNick", user.InternalNick),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
