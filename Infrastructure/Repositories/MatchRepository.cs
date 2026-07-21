using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cs2Admin.API.Infrastructure.Repositories;

public class MatchRepository(ApplicationDbContext context) : Repository<Match>(context), IMatchRepository
{
    public async Task<IEnumerable<Match>> GetMatchesWithDetailsAsync()
    {
        return await DbSet
            .Include(m => m.Team1)
            .Include(m => m.Team2)
            .Include(m => m.Server)
            .ToListAsync();
    }

    public async Task<Match?> GetMatchWithDetailsAsync(int id)
    {
        return await DbSet
            .Include(m => m.Team1)
            .Include(m => m.Team2)
            .Include(m => m.Server)
            .FirstOrDefaultAsync(m => m.Id == id);
    }
}
