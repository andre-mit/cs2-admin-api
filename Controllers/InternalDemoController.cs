using Microsoft.AspNetCore.Mvc;
using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using Cs2Admin.API.Infrastructure.Repositories;

namespace Cs2Admin.API.Controllers;

[ApiController]
[Route("api/internal/demo-results")]
public class InternalDemoController(
    IMatchRepository matchRepository,
    IMatchRoundTimelineRepository timelineRepository,
    ILogger<InternalDemoController> logger) : ControllerBase
{
    [HttpPost("{matchId:int}")]
    public async Task<IActionResult> ReceiveDemoResults(int matchId, [FromBody] DemoResultPayload payload)
    {
        var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expectedKey = configuration["InternalApiKey"];
        
        if (!string.IsNullOrEmpty(expectedKey))
        {
            if (!Request.Headers.TryGetValue("X-Internal-Api-Key", out var providedKey) || providedKey != expectedKey)
            {
                logger.LogWarning("Unauthorized access attempt to InternalDemoController");
                return Unauthorized("Invalid API Key");
            }
        }
        
        if (payload.MatchId != matchId)
            return BadRequest("MatchId mismatch");

        var match = await matchRepository.GetByIdAsync(matchId);
        if (match == null) return NotFound("Match not found");

        var newTimelines = new List<MatchRoundTimeline>();

        foreach (var ev in payload.Events)
        {
            newTimelines.Add(new MatchRoundTimeline
            {
                MatchId = matchId,
                RoundNumber = ev.RoundNumber,
                EventType = ev.EventType,
                ActorSteamId = ev.AttackerSteamId,
                TargetSteamId = ev.VictimSteamId,
                Weapon = ev.Weapon,
                Headshot = ev.Headshot,
                Timestamp = ev.Timestamp
            });
        }

        if (newTimelines.Count > 0)
        {
            // Remove any rudimentary round_end records from MatchZy to avoid clutter, 
            // or we keep them to denote round ends alongside kills. 
            // For now, we just add the detailed kills.
            await timelineRepository.AddRangeAsync(newTimelines);
            await timelineRepository.SaveChangesAsync();
            logger.LogInformation("Saved {EventCount} detailed events for Match {MatchId} from Go Parser", newTimelines.Count, matchId);
        }

        return Ok();
    }
}

public class DemoResultPayload
{
    [Required]
    public int MatchId { get; set; }
    
    [Required]
    public List<EventLog> Events { get; set; } = new();
}

public class EventLog
{
    public string EventType { get; set; } = "";
    public int RoundNumber { get; set; }
    public string? AttackerSteamId { get; set; }
    public string? AttackerName { get; set; }
    public string? VictimSteamId { get; set; }
    public string? VictimName { get; set; }
    public string? Weapon { get; set; }
    public bool Headshot { get; set; }
    public DateTime Timestamp { get; set; }
}
