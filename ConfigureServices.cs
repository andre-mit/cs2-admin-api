using Cs2Admin.API.Services;
using Cs2Admin.API.Services.Interfaces;
using Docker.DotNet;

namespace Cs2Admin.API;

public static class ConfigureServices
{
    public static void AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHostedService<S3CleanupService>();

        services.AddScoped<IRconService, RconService>();
        services.AddScoped<IServerService, ServerService>();
        services.AddScoped<ISteamTokenService, SteamTokenService>();
        services.AddSingleton<IPortAllocatorService, PortAllocatorService>();

        // Docker Client
        var dockerUri = configuration.GetValue<string?>("Docker:Uri") ?? "unix:///var/run/docker.sock";
        var dockerClient = new DockerClientConfiguration(new Uri(dockerUri)).CreateClient();
        services.AddSingleton<IDockerClient>(dockerClient);
    }
}