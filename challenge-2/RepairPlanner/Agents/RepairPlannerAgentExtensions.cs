using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepairPlanner.Services;

namespace RepairPlanner.Agents;

/// <summary>
/// Extension methods for configuring Repair Planner Agent services.
/// </summary>
public static class RepairPlannerAgentExtensions
{
    public static IServiceCollection UseRepairPlannerAgent(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<RepairPlannerAgent.AgentOptions>()
            .Bind(config.GetSection(RepairPlannerAgent.AgentOptions.SectionName))
            .Validate(options =>
            {
                // Validate that all required Agent settings are present
                return !string.IsNullOrWhiteSpace(options.ProjectEndpoint) &&
                       !string.IsNullOrWhiteSpace(options.ModelDeploymentName);
            }, "One or more required RepairPlannerAgent configuration values are missing")
            .ValidateOnStart();

        // Create AI Project Client with DefaultAzureCredential
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RepairPlannerAgent.AgentOptions>>().Value;
            return new AIProjectClient(new Uri(options.ProjectEndpoint), new DefaultAzureCredential());
        });

        // Register the Repair Planner Agent
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RepairPlannerAgent.AgentOptions>>().Value;
            return new RepairPlannerAgent(
                sp.GetRequiredService<AIProjectClient>(),
                sp.GetRequiredService<CosmosDbService>(),
                sp.GetRequiredService<IFaultMappingService>(),
                options,
                sp.GetRequiredService<ILogger<RepairPlannerAgent>>()
            );
        });

        return services;
    }
}
