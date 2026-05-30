using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Cs2Admin.API.Data;
using Cs2Admin.API.Hubs;
using Cs2Admin.API.Models;
using System.Text.Json;
using System.Linq;

namespace Cs2Admin.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class LobbiesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<LobbyHub> _hubContext;

        public LobbiesController(ApplicationDbContext context, IHubContext<LobbyHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Lobby>>> GetLobbies()
        {
            return await _context.Lobbies.Include(l => l.Players).OrderByDescending(l => l.CreatedAt).ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Lobby>> GetLobby(int id)
        {
            var lobby = await _context.Lobbies.Include(l => l.Players).FirstOrDefaultAsync(l => l.Id == id);
            if (lobby == null) return NotFound();
            return lobby;
        }

        [HttpPost]
        public async Task<ActionResult<Lobby>> CreateLobby([FromBody] Lobby lobby)
        {
            _context.Lobbies.Add(lobby);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetLobby), new { id = lobby.Id }, lobby);
        }

        public class JoinRequest
        {
            public string SteamId { get; set; } = "";
            public string Name { get; set; } = "";
            public string AvatarUrl { get; set; } = "";
            public int TeamDesignation { get; set; }
        }

        [HttpPost("{id}/join")]
        public async Task<IActionResult> JoinLobby(int id, [FromBody] JoinRequest req)
        {
            var lobby = await _context.Lobbies.Include(l => l.Players).FirstOrDefaultAsync(l => l.Id == id);
            if (lobby == null) return NotFound();

            var player = lobby.Players.FirstOrDefault(p => p.SteamId == req.SteamId);
            if (player != null)
            {
                player.TeamDesignation = req.TeamDesignation;
                player.Name = req.Name;
            }
            else
            {
                lobby.Players.Add(new LobbyPlayer
                {
                    SteamId = req.SteamId,
                    Name = req.Name,
                    AvatarUrl = req.AvatarUrl,
                    TeamDesignation = req.TeamDesignation,
                    IsCaptain = false 
                });
            }

            await _context.SaveChangesAsync();
            await _hubContext.Clients.Group($"Lobby_{id}").SendAsync("LobbyUpdated", lobby);
            return Ok(lobby);
        }

        [HttpPost("{id}/randomize")]
        public async Task<IActionResult> RandomizeTeams(int id)
        {
            var lobby = await _context.Lobbies.Include(l => l.Players).FirstOrDefaultAsync(l => l.Id == id);
            if (lobby == null) return NotFound();

            var activePlayers = lobby.Players.Where(p => p.TeamDesignation != 0).ToList();
            var random = new Random();
            var shuffled = activePlayers.OrderBy(x => random.Next()).ToList();

            for (int i = 0; i < shuffled.Count; i++)
            {
                shuffled[i].TeamDesignation = (i % 2) + 1;
            }

            await _context.SaveChangesAsync();
            await _hubContext.Clients.Group($"Lobby_{id}").SendAsync("LobbyUpdated", lobby);
            return Ok(lobby);
        }

        public class StateRequest { public string State { get; set; } = ""; }

        [HttpPost("{id}/state")]
        public async Task<IActionResult> SetState(int id, [FromBody] StateRequest req)
        {
            var lobby = await _context.Lobbies.Include(l => l.Players).FirstOrDefaultAsync(l => l.Id == id);
            if (lobby == null) return NotFound();

            lobby.State = req.State;
            await _context.SaveChangesAsync();
            await _hubContext.Clients.Group($"Lobby_{id}").SendAsync("LobbyUpdated", lobby);
            return Ok(lobby);
        }
        
        public class VetoRequest { public string Map { get; set; } = ""; public string Action { get; set; } = ""; }

        [HttpPost("{id}/veto")]
        public async Task<IActionResult> VetoMap(int id, [FromBody] VetoRequest req)
        {
            var lobby = await _context.Lobbies.Include(l => l.Players).FirstOrDefaultAsync(l => l.Id == id);
            if (lobby == null) return NotFound();

            var history = JsonSerializer.Deserialize<List<string>>(lobby.VetoHistory) ?? new List<string>();
            var selected = JsonSerializer.Deserialize<List<string>>(lobby.SelectedMaps) ?? new List<string>();

            history.Add($"{req.Action}:{req.Map}");
            if (req.Action == "pick") {
                selected.Add(req.Map);
            }

            // Auto-decider: if exactly 1 map is left, pick it automatically
            var mapPoolList = lobby.MapPool.Split(',').ToList();
            if (mapPoolList.Count - history.Count == 1) 
            {
                var usedMaps = history.Select(h => h.Split(':')[1]).ToList();
                var remaining = mapPoolList.Except(usedMaps).FirstOrDefault();
                if (remaining != null) 
                {
                    history.Add($"pick:{remaining}");
                    selected.Add(remaining);
                }
            }

            lobby.VetoHistory = JsonSerializer.Serialize(history);
            lobby.SelectedMaps = JsonSerializer.Serialize(selected);

            await _context.SaveChangesAsync();
            await _hubContext.Clients.Group($"Lobby_{id}").SendAsync("LobbyUpdated", lobby);
            return Ok(lobby);
        }
        
        [HttpPost("{id}/generate")]
        public async Task<IActionResult> GenerateMatch(int id)
        {
            var lobby = await _context.Lobbies.Include(l => l.Players).FirstOrDefaultAsync(l => l.Id == id);
            if (lobby == null) return NotFound();

            var team1Players = lobby.Players.Where(p => p.TeamDesignation == 1).ToDictionary(p => p.SteamId, p => p.Name);
            var team2Players = lobby.Players.Where(p => p.TeamDesignation == 2).ToDictionary(p => p.SteamId, p => p.Name);
            var specs = lobby.Players.Where(p => p.TeamDesignation == 0).ToDictionary(p => p.SteamId, p => p.Name);

            var selected = JsonSerializer.Deserialize<List<string>>(lobby.SelectedMaps) ?? new List<string>();
            var mapSides = new List<string>();
            for(int i=0; i<selected.Count; i++) {
                mapSides.Add(lobby.MapSidesMode); // e.g. "knife"
            }

            var matchConfig = new
            {
                matchid = lobby.Id,
                team1 = new { name = "Team A", players = team1Players },
                team2 = new { name = "Team B", players = team2Players },
                num_maps = lobby.MaxMaps,
                maplist = selected,
                map_sides = mapSides,
                spectators = new { players = specs },
                clinch_series = true,
                players_per_team = 5,
                cvars = new { hostname = $"MatchZy: {lobby.Title}" }
            };

            lobby.GeneratedJson = JsonSerializer.Serialize(matchConfig);
            lobby.State = "Ready";
            await _context.SaveChangesAsync();
            await _hubContext.Clients.Group($"Lobby_{id}").SendAsync("LobbyUpdated", lobby);

            return Ok(matchConfig);
        }

        [HttpGet("{id}/config.json")]
        public async Task<IActionResult> GetConfig(int id)
        {
            var lobby = await _context.Lobbies.FirstOrDefaultAsync(l => l.Id == id);
            if (lobby == null || string.IsNullOrEmpty(lobby.GeneratedJson)) return NotFound();

            var json = JsonSerializer.Deserialize<object>(lobby.GeneratedJson);
            return Ok(json);
        }
    }
}
