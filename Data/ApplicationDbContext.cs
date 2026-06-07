using Cs2Admin.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cs2Admin.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Server> Servers { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<Match> Matches { get; set; }
        public DbSet<MatchPlayerStat> MatchPlayerStats { get; set; }
        public DbSet<MatchEventLog> MatchEventLogs { get; set; }
        public DbSet<Lobby> Lobbies { get; set; }
        public DbSet<LobbyPlayer> LobbyPlayers { get; set; }
        public DbSet<GameMap> Maps { get; set; }
        
        public DbSet<SteamServerToken> SteamServerTokens { get; set; }
        public DbSet<GamePlugin> GamePlugins { get; set; }
        public DbSet<ServerPlugin> ServerPlugins { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Map table names to existing structure if needed or define constraints
            modelBuilder.Entity<Match>()
                .HasOne(m => m.Team1)
                .WithMany()
                .HasForeignKey(m => m.Team1Id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Match>()
                .HasOne(m => m.Team2)
                .WithMany()
                .HasForeignKey(m => m.Team2Id)
                .OnDelete(DeleteBehavior.Restrict);
                
            modelBuilder.Entity<ServerPlugin>()
                .HasIndex(sp => new { sp.ServerId, sp.GamePluginId })
                .IsUnique();
        }
    }
}
