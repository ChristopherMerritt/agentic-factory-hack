using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RepairPlanner.Services;

/// <summary>
/// Extension methods for configuring Fault Mapping services.
/// </summary>
public static class FaultMappingServiceExtensions
{
    public static IServiceCollection UseFaultMappingService(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IFaultMappingService, FaultMappingService>();

        return services;
    }
}
