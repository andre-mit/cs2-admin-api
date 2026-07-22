using Cs2Admin.API.Services;
using Cs2Admin.API.Services.Interfaces;
using Docker.DotNet;
using FluentValidation;
using Cs2Admin.API.Infrastructure.PipelineBehaviors;
using Cs2Admin.API.Infrastructure.Repositories;

namespace Cs2Admin.API;

public static class ConfigureServices
{
    public static void AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHostedService<S3CleanupService>();
        services.AddHostedService<ServerMonitorBackgroundService>();

        services.AddSingleton<IServerEventService, ServerEventService>();

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

        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<IServerRepository, ServerRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IMatchEventLogRepository, MatchEventLogRepository>();
        services.AddScoped<IMatchPlayerStatRepository, MatchPlayerStatRepository>();
        services.AddScoped<IMapRepository, MapRepository>();
        services.AddScoped<ISteamTokenRepository, SteamTokenRepository>();
        services.AddScoped<IServerPresetRepository, ServerPresetRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IMatchRoundTimelineRepository, MatchRoundTimelineRepository>();

        // Mediator & Validation
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
        });
        services.AddValidatorsFromAssembly(typeof(ConfigureServices).Assembly);
        services.AddScoped(typeof(Mediator.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Docker Client
        var dockerUri = configuration.GetValue<string?>("Docker:Uri") ?? "unix:///var/run/docker.sock";
        var dockerClient = new DockerClientConfiguration(new Uri(dockerUri)).CreateClient();
        services.AddSingleton<IDockerClient>(dockerClient);
    }
}