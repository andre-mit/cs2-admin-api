using Cs2Admin.API.Models;

namespace Cs2Admin.API.Infrastructure.Repositories;

public interface IServerRepository : IRepository<Server>
{
    Task<IEnumerable<Server>> GetServersOrderByDescendingAsync();
}
