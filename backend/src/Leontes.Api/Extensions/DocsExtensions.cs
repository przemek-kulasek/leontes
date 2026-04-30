using Scalar.AspNetCore;

namespace Leontes.Api.Extensions;

public static class DocsExtensions
{
    public static WebApplication MapApiDocs(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        return app;
    }
}
