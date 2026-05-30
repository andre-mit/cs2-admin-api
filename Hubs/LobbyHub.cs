using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Cs2Admin.API.Hubs
{
    public class LobbyHub : Hub
    {
        public async Task JoinLobbyGroup(string lobbyId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Lobby_{lobbyId}");
        }

        public async Task LeaveLobbyGroup(string lobbyId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Lobby_{lobbyId}");
        }
    }
}
