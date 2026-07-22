using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cs2Admin.API.Services;

public interface IMatchHistoryService
{
    Task<object> GetMatchesAsync(string steamId, int page, int pageSize);
    Task<object?> GetMatchDetailAsync(int matchId);
}

public class MatchHistoryService : IMatchHistoryService
{
    private readonly ApplicationDbContext _context;

    public MatchHistoryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<object> GetMatchesAsync(string steamId, int page, int pageSize)
    {
        var matchIds = await _context.MatchPlayerStats
            .Where(s => s.SteamId == steamId)
            .Select(s => s.MatchId)
            .Distinct()
            .ToListAsync();

        var matches = await _context.Matches
            .Where(m => matchIds.Contains(m.Id))
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.CreatedAt,
                m.StartTime,
                m.EndTime,
                m.Winner,
                m.DemoUrl,
                m.MaxMaps,
                m.MapStat
            })
            .ToListAsync();

        var totalCount = matchIds.Count;

        return new
        {
            matches,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<object?> GetMatchDetailAsync(int matchId)
    {
        var match = await _context.Matches
            .Include(m => m.Team1)
            .Include(m => m.Team2)
            .FirstOrDefaultAsync(m => m.Id == matchId);

        if (match == null) return null;

        var playerStats = await _context.MatchPlayerStats
            .Where(s => s.MatchId == matchId)
            .ToListAsync();

        var timeline = await _context.MatchRoundTimelines
            .Where(t => t.MatchId == matchId)
            .OrderBy(t => t.RoundNumber)
            .ThenBy(t => t.Timestamp)
            .ToListAsync();

        var eventLogs = await _context.MatchEventLogs
            .Where(e => e.MatchId == matchId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync();

        var rounds = timeline
            .GroupBy(t => t.RoundNumber)
            .Select(g => new
            {
                Round = g.Key,
                Events = g.Select(e => new
                {
                    e.EventType,
                    e.ActorSteamId,
                    e.TargetSteamId,
                    e.Weapon,
                    e.Headshot,
                    e.Timestamp
                })
            })
            .ToList();

        return new
        {
            match = new
            {
                match.Id,
                match.CreatedAt,
                match.StartTime,
                match.EndTime,
                match.Winner,
                match.DemoUrl,
                match.MaxMaps,
                match.MapStat,
                Team1Name = match.Team1?.Name,
                Team2Name = match.Team2?.Name
            },
            playerStats = playerStats.Select(s => new
            {
                s.SteamId,
                PlayerName = s.Name,
                TeamNumber = s.Team == "Team1" || s.Team == "1" ? 1 : 2,
                s.Kills,
                s.Deaths,
                s.Assists,
                s.Damage,
                HeadshotPercentage = s.Kills > 0 ? (double)s.HeadshotKills / s.Kills * 100.0 : 0.0,
                Adr = 0.0,
                Rating = s.Score > 0 ? (double)s.Score / 100.0 : 0.0
            }),
            rounds,
            rawEvents = eventLogs.Select(e => new
            {
                e.EventType,
                e.Timestamp,
                e.RawEventData
            })
        };
    }
}
