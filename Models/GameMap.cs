namespace Cs2Admin.API.Models
{
    public class GameMap
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
        public bool IsCommunity { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? BadgeUrl { get; set; }
    }
}
