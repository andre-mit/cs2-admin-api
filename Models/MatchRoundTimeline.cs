using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cs2Admin.API.Models
{
    public class MatchRoundTimeline
    {
        [Key]
        public int Id { get; set; }

        public int MatchId { get; set; }
        [ForeignKey("MatchId")]
        public Match? Match { get; set; }

        public int RoundNumber { get; set; }

        // Event type like "kill", "assist", "bomb_planted", "bomb_defused"
        [Required]
        [MaxLength(50)]
        public string EventType { get; set; } = string.Empty;

        [MaxLength(64)]
        public string? ActorSteamId { get; set; }

        [MaxLength(64)]
        public string? TargetSteamId { get; set; }

        public string? Weapon { get; set; }
        public bool Headshot { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
