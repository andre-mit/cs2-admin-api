using System;
using System.ComponentModel.DataAnnotations;

namespace Cs2Admin.API.Models
{
    public class User
    {
        [Key]
        [MaxLength(64)]
        public string SteamId { get; set; } = string.Empty;

        [MaxLength(100)]
        public string InternalNick { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? AvatarUrl { get; set; }

        public int Elo { get; set; } = 1000;

        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    }
}
