using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cs2Admin.API.Services;

public interface IProfileService
{
    Task<object?> GetProfileAsync(string steamId);
    Task<object?> UpdateProfileAsync(string steamId, string? newNick);
}

public class ProfileService : IProfileService
{
    private readonly ApplicationDbContext _context;

    public ProfileService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<object?> GetProfileAsync(string steamId)
    {
        var user = await _context.Users.FindAsync(steamId);
        if (user == null) return null;

        return new
        {
            user.SteamId,
            user.InternalNick,
            user.AvatarUrl,
            user.Elo,
            user.RegisteredAt
        };
    }

    public async Task<object?> UpdateProfileAsync(string steamId, string? newNick)
    {
        var user = await _context.Users.FindAsync(steamId);
        if (user == null) return null;

        if (!string.IsNullOrWhiteSpace(newNick) && newNick.Length <= 100)
        {
            user.InternalNick = newNick.Trim();
        }

        await _context.SaveChangesAsync();

        return new
        {
            user.SteamId,
            user.InternalNick,
            user.AvatarUrl,
            user.Elo,
            user.RegisteredAt
        };
    }
}
