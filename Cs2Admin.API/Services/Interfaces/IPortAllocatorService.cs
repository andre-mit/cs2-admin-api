using Cs2Admin.API.ViewModels;

namespace Cs2Admin.API.Services.Interfaces;

public interface IPortAllocatorService
{
    Task<AllocatedPortResult> AllocateAvailablePortPairAsync(CancellationToken cancellationToken);
}