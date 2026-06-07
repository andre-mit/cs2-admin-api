using Cs2Admin.API.ViewModels;

namespace Cs2Admin.API.Services.Interfaces;

public interface IServerService
{
    Task<ServerResult> CreateServerAsync(ServerRequest serverRequest, CancellationToken cancellationToken = default);
    Task StartServerAsync(string instanceId, CancellationToken cancellationToken = default);
    Task StopServerAsync(string instanceId, CancellationToken cancellationToken = default);
    Task RestartServerAsync(string instanceId, CancellationToken cancellationToken = default);
    Task DeleteServerAsync(string containerId, CancellationToken cancellationToken = default);
}