using System.Threading.Tasks;

namespace Cs2Admin.API.Services.Interfaces
{
    public interface IMatchmakingService
    {
        Task EnqueuePartyAsync(string partyId, string mode, string[] steamIds);
        Task DequeuePartyAsync(string partyId, string mode);
        Task<bool> ReadyUpPlayerAsync(string matchSessionId, string steamId);
    }
}
