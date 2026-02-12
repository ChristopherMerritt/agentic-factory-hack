using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RepairPlanner.Services;

/// <summary>
/// Extension methods for configuring Cosmos DB services.
/// </summary>
public static class CosmosDbServiceExtensions
{
    public static IServiceCollection UseCosmosDbService(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<CosmosDbService.CosmosDbOptions>()
            .Bind(config.GetSection(CosmosDbService.CosmosDbOptions.SectionName))
            .Validate(options =>
            {
                // Validate that all required Cosmos DB settings are present
                return !string.IsNullOrWhiteSpace(options.Endpoint) &&
                       !string.IsNullOrWhiteSpace(options.Key) &&
                       !string.IsNullOrWhiteSpace(options.DatabaseName);
            }, "One or more required CosmosDb configuration values are missing")
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<CosmosDbService.CosmosDbOptions>>().Value;
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CosmosDbService>>();
            return new CosmosDbService(options, logger);
        });

        return services;
    }
}
