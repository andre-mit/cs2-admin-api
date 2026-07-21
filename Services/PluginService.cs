using System.IO;
using System.IO.Compression;
using Cs2Admin.API.Configurations;
using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Cs2Admin.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cs2Admin.API.Services
{
    public class PluginService(
        ApplicationDbContext context,
        IOptions<ServersConfiguration> serversConfigurationOptions,
        ILogger<PluginService> logger)
        : IPluginService
    {
        private readonly ServersConfiguration _serversConfiguration = serversConfigurationOptions.Value;

        public Task<List<GamePlugin>> GetAllAsync(CancellationToken ct = default)
        {
            return context.GamePlugins.ToListAsync(ct);
        }

        public Task<GamePlugin?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return context.GamePlugins.FindAsync(new object[] { id }, ct).AsTask();
        }

        public async Task<GamePlugin> CreateAsync(GamePlugin plugin, CancellationToken ct = default)
        {
            context.GamePlugins.Add(plugin);
            await context.SaveChangesAsync(ct);
            return plugin;
        }

        public async Task<GamePlugin> UpdateAsync(int id, GamePlugin updatedPlugin, CancellationToken ct = default)
        {
            var plugin = await context.GamePlugins.FindAsync(new object[] { id }, ct);
            if (plugin == null) throw new Exception("Plugin not found");

            var oldDir = Path.Combine(_serversConfiguration.PluginsBaseDir, plugin.Name);
            
            plugin.Name = updatedPlugin.Name;
            plugin.Description = updatedPlugin.Description;
            plugin.ConfigFilesJson = updatedPlugin.ConfigFilesJson;

            var newDir = Path.Combine(_serversConfiguration.PluginsBaseDir, plugin.Name);

            if (oldDir != newDir && Directory.Exists(oldDir))
            {
                Directory.Move(oldDir, newDir);
            }

            await context.SaveChangesAsync(ct);
            return plugin;
        }

        public async Task<GamePlugin> UploadAsync(int pluginId, IFormFile zipFile, CancellationToken ct = default)
        {
            var plugin = await context.GamePlugins.FindAsync(new object[] { pluginId }, ct);
            if (plugin == null) throw new Exception("Plugin not found");

            if (zipFile == null || zipFile.Length == 0)
                throw new Exception("File is empty");

            if (!Path.GetExtension(zipFile.FileName).Equals(".zip", StringComparison.CurrentCultureIgnoreCase))
                throw new Exception("Only .zip files are allowed");

            var pluginDir = Path.Combine(_serversConfiguration.PluginsBaseDir, plugin.Name);

            if (Directory.Exists(pluginDir))
            {
                logger.LogInformation("Cleaning up existing plugin directory: {PluginDir}", pluginDir);
                Directory.Delete(pluginDir, true);
            }

            logger.LogInformation("Creating plugin directory: {PluginDir}", pluginDir);
            Directory.CreateDirectory(pluginDir);

            var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");

            try
            {
                await using (var stream =
                             new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    logger.LogInformation("Saving uploaded zip file to temporary path: {TempFilePath}", tempFilePath);
                    await zipFile.CopyToAsync(stream, ct);
                }

                logger.LogInformation("Extracting zip file to plugin directory: {PluginDir}", pluginDir);
                ZipFile.ExtractToDirectory(tempFilePath, pluginDir, overwriteFiles: true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to upload and extract plugin {PluginName}", plugin.Name);
                throw;
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    logger.LogInformation("Deleting temporary file: {TempFilePath}", tempFilePath);
                    File.Delete(tempFilePath);
                }
            }

            logger.LogInformation("Plugin {PluginName} uploaded and structured successfully to {PluginDir}",
                plugin.Name, pluginDir);
            return plugin;
        }

        public async Task<GamePlugin> UploadChunkAsync(int pluginId, IFormFile chunk, int chunkIndex, int totalChunks, CancellationToken ct = default)
        {
            var plugin = await context.GamePlugins.FindAsync(new object[] { pluginId }, ct);
            if (plugin == null) throw new Exception("Plugin not found");

            if (chunk == null || chunk.Length == 0)
                throw new Exception("Chunk is empty");

            var pluginDir = Path.Combine(_serversConfiguration.PluginsBaseDir, plugin.Name);
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"plugin_upload_{pluginId}.zip.tmp");

            // On first chunk, clean up any previous failed uploads or existing plugin dir
            if (chunkIndex == 0)
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
                if (Directory.Exists(pluginDir))
                {
                    logger.LogInformation("Cleaning up existing plugin directory: {PluginDir}", pluginDir);
                    Directory.Delete(pluginDir, true);
                }
            }

            // Append chunk to temp file
            try
            {
                await using (var stream = new FileStream(tempFilePath, FileMode.Append, FileAccess.Write, FileShare.None))
                {
                    await chunk.CopyToAsync(stream, ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write chunk {ChunkIndex} for plugin {PluginName}", chunkIndex, plugin.Name);
                throw;
            }

            // If it's the last chunk, extract the file
            if (chunkIndex == totalChunks - 1)
            {
                try
                {
                    logger.LogInformation("Creating plugin directory: {PluginDir}", pluginDir);
                    Directory.CreateDirectory(pluginDir);

                    logger.LogInformation("Extracting completed zip file to plugin directory: {PluginDir}", pluginDir);
                    ZipFile.ExtractToDirectory(tempFilePath, pluginDir, overwriteFiles: true);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to extract chunked plugin {PluginName}", plugin.Name);
                    throw;
                }
                finally
                {
                    if (File.Exists(tempFilePath))
                    {
                        logger.LogInformation("Deleting temporary chunked file: {TempFilePath}", tempFilePath);
                        File.Delete(tempFilePath);
                    }
                }
                logger.LogInformation("Plugin {PluginName} uploaded via chunks and structured successfully to {PluginDir}", plugin.Name, pluginDir);
            }

            return plugin;
        }

        public async Task<FileNode?> GetFileTreeAsync(int id, CancellationToken ct = default)
        {
            var plugin = await context.GamePlugins.FindAsync([id], ct);
            if (plugin == null) return null;

            var pluginDir = Path.Combine(_serversConfiguration.PluginsBaseDir, plugin.Name);
            if (!Directory.Exists(pluginDir)) return null;

            return BuildFileTree(new DirectoryInfo(pluginDir), pluginDir);
        }

        private FileNode BuildFileTree(DirectoryInfo dirInfo, string basePluginDir)
        {
            var node = new FileNode
            {
                Name = dirInfo.Name,
                Path = Path.GetRelativePath(basePluginDir, dirInfo.FullName).Replace("\\", "/"),
                IsDirectory = true,
                Children = new List<FileNode>()
            };

            if (node.Path == ".") node.Path = ""; // Root

            foreach (var dir in dirInfo.GetDirectories())
            {
                node.Children.Add(BuildFileTree(dir, basePluginDir));
            }

            foreach (var file in dirInfo.GetFiles())
            {
                node.Children.Add(new FileNode
                {
                    Name = file.Name,
                    Path = Path.GetRelativePath(basePluginDir, file.FullName).Replace("\\", "/"),
                    IsDirectory = false
                });
            }

            return node;
        }

        private string EnsureSafePath(string baseDir, string relativePath)
        {
            if (relativePath.Contains("..")) throw new Exception("Invalid path");
            var fullPath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
            if (!fullPath.StartsWith(Path.GetFullPath(baseDir))) throw new Exception("Path traversal detected");
            return fullPath;
        }

        public async Task<string?> GetFileContentAsync(int id, string path, CancellationToken ct = default)
        {
            var plugin = await context.GamePlugins.FindAsync([id], ct);
            if (plugin == null) return null;

            var pluginDir = Path.Combine(_serversConfiguration.PluginsBaseDir, plugin.Name);
            if (!Directory.Exists(pluginDir)) return null;

            var fullPath = EnsureSafePath(pluginDir, path);
            if (!File.Exists(fullPath)) return null;

            return await File.ReadAllTextAsync(fullPath, ct);
        }

        public async Task SaveFileContentAsync(int id, string path, string content, CancellationToken ct = default)
        {
            var plugin = await context.GamePlugins.FindAsync([id], ct);
            if (plugin == null) throw new Exception("Plugin not found");

            var pluginDir = Path.Combine(_serversConfiguration.PluginsBaseDir, plugin.Name);
            var fullPath = EnsureSafePath(pluginDir, path);

            var dir = Path.GetDirectoryName(fullPath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(fullPath, content, ct);
        }

        public async Task DeleteFileAsync(int id, string path, CancellationToken ct = default)
        {
            var plugin = await context.GamePlugins.FindAsync([id], ct);
            if (plugin == null) throw new Exception("Plugin not found");

            var pluginDir = Path.Combine(_serversConfiguration.PluginsBaseDir, plugin.Name);
            var fullPath = EnsureSafePath(pluginDir, path);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            else if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
            }
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var plugin = await context.GamePlugins.FindAsync([id], ct);
            if (plugin != null)
            {
                var pluginDir = Path.Combine(_serversConfiguration.PluginsBaseDir, plugin.Name);
                if (Directory.Exists(pluginDir))
                {
                    Directory.Delete(pluginDir, true);
                }

                context.GamePlugins.Remove(plugin);
                await context.SaveChangesAsync(ct);
            }
        }
    }
}