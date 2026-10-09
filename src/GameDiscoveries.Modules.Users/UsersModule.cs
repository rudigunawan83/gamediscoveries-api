using FluentValidation;
using GameDiscoveries.BuildingBlocks.Abstractions;
using GameDiscoveries.Modules.Users.Data;
using GameDiscoveries.Modules.Users.Features.Avatar;
using GameDiscoveries.Modules.Users.Features.GetCurrentUser;
using GameDiscoveries.Modules.Users.Features.Login;
using GameDiscoveries.Modules.Users.Features.Logout;
using GameDiscoveries.Modules.Users.Features.Register;
using GameDiscoveries.Modules.Users.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameDiscoveries.Modules.Users;

public sealed class UsersModule : IModule
{
    public string Name => "Users";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RegisterHandler>();
        services.AddScoped<GetCurrentUserHandler>();
        services.AddOptions<AvatarOptions>().BindConfiguration(AvatarOptions.SectionName);
        services.AddSingleton<AvatarStorage>();
        services.AddScoped<AvatarHandler>();
        services.AddValidatorsFromAssemblyContaining<LoginValidator>();
        services.AddValidatorsFromAssemblyContaining<RegisterValidator>();
        services.AddHostedService<AuthSeedHostedService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapLogin();
        endpoints.MapRegister();
        endpoints.MapLogout();
        endpoints.MapGetCurrentUser();
        endpoints.MapAvatar();
    }
}

public static class UsersModuleExtensions
{
    public static IServiceCollection AddUsersModule(this IServiceCollection services)
    {
        var module = new UsersModule();
        module.RegisterServices(services);
        services.AddSingleton<IModule>(module);
        return services;
    }

    public static IEndpointRouteBuilder MapUsersModule(this IEndpointRouteBuilder endpoints)
    {
        new UsersModule().MapEndpoints(endpoints);
        return endpoints;
    }
}
