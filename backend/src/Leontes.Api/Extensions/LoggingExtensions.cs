using Serilog;

namespace Leontes.Api.Extensions;

public static class LoggingExtensions
{
    public static WebApplicationBuilder AddApiLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, configuration) =>
            configuration.ReadFrom.Configuration(context.Configuration));

        return builder;
    }

    public static WebApplication UseApiLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging();
        return app;
    }
}
