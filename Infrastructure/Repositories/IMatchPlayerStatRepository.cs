using Cs2Admin.API.Models;

namespace Cs2Admin.API.Infrastructure.Repositories;

public interface IMatchPlayerStatRepository : IRepository<MatchPlayerStat>
{
    Task<MatchPlayerStat?> GetByMatchAndSteamIdAsync(int matchId, string steamId);
}
