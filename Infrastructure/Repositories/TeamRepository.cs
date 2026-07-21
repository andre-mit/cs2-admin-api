using Cs2Admin.API.Data;
using Cs2Admin.API.Models;

namespace Cs2Admin.API.Infrastructure.Repositories;

public class TeamRepository(ApplicationDbContext context) : Repository<Team>(context), ITeamRepository
{
}
