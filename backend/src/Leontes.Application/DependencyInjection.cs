using Microsoft.Extensions.DependencyInjection;

namespace Leontes.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
