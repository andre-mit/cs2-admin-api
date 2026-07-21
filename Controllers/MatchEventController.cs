using System;
using System.Text.Json;
using System.Threading.Tasks;
using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Cs2Admin.API.Infrastructure.Repositories;
namespace Cs2Admin.API.Controllers
{
    [ApiController]
    [Route("api/v1/match")]
    public class MatchEventController(
        ILogger<MatchEventController> logger,
        IMatchEventLogRepository matchEventLogRepository,
        IMatchPlayerStatRepository matchPlayerStatRepository) : ControllerBase
    {

        [HttpPost("event")]
        public async Task<IActionResult> HandleMatchEvent([FromBody] JsonElement payload)
        {
            try
            {
                var eventData = payload.GetProperty("event").GetString();
                var matchIdStr = payload.GetProperty("matchid").GetString();
                int matchId = int.TryParse(matchIdStr, out var id) ? id : 0;

                logger.LogInformation("Received match event '{Event}' for match {MatchId}", eventData, matchId);

                // 1. Log the raw event for debugging or future processing
                var log = new MatchEventLog
                {
                    MatchId = matchId,
                    EventType = eventData ?? "unknown",
                    RawEventData = payload.GetRawText()
                };
                await matchEventLogRepository.AddAsync(log);

                // 2. Parse specific events
                if (eventData == "player_death")
                {
                    await HandlePlayerDeath(matchId, payload);
                }
                
                await matchEventLogRepository.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse or handle match event");
                return BadRequest(new { success = false, error = "Invalid payload" });
            }
        }

        private async Task HandlePlayerDeath(int matchId, JsonElement payload)
        {
            if (!payload.TryGetProperty("params", out var p)) return;

            // Handle Attacker (Kills / Headshots)
            if (p.TryGetProperty("attacker", out var attacker))
            {
                var steamId = attacker.GetProperty("steamid64").GetString();
                var name = attacker.GetProperty("name").GetString();
                var headshot = p.TryGetProperty("headshot", out var hs) && hs.GetBoolean();

                if (!string.IsNullOrEmpty(steamId))
                {
                    var stat = await GetOrCreatePlayerStat(matchId, steamId, name ?? "Unknown");
                    stat.Kills++;
                    if (headshot) stat.HeadshotKills++;
                }
            }

            // Handle Victim (Deaths)
            if (p.TryGetProperty("victim", out var victim))
            {
                var steamId = victim.GetProperty("steamid64").GetString();
                var name = victim.GetProperty("name").GetString();

                if (!string.IsNullOrEmpty(steamId))
                {
                    var stat = await GetOrCreatePlayerStat(matchId, steamId, name ?? "Unknown");
                    stat.Deaths++;
                }
            }

            // Handle Assist
            if (p.TryGetProperty("assist", out var assist))
            {
                var steamId = assist.GetProperty("steamid64").GetString();
                var name = assist.GetProperty("name").GetString();

                if (!string.IsNullOrEmpty(steamId))
                {
                    var stat = await GetOrCreatePlayerStat(matchId, steamId, name ?? "Unknown");
                    stat.Assists++;
                }
            }
        }

        private async Task<MatchPlayerStat> GetOrCreatePlayerStat(int matchId, string steamId, string name)
        {
            var stat = await matchPlayerStatRepository.GetByMatchAndSteamIdAsync(matchId, steamId);

            if (stat == null)
            {
                stat = new MatchPlayerStat
                {
                    MatchId = matchId,
                    SteamId = steamId,
                    Name = name
                };
                await matchPlayerStatRepository.AddAsync(stat);
            }

            return stat;
        }
    }
}
