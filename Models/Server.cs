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
    }
}
