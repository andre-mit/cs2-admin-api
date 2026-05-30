using System;
using System.Collections.Generic;

namespace Cs2Admin.API.Models
{
    public class Team
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Flag { get; set; }
        public string? Logo { get; set; }
        
        // Steam IDs stored as a JSON string or comma separated for simplicity in this draft
        public string? PlayerSteamIds { get; set; }
    }
}
