using Microsoft.Extensions.DependencyInjection;

using Project.Application.Services.ImageService;
using Project.Infrastructure.Adapters.ImageService;

namespace Project.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<ImageServiceBase, CloudinaryImageServiceAdapter>();

        return services;
    }
}
