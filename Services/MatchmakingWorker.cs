using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cs2Admin.API.Hubs;
using Cs2Admin.API.Services.Interfaces;
using Cs2Admin.API.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Cs2Admin.API.Services
{
    public class MatchmakingWorker : BackgroundService, IMatchmakingService
    {
        private readonly ILogger<MatchmakingWorker> _logger;
        private readonly IConnectionMultiplexer _redis;
        private readonly IHubContext<LobbyHub> _hubContext;
        private readonly IServiceProvider _serviceProvider;
        private readonly string[] _modes = { "1v1", "2v2", "2v2w", "5v5" };

        public MatchmakingWorker(
            ILogger<MatchmakingWorker> logger,
            IConnectionMultiplexer redis,
            IHubContext<LobbyHub> hubContext,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _redis = redis;
            _hubContext = hubContext;
            _serviceProvider = serviceProvider;
        }

        public async Task EnqueuePartyAsync(string partyId, string mode, string[] steamIds)
        {
            var db = _redis.GetDatabase();
            var entry = JsonSerializer.Serialize(new { PartyId = partyId, SteamIds = steamIds, Mode = mode });
            await db.ListRightPushAsync($"queue:{mode}", entry);
            _logger.LogInformation("Party {PartyId} entered queue {Mode} with {Count} players.", partyId, mode, steamIds.Length);
        }

        public async Task DequeuePartyAsync(string partyId, string mode)
        {
            // O(N) removal, can be optimized but fine for now
            var db = _redis.GetDatabase();
            var queueKey = $"queue:{mode}";
            var items = await db.ListRangeAsync(queueKey);
            foreach (var item in items)
            {
                if (item.HasValue)
                {
                    var doc = JsonDocument.Parse(item.ToString());
                    if (doc.RootElement.GetProperty("PartyId").GetString() == partyId)
                    {
                        await db.ListRemoveAsync(queueKey, item, 1);
                        _logger.LogInformation("Party {PartyId} removed from queue {Mode}.", partyId, mode);
                        break;
                    }
                }
            }
        }

        public async Task<bool> ReadyUpPlayerAsync(string matchSessionId, string steamId)
        {
            var db = _redis.GetDatabase();
            var key = $"match_session:{matchSessionId}:ready";
            await db.SetAddAsync(key, steamId);
            
            // Check if all players are ready
            var allPlayersJson = await db.StringGetAsync($"match_session:{matchSessionId}:players");
            if (allPlayersJson.HasValue)
            {
                var allPlayers = JsonSerializer.Deserialize<string[]>(allPlayersJson.ToString());
                var readyPlayersCount = await db.SetLengthAsync(key);
                if (readyPlayersCount >= allPlayers?.Length)
                {
                    // Everyone is ready! Start server provisioning
                    _ = Task.Run(() => ProvisionMatchServer(matchSessionId, allPlayers));
                    return true;
                }
            }
            return false;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessQueuesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing matchmaking queues.");
                }

                await Task.Delay(5000, stoppingToken);
            }
        }

        private async Task ProcessQueuesAsync()
        {
            var db = _redis.GetDatabase();

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            foreach (var mode in _modes)
            {
                int requiredPlayers = mode switch
                {
                    "1v1" => 2,
                    "2v2" => 4,
                    "2v2w" => 4,
                    "5v5" => 10,
                    _ => 0
                };

                if (requiredPlayers == 0) continue;

                var queueKey = $"queue:{mode}";
                var queueLength = await db.ListLengthAsync(queueKey);
                if (queueLength == 0) continue;

                var items = await db.ListRangeAsync(queueKey);
                var candidates = new List<(string PartyId, string[] SteamIds, string Raw)>();

                foreach (var item in items)
                {
                    if (item.HasValue)
                    {
                        var json = item.ToString();
                        using var doc = JsonDocument.Parse(json);
                        var partyId = doc.RootElement.GetProperty("PartyId").GetString()!;
                        var steamIds = JsonSerializer.Deserialize<string[]>(doc.RootElement.GetProperty("SteamIds").GetRawText())!;
                        candidates.Add((partyId, steamIds, json));
                    }
                }

                var allCandidateSteamIds = candidates.SelectMany(c => c.SteamIds).Distinct().ToList();
                if (allCandidateSteamIds.Count < requiredPlayers) continue;

                var eloLookup = await dbContext.Users
                    .Where(u => allCandidateSteamIds.Contains(u.SteamId))
                    .ToDictionaryAsync(u => u.SteamId, u => u.Elo);

                foreach (var id in allCandidateSteamIds)
                {
                    if (!eloLookup.ContainsKey(id))
                        eloLookup[id] = 1000;
                }

                var sortedCandidates = candidates
                    .OrderBy(c => c.SteamIds.Average(s => eloLookup.GetValueOrDefault(s, 1000)))
                    .ToList();

                var matchGroup = new List<(string PartyId, string[] SteamIds, string Raw)>();
                int totalPlayers = 0;

                foreach (var candidate in sortedCandidates)
                {
                    if (matchGroup.Count == 0)
                    {
                        matchGroup.Add(candidate);
                        totalPlayers += candidate.SteamIds.Length;
                    }
                    else
                    {
                        double groupAvgElo = matchGroup
                            .SelectMany(c => c.SteamIds)
                            .Average(s => eloLookup.GetValueOrDefault(s, 1000));
                        double candidateAvgElo = candidate.SteamIds
                            .Average(s => eloLookup.GetValueOrDefault(s, 1000));

                        if (Math.Abs(groupAvgElo - candidateAvgElo) <= 300)
                        {
                            matchGroup.Add(candidate);
                            totalPlayers += candidate.SteamIds.Length;
                        }
                    }

                    if (totalPlayers >= requiredPlayers)
                    {
                        await FormMatchAsync(mode, matchGroup);
                        matchGroup = new();
                        totalPlayers = 0;
                    }
                }
            }
        }

        private async Task FormMatchAsync(string mode, List<(string PartyId, string[] SteamIds, string Raw)> parties)
        {
            var db = _redis.GetDatabase();
            var queueKey = $"queue:{mode}";

            foreach (var p in parties)
            {
                await db.ListRemoveAsync(queueKey, p.Raw, 1);
            }

            var matchSessionId = Guid.NewGuid().ToString();
            var allSteamIds = parties.SelectMany(p => p.SteamIds).ToArray();
            
            await db.StringSetAsync($"match_session:{matchSessionId}:players", JsonSerializer.Serialize(allSteamIds), TimeSpan.FromMinutes(5));
            await db.StringSetAsync($"match_session:{matchSessionId}:mode", mode, TimeSpan.FromMinutes(5));

            _logger.LogInformation("Match Session {SessionId} created for mode {Mode} with {Count} players.", matchSessionId, mode, allSteamIds.Length);

            foreach (var steamId in allSteamIds)
            {
                await _hubContext.Clients.Group($"Player_{steamId}").SendAsync("MatchFound", new { matchSessionId, mode });
            }
        }

        private async Task ProvisionMatchServer(string matchSessionId, string[] steamIds)
        {
            using var scope = _serviceProvider.CreateScope();
            var serverService = scope.ServiceProvider.GetRequiredService<IServerService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var modeJson = await _redis.GetDatabase().StringGetAsync($"match_session:{matchSessionId}:mode");
            var mode = modeJson.HasValue ? modeJson.ToString() : "5v5";

            _logger.LogInformation("Starting provisioning for Match Session {SessionId}", matchSessionId);

            var users = dbContext.Users.Where(u => steamIds.Contains(u.SteamId)).ToList();
            var playerElos = users.ToDictionary(u => u.SteamId, u => u.Elo);
            foreach (var id in steamIds)
            {
                if (!playerElos.ContainsKey(id))
                    playerElos[id] = 1000;
            }

            var (team1, team2) = EloService.BalanceTeams(playerElos);

            double team1Avg = team1.Average(s => playerElos[s]);
            double team2Avg = team2.Average(s => playerElos[s]);
            _logger.LogInformation("Teams balanced. Team1 avg Elo: {T1Avg:F0}, Team2 avg Elo: {T2Avg:F0}", team1Avg, team2Avg);

            // Create match record
            var match = new Cs2Admin.API.Models.Match
            {
                CreatedAt = DateTime.UtcNow,
                MaxMaps = 1,
                SkipVeto = false // Or dynamic based on mode
            };
            dbContext.Matches.Add(match);
            await dbContext.SaveChangesAsync();

            // Notify clients that server is starting
            foreach (var steamId in steamIds)
            {
                await _hubContext.Clients.Group($"Player_{steamId}").SendAsync("MatchStarting", new { matchId = match.Id });
            }

            // Build MatchZy JSON override
            var matchZyOverride = new
            {
                matchid = match.Id.ToString(),
                num_maps = 1,
                players_per_team = steamIds.Length / 2,
                team1 = new {
                    name = "Team 1",
                    players = team1.ToDictionary(s => s, s => users.FirstOrDefault(u => u.SteamId == s)?.InternalNick ?? s)
                },
                team2 = new {
                    name = "Team 2",
                    players = team2.ToDictionary(s => s, s => users.FirstOrDefault(u => u.SteamId == s)?.InternalNick ?? s)
                }
            };

            var matchZyOverrideJson = JsonSerializer.Serialize(matchZyOverride);

            // Assume MatchZy is GamePluginId = 1
            var matchZyPlugin = dbContext.GamePlugins.FirstOrDefault(p => p.Name.ToLower().Contains("matchzy"));
            var plugins = new List<Cs2Admin.API.ViewModels.PluginSelectionItem>();
            if (matchZyPlugin != null)
            {
                plugins.Add(new Cs2Admin.API.ViewModels.PluginSelectionItem
                {
                    PluginId = matchZyPlugin.Id,
                    ConfigOverridesJson = matchZyOverrideJson
                });
            }

            var request = new Cs2Admin.API.ViewModels.ServerRequest
            {
                Name = $"Match {match.Id} - {mode}",
                MaxPlayers = (byte)(steamIds.Length + 2), // +2 for GOTV/Spectators
                PluginSelections = plugins,
                ServerVariables = new Dictionary<string, string>
                {
                    { "matchzy_demo_upload_url", "http://host.docker.internal:5000/api/v1/matchzy/demo" },
                    { "matchzy_remote_log_url", "http://host.docker.internal:5000/api/v1/matchzy/events" },
                    { "matchzy_remote_log_header_key", "MatchZy-MatchId" },
                    { "matchzy_remote_log_header_value", match.Id.ToString() }
                }
            };

            try
            {
                var server = await serverService.CreateServerAsync(request, CancellationToken.None);
                
                // Update match with ServerId
                match.ServerId = server.Id;
                await dbContext.SaveChangesAsync();

                _logger.LogInformation("Match {MatchId} server provisioned on port {Port}.", match.Id, server.Port);

                var connectString = $"steam://connect/{server.IpAddress}:{server.Port}";
                foreach (var steamId in steamIds)
                {
                    await _hubContext.Clients.Group($"Player_{steamId}").SendAsync("ServerReady", new { connectString });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to provision server for Match {MatchId}.", match.Id);
                // Broadcast error
                foreach (var steamId in steamIds)
                {
                    await _hubContext.Clients.Group($"Player_{steamId}").SendAsync("MatchError", new { message = "Failed to start server." });
                }
            }
        }
    }
}
