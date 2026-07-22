using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cs2Admin.API.Models;

namespace Cs2Admin.API.Services.Interfaces
{
    public interface IPluginService
    {
        Task<List<GamePlugin>> GetAllAsync(CancellationToken ct = default);
        Task<GamePlugin?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<GamePlugin> CreateAsync(GamePlugin plugin, CancellationToken ct = default);
        Task<GamePlugin> UpdateAsync(int id, GamePlugin plugin, CancellationToken ct = default);
        Task<GamePlugin> UploadAsync(int pluginId, IFormFile zipFile, CancellationToken ct = default);
        Task<GamePlugin> UploadChunkAsync(int pluginId, IFormFile chunk, int chunkIndex, int totalChunks, CancellationToken ct = default);
        Task<FileNode?> GetFileTreeAsync(int id, CancellationToken ct = default);
        Task<string?> GetFileContentAsync(int id, string path, CancellationToken ct = default);
        Task SaveFileContentAsync(int id, string path, string content, CancellationToken ct = default);
        Task DeleteFileAsync(int id, string path, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
    }
}
