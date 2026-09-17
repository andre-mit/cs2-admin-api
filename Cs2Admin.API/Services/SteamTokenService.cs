using System.Text.RegularExpressions;
using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Cs2Admin.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Cs2Admin.API.Services;

public class SteamTokenService(ApplicationDbContext context) : ISteamTokenService
{
    public static string SanitizeMemo(string? memo)
    {
        if (string.IsNullOrWhiteSpace(memo)) return "instance";
        var sanitized = Regex.Replace(memo.Trim(), @"[^a-zA-Z0-9_.-]", "-");
        sanitized = Regex.Replace(sanitized, @"-+", "-");
        sanitized = sanitized.Trim('-', '.', '_');
        return string.IsNullOrEmpty(sanitized) ? "instance" : sanitized;
    }

    public async Task<int> CreateTokenAsync(string memo, string token, CancellationToken ct)
    {
        var safeMemo = SanitizeMemo(memo);
        var steamToken = new Models.SteamServerToken
        {
            Memo = safeMemo,
            Token = token.Trim(),
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
        var safeMemo = SanitizeMemo(memo);
        var token = await context.SteamServerTokens.FirstOrDefaultAsync(t => t.Memo == memo || t.Memo == safeMemo, ct);
        if (token == null)
        {
            var allTokens = await context.SteamServerTokens.ToListAsync(ct);
            token = allTokens.FirstOrDefault(t => SanitizeMemo(t.Memo) == safeMemo);
        }

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
        var token = await context.SteamServerTokens.FirstOrDefaultAsync(t => t.IsAvailable, ct);
        if (token != null)
        {
            var sanitized = SanitizeMemo(token.Memo);
            if (token.Memo != sanitized)
            {
                token.Memo = sanitized;
                await context.SaveChangesAsync(ct);
            }
        }
        return token;
    }
}
