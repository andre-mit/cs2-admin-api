namespace Cs2Admin.API.Models
{
    public class FileEditRequest
    {
        public string Path { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
