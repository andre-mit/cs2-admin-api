namespace Cs2Admin.API.Models
{
    public class LobbyPlayer
    {
        public int Id { get; set; }
        
        public int LobbyId { get; set; }
        public Lobby? Lobby { get; set; }

        public string SteamId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }

        // 1 for Team A, 2 for Team B, 0 for Spectator
        public int TeamDesignation { get; set; } = 0;
        
        public bool IsCaptain { get; set; } = false;
    }
}
