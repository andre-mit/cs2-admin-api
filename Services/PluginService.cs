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
                
                DetectAndNormalizePluginStructure(pluginDir, plugin.Name);
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

        private void DetectAndNormalizePluginStructure(string targetDir, string pluginName)
        {
            var dllFiles = Directory.GetFiles(targetDir, "*.dll", SearchOption.AllDirectories);
            if (dllFiles.Length == 0) return;

            var mainDllPath = dllFiles.FirstOrDefault(f =>
                                  Path.GetFileNameWithoutExtension(f)
                                      .Equals(pluginName, StringComparison.OrdinalIgnoreCase))
                              ?? dllFiles.First();

            var actualPluginFolder = Path.GetDirectoryName(mainDllPath);

            if (actualPluginFolder != null && actualPluginFolder != targetDir)
            {
                logger.LogInformation("Nesting detected. Normalizing folder structure from {ActualFolder} to {TargetDir}", actualPluginFolder, targetDir);

                var tempTarget = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempTarget);
                
                MoveDirectoryContents(actualPluginFolder, tempTarget);
                
                Directory.Delete(targetDir, true);
                Directory.CreateDirectory(targetDir);
                
                MoveDirectoryContents(tempTarget, targetDir);
                Directory.Delete(tempTarget, true);
            }
        }

        private static void MoveDirectoryContents(string source, string target)
        {
            foreach (var file in Directory.GetFiles(source))
            {
                var dest = Path.Combine(target, Path.GetFileName(file));
                File.Move(file, dest, true);
            }

            foreach (var dir in Directory.GetDirectories(source))
            {
                var dest = Path.Combine(target, Path.GetFileName(dir));
                Directory.CreateDirectory(dest);
                MoveDirectoryContents(dir, dest);
            }
        }
    }
}