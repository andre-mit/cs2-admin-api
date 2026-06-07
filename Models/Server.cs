using System;

namespace Cs2Admin.API.Models
{
    public class Server
    {
        public int Id { get; set; }
        public string IpString { get; set; } = string.Empty;
        public int Port { get; set; }
        public string? RconPassword { get; set; }
        public string? DisplayName { get; set; }
        public bool InUse { get; set; }

        /// <summary>
        /// Docker container ID/name for dynamically created servers.
        /// </summary>
        public string? ContainerId { get; set; }

        /// <summary>
        /// GOTV port for the server.
        /// </summary>
        public int? TvPort { get; set; }

        /// <summary>
        /// True if this server was created dynamically via Docker.
        /// </summary>
        public bool IsDynamic { get; set; }

        /// <summary>
        /// When the server record was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
