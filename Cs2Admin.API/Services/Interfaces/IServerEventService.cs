using System.Text.Json;

namespace Cs2Admin.API.Services.Interfaces;

public interface IServerEventService
{
    Task BroadcastEventAsync(string eventType, object data);
    Task RegisterClientAsync(HttpResponse response, CancellationToken cancellationToken);
}
