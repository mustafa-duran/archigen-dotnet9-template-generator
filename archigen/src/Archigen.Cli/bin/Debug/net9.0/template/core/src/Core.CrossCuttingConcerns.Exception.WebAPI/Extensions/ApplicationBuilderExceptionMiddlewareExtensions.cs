using Core.CrossCuttingConcerns.Exception.WebAPI.Middleware;

using Microsoft.AspNetCore.Builder;

namespace Core.CrossCuttingConcerns.Exception.WebAPI.Extensions;

public static class ApplicationBuilderExceptionMiddlewareExtensions
{
    public static void ConfigureCustomExceptionMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<ExceptionMiddleware>();
    }
}