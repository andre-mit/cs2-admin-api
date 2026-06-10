using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Cs2Admin.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Cs2Admin.API.Services;

public class SteamTokenService(ApplicationDbContext context) : ISteamTokenService
{
    public async Task<int> CreateTokenAsync(string memo, string token, CancellationToken ct)
    {
        var steamToken = new Models.SteamServerToken
        {
            Memo = memo,
            Token = token,
            IsAvailable = true
        };

        await context.SteamServerTokens.AddAsync(steamToken, ct);
        await context.SaveChangesAsync(ct);

        return steamToken.Id;
    }
    
    public async Task<int> AvailableTokensCountAsync(CancellationToken ct)
    {
        return await context.SteamServerTokens.CountAsync(t => t.IsAvailable, ct);
    }
    
    public async Task<bool> MarkTokenAsUsedAsync(int tokenId, CancellationToken ct)
    {
        var token = await context.SteamServerTokens.FindAsync([tokenId], ct);
        if (token is not { IsAvailable: true })
        {
            return false;
        }

        token.IsAvailable = false;
        await context.SaveChangesAsync(ct);
        return true;
    }
    
    public async Task<bool> MarkTokenAsAvailableAsync(int tokenId, CancellationToken ct)
    {
        var token = await context.SteamServerTokens.FindAsync([tokenId], cancellationToken: ct);
        if (token is not { IsAvailable: false })
        {
            return false;
        }

        token.IsAvailable = true;
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> MarkTokenAsAvailableByMemoAsync(string memo, CancellationToken ct)
    {
        var token = await context.SteamServerTokens.FirstOrDefaultAsync(t => t.Memo == memo, ct);
        if (token is not { IsAvailable: false })
        {
            return false;
        }

        token.IsAvailable = true;
        await context.SaveChangesAsync(ct);
        return true;
    }
    
    public async Task<SteamServerToken?> GetAvailableTokenAsync(CancellationToken ct)
    {
        return await context.SteamServerTokens.FirstOrDefaultAsync(t => t.IsAvailable, ct);
    }
}