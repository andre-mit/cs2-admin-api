using Cs2Admin.API.Data;
using Cs2Admin.API.Models;

namespace Cs2Admin.API.Infrastructure.Repositories;

public class MatchEventLogRepository(ApplicationDbContext context) : Repository<MatchEventLog>(context), IMatchEventLogRepository
{
}
