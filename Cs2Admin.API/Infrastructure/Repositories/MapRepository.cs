using Cs2Admin.API.Data;
using Cs2Admin.API.Models;

namespace Cs2Admin.API.Infrastructure.Repositories;

public class MapRepository(ApplicationDbContext context) : Repository<GameMap>(context), IMapRepository
{
}
