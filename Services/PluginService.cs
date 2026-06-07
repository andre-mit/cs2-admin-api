using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Cs2Admin.API.Configurations;
using Cs2Admin.API.Data;
using Cs2Admin.API.Models;
using Cs2Admin.API.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cs2Admin.API.Services
{
    public class PluginService : IPluginService
    {
        private readonly ApplicationDbContext _context;
        private readonly ServersConfiguration _serversConfiguration;

        public PluginService(ApplicationDbContext context, IOptions<ServersConfiguration> serversConfigurationOptions)
        {
            _context = context;
            _serversConfiguration = serversConfigurationOptions.Value;
        }

        public Task<List<GamePlugin>> GetAllAsync(CancellationToken ct = default)
        {
            return _context.GamePlugins.ToListAsync(ct);
        }

        public Task<GamePlugin?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return _context.GamePlugins.FindAsync(new object[] { id }, ct).AsTask();
        }

        public async Task<GamePlugin> CreateAsync(GamePlugin plugin, CancellationToken ct = default)
        {
            _context.GamePlugins.Add(plugin);
            await _context.SaveChangesAsync(ct);
            return plugin;
        }

        public async Task<GamePlugin> UploadAsync(int pluginId, IFormFile zipFile, CancellationToken ct = default)
        {
            var plugin = await _context.GamePlugins.FindAsync(new object[] { pluginId }, ct);
            if (plugin == null) throw new Exception("Plugin not found");

            if (zipFile == null || zipFile.Length == 0)
                throw new Exception("File is empty");

            if (Path.GetExtension(zipFile.FileName).ToLower() != ".zip")
                throw new Exception("Only .zip files are allowed");

            var pluginDir = Path.Combine(_serversConfiguration.PluginsBaseDir, plugin.Name);

            // Clean up existing directory if it exists
            if (Directory.Exists(pluginDir))
            {
                Directory.Delete(pluginDir, true);
            }

            Directory.CreateDirectory(pluginDir);

            var tempFilePath = Path.GetTempFileName();
            try
            {
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await zipFile.CopyToAsync(stream, ct);
                }

                ZipFile.ExtractToDirectory(tempFilePath, pluginDir, overwriteFiles: true);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }

            return plugin;
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var plugin = await _context.GamePlugins.FindAsync(new object[] { id }, ct);
            if (plugin != null)
            {
                var pluginDir = Path.Combine(_serversConfiguration.PluginsBaseDir, plugin.Name);
                if (Directory.Exists(pluginDir))
                {
                    Directory.Delete(pluginDir, true);
                }

                _context.GamePlugins.Remove(plugin);
                await _context.SaveChangesAsync(ct);
            }
        }
    }
}
