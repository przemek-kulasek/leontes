using Leontes.Application.Vision;
using Leontes.Infrastructure.Vision;
using Leontes.Vision.Windows;
using Microsoft.Extensions.Options;

namespace Leontes.Api.Extensions;

public static class VisionExtensions
{
    public static IServiceCollection AddStructuralVision(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<VisionOptions>(configuration.GetSection(VisionOptions.SectionName));

        services.AddSingleton<ITreeSerializer, CompactMarkdownSerializer>();

        services.AddSingleton<IUITreeWalker>(sp => new UIAutomationTreeWalker(
            sp.GetRequiredService<IOptions<VisionOptions>>().Value,
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<UIAutomationTreeWalker>()));

        return services;
    }
}
