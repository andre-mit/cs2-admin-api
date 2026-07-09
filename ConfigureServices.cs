using Cs2Admin.API.Services;
using Cs2Admin.API.Services.Interfaces;
using Docker.DotNet;

namespace Cs2Admin.API;

public static class ConfigureServices
{
    public static void AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHostedService<S3CleanupService>();

        services.AddSingleton<IMatchmakingService, MatchmakingWorker>();
        services.AddHostedService(sp => (MatchmakingWorker)sp.GetRequiredService<IMatchmakingService>());

        services.AddScoped<IRconService, RconService>();
        services.AddScoped<ServerService>();
        services.AddScoped<IServerService>(sp => sp.GetRequiredService<ServerService>());
        services.AddScoped<IPluginService, PluginService>();
        services.AddScoped<ISteamTokenService, SteamTokenService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IMatchHistoryService, MatchHistoryService>();
        services.AddScoped<IMatchZyService, MatchZyService>();
        services.AddSingleton<IPortAllocatorService, PortAllocatorService>();

        // Docker Client
        var dockerUri = configuration.GetValue<string?>("Docker:Uri") ?? "unix:///var/run/docker.sock";
        var dockerClient = new DockerClientConfiguration(new Uri(dockerUri)).CreateClient();
        services.AddSingleton<IDockerClient>(dockerClient);
    }
}