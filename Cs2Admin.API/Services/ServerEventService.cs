using System.Collections.Concurrent;
using System.Text.Json;
using Cs2Admin.API.Services.Interfaces;

namespace Cs2Admin.API.Services;

public class ServerEventService : IServerEventService
{
    private readonly ConcurrentDictionary<Guid, HttpResponse> _clients = new();

    public async Task BroadcastEventAsync(string eventType, object data)
    {
        var message = $"event: {eventType}\ndata: {JsonSerializer.Serialize(data)}\n\n";
        var bytes = System.Text.Encoding.UTF8.GetBytes(message);

        foreach (var client in _clients)
        {
            try
            {
                await client.Value.Body.WriteAsync(bytes, 0, bytes.Length);
                await client.Value.Body.FlushAsync();
            }
            catch
            {
                // If writing fails (e.g. client disconnected), remove the client
                _clients.TryRemove(client.Key, out _);
            }
        }
    }

    public async Task RegisterClientAsync(HttpResponse response, CancellationToken cancellationToken)
    {
        var clientId = Guid.NewGuid();
        
        response.Headers.Append("Content-Type", "text/event-stream");
        response.Headers.Append("Cache-Control", "no-cache");
        response.Headers.Append("Connection", "keep-alive");

        _clients.TryAdd(clientId, response);

        try
        {
            // Keep connection open until cancelled
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            // Expected when client disconnects
        }
        finally
        {
            _clients.TryRemove(clientId, out _);
        }
    }
}
