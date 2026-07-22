using Cs2Admin.API.Models;

namespace Cs2Admin.API.Infrastructure.Repositories;

public interface IMatchRepository : IRepository<Match>
{
    Task<IEnumerable<Match>> GetMatchesWithDetailsAsync();
    Task<Match?> GetMatchWithDetailsAsync(int id);
}
