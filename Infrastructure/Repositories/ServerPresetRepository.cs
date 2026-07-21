using Cs2Admin.API.Data;
using Cs2Admin.API.Models;

namespace Cs2Admin.API.Infrastructure.Repositories;

public class ServerPresetRepository(ApplicationDbContext context) : Repository<ServerPreset>(context), IServerPresetRepository
{
}
