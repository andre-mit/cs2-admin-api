using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cs2Admin.API.Infrastructure.Repositories;

public class MatchPlayerStatRepository(ApplicationDbContext context) : Repository<MatchPlayerStat>(context), IMatchPlayerStatRepository
{
    public async Task<MatchPlayerStat?> GetByMatchAndSteamIdAsync(int matchId, string steamId)
    {
        return await DbSet.FirstOrDefaultAsync(s => s.MatchId == matchId && s.SteamId == steamId);
    }
}
