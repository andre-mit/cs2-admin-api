using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cs2Admin.API.Models
{
    public class MatchEventLog
    {
        [Key]
        public int Id { get; set; }

        public int MatchId { get; set; }
        [ForeignKey("MatchId")]
        public Match? Match { get; set; }

        // event type: "player_death", "round_end", "bomb_planted", etc.
        [Required]
        [MaxLength(50)]
        public string EventType { get; set; } = string.Empty;

        // Raw JSON payload of the event for debugging or future processing
        public string RawEventData { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
