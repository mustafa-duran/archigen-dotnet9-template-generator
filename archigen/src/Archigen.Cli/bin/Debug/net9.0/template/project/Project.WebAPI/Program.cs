using System.Reflection;

using Core.CrossCuttingConcerns.Exception.WebAPI.Extensions;
using Core.CrossCuttingConcerns.Logging.Configurations;
using Core.ElasticSearch.Models;
using Core.Localization.WebApi;
using Core.Mailing;
using Core.Persistence.WebApi;
using Core.Security.Encryption;
using Core.Security.JWT;
using Core.Security.WebApi.OpenApi.Extensions;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

using Project.Application;
using Project.Infrastructure;
using Project.Persistence;
using Project.WebAPI;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddAutoMapper(configuration =>
{
    configuration.LicenseKey = builder.Configuration["AutoMapperConfig:LicenseKey"]
    ?? throw new InvalidOperationException($"AutoMapperConfig section cannot found in configuration");
});

builder.Services.AddMediatR(configuration =>
{
    configuration.LicenseKey = builder.Configuration["MediatRConfig:LicenseKey"]
    ?? throw new InvalidOperationException($"MediatRConfig section cannot found in configuration");
    configuration.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
});

builder.Services.AddApplicationServices(
mailSettings: builder.Configuration.GetSection("MailSettings").Get<MailSettings>()
    ?? throw new InvalidOperationException("MailSettings section cannot found in configuration."),
fileLogConfiguration: builder.Configuration.GetSection("SeriLogConfigurations:FileLogConfiguration").Get<FileLogConfiguration>()
    ?? throw new InvalidOperationException("FileLogConfiguration section cannot found in configuration."),
elasticSearchConfig: builder.Configuration.GetSection("ElasticSearchConfig").Get<ElasticSearchConfig>()
    ?? throw new InvalidOperationException("ElasticSearchConfig section cannot found in configuration."),
tokenOptions: builder.Configuration.GetSection("TokenOptions").Get<TokenOptions>()
    ?? throw new InvalidOperationException("TokenOptions section cannot found in configuration.")
);

builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddInfrastructureServices();
builder.Services.AddHttpContextAccessor();

const string tokenOptionsConfigurationSection = "TokenOptions";
TokenOptions tokenOptions =
    builder.Configuration.GetSection(tokenOptionsConfigurationSection).Get<TokenOptions>()
    ?? throw new InvalidOperationException($"\"{tokenOptionsConfigurationSection}\" section cannot found in configuration.");
builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidIssuer = tokenOptions.Issuer,
            ValidAudience = tokenOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SecurityKeyHelper.CreateSecurityKey(tokenOptions.SecurityKey)
        };
    });

builder.Services.AddDistributedMemoryCache();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(param =>
    {
        param.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    })
);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecurityDocumentTransformer>();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
        .WithTitle("sampleproject Project")
        .WithTheme(ScalarTheme.Default)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

//if (app.Environment.IsProduction()) { }
app.ConfigureCustomExceptionMiddleware();

app.UseDbMigrationApplier();

//app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

const string webApiConfigurationSection = "WebAPIConfiguration";

WebApiConfiguration webApiConfiguration =
    app.Configuration.GetSection(webApiConfigurationSection).Get<WebApiConfiguration>()
    ?? throw new InvalidOperationException($"\"{webApiConfigurationSection}\" section cannot found in configuration.");

app.UseCors(opt => opt.WithOrigins(webApiConfiguration.AllowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials());

app.UseResponseLocalization();

app.Run();
