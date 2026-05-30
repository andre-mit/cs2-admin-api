using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cs2Admin.API.Models
{
    public class MatchPlayerStat
    {
        [Key]
        public int Id { get; set; }

        public int MatchId { get; set; }
        [ForeignKey("MatchId")]
        public Match? Match { get; set; }

        // The SteamID64 of the player
        [Required]
        [MaxLength(64)]
        public string SteamId { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        // E.g. "Team1" or "Team2"
        [MaxLength(20)]
        public string Team { get; set; } = string.Empty;

        public int Kills { get; set; } = 0;
        public int Deaths { get; set; } = 0;
        public int Assists { get; set; } = 0;
        public int HeadshotKills { get; set; } = 0;
        public int Damage { get; set; } = 0;
        public int Mvp { get; set; } = 0;
        public int Score { get; set; } = 0;
    }
}
