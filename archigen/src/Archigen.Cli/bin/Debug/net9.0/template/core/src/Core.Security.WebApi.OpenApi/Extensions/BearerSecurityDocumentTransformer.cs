using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace Core.Security.WebApi.OpenApi.Extensions;

public class BearerSecurityDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, OpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Name = "Authorization",
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\""
        };

        document.SecurityRequirements ??= new List<OpenApiSecurityRequirement>();
        var bearerSchemeRef = new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        };
        document.SecurityRequirements.Add(new OpenApiSecurityRequirement
        {
            [bearerSchemeRef] = new string[] { }
        });

        return Task.CompletedTask;
    }
}
