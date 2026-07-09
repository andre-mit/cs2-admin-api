using System;

namespace Cs2Admin.API.Models
{
    public class Match
    {
        public int Id { get; set; }
        public int Team1Id { get; set; }
        public Team? Team1 { get; set; }
        
        public int Team2Id { get; set; }
        public Team? Team2 { get; set; }
        
        public int? ServerId { get; set; }
        public Server? Server { get; set; }
        
        public int MaxMaps { get; set; }
        public bool SkipVeto { get; set; }
        public string? MapStat { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Winner { get; set; }
        public string? Forfeit { get; set; }
        public string? DemoUrl { get; set; }
    }
}
