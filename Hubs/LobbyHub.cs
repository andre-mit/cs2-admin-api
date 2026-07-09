using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Cs2Admin.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System;

namespace Cs2Admin.API.Hubs
{
    [Authorize]
    public class LobbyHub : Hub
    {
        private readonly IMatchmakingService _matchmakingService;

        public LobbyHub(IMatchmakingService matchmakingService)
        {
            _matchmakingService = matchmakingService;
        }

        public override async Task OnConnectedAsync()
        {
            var steamId = Context.User?.FindFirst("SteamId")?.Value;
            if (!string.IsNullOrEmpty(steamId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Player_{steamId}");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var steamId = Context.User?.FindFirst("SteamId")?.Value;
            if (!string.IsNullOrEmpty(steamId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Player_{steamId}");
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinLobbyGroup(string lobbyId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Lobby_{lobbyId}");
        }

        public async Task LeaveLobbyGroup(string lobbyId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Lobby_{lobbyId}");
        }

        public async Task SearchMatch(string lobbyId, string mode, string[] steamIds)
        {
            // Usually we'd verify the caller is the lobby leader and all players are ready.
            await _matchmakingService.EnqueuePartyAsync(lobbyId, mode, steamIds);
            
            // Broadcast to the lobby that they are in queue
            await Clients.Group($"Lobby_{lobbyId}").SendAsync("QueueStarted", mode);
        }

        public async Task CancelSearch(string lobbyId, string mode)
        {
            await _matchmakingService.DequeuePartyAsync(lobbyId, mode);
            await Clients.Group($"Lobby_{lobbyId}").SendAsync("QueueCancelled");
        }

        public async Task ReadyUp(string matchSessionId)
        {
            var steamId = Context.User?.FindFirst("SteamId")?.Value;
            if (string.IsNullOrEmpty(steamId)) return;

            await _matchmakingService.ReadyUpPlayerAsync(matchSessionId, steamId);
            // Optionally, tell the specific player their ready state is acknowledged
            await Clients.Caller.SendAsync("ReadyAcknowledged");
        }
    }
}
