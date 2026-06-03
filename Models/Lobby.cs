using System;
using System.Collections.Generic;

namespace Cs2Admin.API.Models
{
    public class Lobby
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        
        public string Team1Name { get; set; } = "Team 1";
        public string Team2Name { get; set; } = "Team 2";
        
        // Waiting, Veto, Ready
        public string State { get; set; } = "Waiting";
        
        public int MaxMaps { get; set; } = 1;
        public string MapPool { get; set; } = "de_mirage,de_inferno,de_overpass,de_nuke,de_vertigo,de_ancient,de_anubis";
        
        public string VetoHistory { get; set; } = "[]"; // JSON array of veto steps
        public string SelectedMaps { get; set; } = "[]"; // JSON array of picked maps
        
        // e.g., "knife" or "pick"
        public string MapSidesMode { get; set; } = "knife";

        public string? GeneratedJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public ICollection<LobbyPlayer> Players { get; set; } = new List<LobbyPlayer>();
    }
}
