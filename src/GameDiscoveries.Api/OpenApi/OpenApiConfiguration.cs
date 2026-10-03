using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace GameDiscoveries.Api.OpenApi;

public static class OpenApiConfiguration
{
    public static IServiceCollection AddGameDiscoveriesOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "GameDiscoveries API",
                    Version = "v1",
                    Description = "Backend API for GameDiscoveries.com"
                };

                return Task.CompletedTask;
            });
        });

        return services;
    }

    public static WebApplication MapGameDiscoveriesOpenApi(this WebApplication app)
    {
        app.MapOpenApi("/openapi/v1.json");

        if (app.Environment.IsDevelopment())
        {
            app.MapGet("/swagger", () => Results.Redirect("/openapi/v1.json"))
                .ExcludeFromDescription();
        }

        return app;
    }
}
