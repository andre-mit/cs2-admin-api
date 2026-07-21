using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cs2Admin.API.Infrastructure.Repositories;

public class ServerRepository(ApplicationDbContext context) : Repository<Server>(context), IServerRepository
{
    public async Task<IEnumerable<Server>> GetServersOrderByDescendingAsync()
    {
        return await DbSet.OrderByDescending(s => s.CreatedAt).ToListAsync();
    }
}
