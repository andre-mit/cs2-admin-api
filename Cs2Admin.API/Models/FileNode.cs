using System.Collections.Generic;

namespace Cs2Admin.API.Models
{
    public class FileNode
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public List<FileNode>? Children { get; set; }
    }
}
