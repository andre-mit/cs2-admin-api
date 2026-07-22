using System.Text.Json;
using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Amazon.S3;
using Amazon.S3.Transfer;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;

namespace Cs2Admin.API.Services;

public interface IMatchZyService
{
    Task ProcessEventAsync(JsonElement payload);
    Task<string?> UploadDemoAsync(int matchId, string fileName, Stream fileStream, string contentType);
}

public class MatchZyService : IMatchZyService
{
    private readonly ApplicationDbContext _context;
    private readonly IAmazonS3 _s3Client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MatchZyService> _logger;

    public MatchZyService(
        ApplicationDbContext context,
        IAmazonS3 s3Client,
        IConfiguration configuration,
        ILogger<MatchZyService> logger)
    {
        _context = context;
        _s3Client = s3Client;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ProcessEventAsync(JsonElement payload)
    {
        var eventType = payload.GetProperty("event").GetString();
        var matchIdStr = payload.TryGetProperty("matchid", out var mid)
            ? mid.ValueKind == JsonValueKind.Number ? mid.GetInt32().ToString() : mid.GetString()
            : null;

        if (!int.TryParse(matchIdStr, out int matchId))
        {
            _logger.LogWarning("Event received without valid matchid: {EventType}", eventType);
            return;
        }

        var eventLog = new MatchEventLog
        {
            MatchId = matchId,
            EventType = eventType ?? "unknown",
            RawEventData = payload.GetRawText(),
            Timestamp = DateTime.UtcNow
        };
        _context.MatchEventLogs.Add(eventLog);

        switch (eventType)
        {
            case "series_start":
                await ProcessSeriesStart(matchId, payload);
                break;
            case "going_live":
                await ProcessGoingLive(matchId);
                break;
            case "round_end":
                await ProcessRoundEnd(matchId, payload);
                break;
            case "map_result":
                await ProcessMapResult(matchId, payload);
                break;
            case "series_end":
                await ProcessSeriesEnd(matchId, payload);
                break;
            case "demo_upload_ended":
                await ProcessDemoUploadEnded(matchId, payload);
                break;
            case "map_picked":
            case "map_vetoed":
            case "side_picked":
                _logger.LogInformation("Veto event: {EventType} for Match {MatchId}", eventType, matchId);
                break;
            default:
                _logger.LogInformation("Unhandled event: {EventType} for Match {MatchId}", eventType, matchId);
                break;
        }

        await _context.SaveChangesAsync();
    }

    private async Task ProcessSeriesStart(int matchId, JsonElement payload)
    {
        var match = await _context.Matches.FindAsync(matchId);
        if (match == null) return;

        match.StartTime = DateTime.UtcNow;
        _logger.LogInformation("Series started for Match {MatchId}", matchId);
    }

    private async Task ProcessGoingLive(int matchId)
    {
        var match = await _context.Matches.FindAsync(matchId);
        if (match == null) return;

        if (match.StartTime == null)
            match.StartTime = DateTime.UtcNow;

        _logger.LogInformation("Match {MatchId} is now LIVE", matchId);
    }

    private async Task ProcessRoundEnd(int matchId, JsonElement payload)
    {
        int roundNumber = payload.TryGetProperty("round_number", out var rn) ? rn.GetInt32() : 0;
        string? winner = payload.TryGetProperty("winner", out var w) ? w.GetString() : null;
        int? reason = payload.TryGetProperty("reason", out var r) ? r.GetInt32() : null;

        var timeline = new MatchRoundTimeline
        {
            MatchId = matchId,
            RoundNumber = roundNumber,
            EventType = "round_end",
            ActorSteamId = winner,
            TargetSteamId = reason?.ToString(),
            Timestamp = DateTime.UtcNow
        };
        _context.MatchRoundTimelines.Add(timeline);

        _logger.LogInformation("Round {Round} ended in Match {MatchId}. Winner: {Winner}, Reason: {Reason}",
            roundNumber, matchId, winner, reason);
    }

    private async Task ProcessMapResult(int matchId, JsonElement payload)
    {
        var match = await _context.Matches.FindAsync(matchId);
        if (match == null) return;

        if (payload.TryGetProperty("winner", out var winnerProp))
            match.Winner = winnerProp.ValueKind == JsonValueKind.Object
                ? winnerProp.GetProperty("team").GetString()
                : winnerProp.GetString();

        if (payload.TryGetProperty("team1", out var t1) && t1.TryGetProperty("players", out var t1Players))
        {
            await SavePlayerStats(matchId, "Team1", t1Players);
        }

        if (payload.TryGetProperty("team2", out var t2) && t2.TryGetProperty("players", out var t2Players))
        {
            await SavePlayerStats(matchId, "Team2", t2Players);
        }

        _logger.LogInformation("Map result processed for Match {MatchId}", matchId);
    }

    private async Task SavePlayerStats(int matchId, string team, JsonElement playersElement)
    {
        if (playersElement.ValueKind != JsonValueKind.Array) return;

        foreach (var player in playersElement.EnumerateArray())
        {
            var steamId = player.TryGetProperty("steamid", out var sid) ? sid.GetString() ?? "" : "";
            
            var existing = await _context.MatchPlayerStats
                .FirstOrDefaultAsync(s => s.MatchId == matchId && s.SteamId == steamId);

            if (existing == null)
            {
                existing = new MatchPlayerStat
                {
                    MatchId = matchId,
                    SteamId = steamId,
                    Team = team
                };
                _context.MatchPlayerStats.Add(existing);
            }

            existing.Name = player.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            existing.Kills = player.TryGetProperty("kills", out var k) ? k.GetInt32() : 0;
            existing.Deaths = player.TryGetProperty("deaths", out var d) ? d.GetInt32() : 0;
            existing.Assists = player.TryGetProperty("assists", out var a) ? a.GetInt32() : 0;
            existing.Damage = player.TryGetProperty("damage", out var dmg) ? dmg.GetInt32() : 0;
            existing.HeadshotKills = player.TryGetProperty("headshot_kills", out var hs) ? hs.GetInt32() : 0;
            existing.Mvp = player.TryGetProperty("mvp", out var mvp) ? mvp.GetInt32() : 0;
            existing.Score = player.TryGetProperty("score", out var sc) ? sc.GetInt32() : 0;
        }
    }

    private async Task ProcessSeriesEnd(int matchId, JsonElement payload)
    {
        var match = await _context.Matches.FindAsync(matchId);
        if (match == null) return;

        match.EndTime = DateTime.UtcNow;

        if (payload.TryGetProperty("winner", out var winnerProp))
        {
            match.Winner = winnerProp.ValueKind == JsonValueKind.Object
                ? winnerProp.GetProperty("team").GetString()
                : winnerProp.GetString();
        }

        await RecalculateElo(matchId, match.Winner);
        _logger.LogInformation("Series ended for Match {MatchId}. Winner: {Winner}", matchId, match.Winner);
    }

    private async Task RecalculateElo(int matchId, string? winner)
    {
        var playerStats = await _context.MatchPlayerStats
            .Where(s => s.MatchId == matchId)
            .ToListAsync();

        if (playerStats.Count == 0) return;

        var team1SteamIds = playerStats.Where(s => s.Team == "Team1" || s.Team == "1").Select(s => s.SteamId).ToList();
        var team2SteamIds = playerStats.Where(s => s.Team == "Team2" || s.Team == "2").Select(s => s.SteamId).ToList();

        var allSteamIds = team1SteamIds.Concat(team2SteamIds).ToList();
        var users = await _context.Users
            .Where(u => allSteamIds.Contains(u.SteamId))
            .ToDictionaryAsync(u => u.SteamId, u => u);

        var team1Elos = team1SteamIds.Where(id => users.ContainsKey(id)).ToDictionary(id => id, id => users[id].Elo);
        var team2Elos = team2SteamIds.Where(id => users.ContainsKey(id)).ToDictionary(id => id, id => users[id].Elo);

        if (team1Elos.Count == 0 || team2Elos.Count == 0) return;

        bool team1Won = winner == "team1" || winner == "1";
        var newRatings = EloService.CalculateNewRatings(team1Elos, team2Elos, team1Won);

        foreach (var (steamId, newElo) in newRatings)
        {
            if (users.TryGetValue(steamId, out var user))
            {
                _logger.LogInformation("Elo update: {SteamId} {OldElo} -> {NewElo}", steamId, user.Elo, newElo);
                user.Elo = newElo;
            }
        }
    }

    private async Task ProcessDemoUploadEnded(int matchId, JsonElement payload)
    {
        if (!payload.TryGetProperty("success", out var success) || !success.GetBoolean())
        {
            _logger.LogWarning("Demo upload failed for Match {MatchId}", matchId);
            return;
        }

        if (payload.TryGetProperty("url", out var urlProp))
        {
            var match = await _context.Matches.FindAsync(matchId);
            if (match != null)
            {
                match.DemoUrl = urlProp.GetString();
                _logger.LogInformation("Demo URL set via event for Match {MatchId}: {Url}", matchId, match.DemoUrl);
            }
        }
    }

    public async Task<string?> UploadDemoAsync(int matchId, string fileName, Stream fileStream, string contentType)
    {
        var match = await _context.Matches.FindAsync(matchId);
        if (match == null) return null;

        var bucketName = _configuration["S3:BucketName"] ?? "cs2-demos";

        var bucketExists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName);
        if (!bucketExists)
            await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = bucketName });

        var objectKey = string.IsNullOrEmpty(fileName)
            ? $"demos/{matchId}/demo_{DateTime.UtcNow:yyyyMMddHHmmss}.zip"
            : $"demos/{matchId}/{fileName}";

        var uploadRequest = new TransferUtilityUploadRequest
        {
            InputStream = fileStream,
            Key = objectKey,
            BucketName = bucketName,
            ContentType = contentType
        };

        var transferUtility = new TransferUtility(_s3Client);
        await transferUtility.UploadAsync(uploadRequest);

        var s3Url = _configuration["S3:PublicUrl"] ?? _configuration["S3:ServiceUrl"];
        match.DemoUrl = $"{s3Url}/{bucketName}/{objectKey}";
        await _context.SaveChangesAsync();

        _logger.LogInformation("Demo uploaded for Match {MatchId}: {DemoUrl}", matchId, match.DemoUrl);

        // Publish to RabbitMQ
        await PublishParsingTask(matchId, match.DemoUrl);

        return match.DemoUrl;
    }

    private async Task PublishParsingTask(int matchId, string demoUrl)
    {
        try
        {
            var factory = new RabbitMQ.Client.ConnectionFactory
            {
                Uri = new Uri(_configuration["RabbitMQ:Url"] ?? "amqp://guest:guest@localhost:5672/")
            };

            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            var queueName = _configuration["RabbitMQ:ParsingQueue"] ?? "demo_parsing_tasks";
            await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

            var payload = JsonSerializer.Serialize(new { MatchId = matchId, DemoUrl = demoUrl });
            var body = System.Text.Encoding.UTF8.GetBytes(payload);

            var properties = new RabbitMQ.Client.BasicProperties
            {
                Persistent = true
            };

            await channel.BasicPublishAsync(exchange: string.Empty, routingKey: queueName, mandatory: false, basicProperties: properties, body: body);
            _logger.LogInformation("Published parsing task for Match {MatchId} to RabbitMQ queue {QueueName}", matchId, queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish parsing task to RabbitMQ for Match {MatchId}", matchId);
        }
    }
}
