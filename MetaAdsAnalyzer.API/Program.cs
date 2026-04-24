using System.Text;
using MetaAdsAnalyzer.API.Options;
using MetaAdsAnalyzer.API.Services;
using MetaAdsAnalyzer.Core.Entities;
using MetaAdsAnalyzer.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(
    options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddDataProtection();
builder.Services.AddSingleton<IMetaAccessTokenProtector, MetaAccessTokenProtector>();

builder.Services.Configure<MetaOptions>(builder.Configuration.GetSection(MetaOptions.SectionName));
builder.Services.Configure<MetaInsightsSchedulingOptions>(
    builder.Configuration.GetSection(MetaInsightsSchedulingOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var jwtSecret = jwtSection.SecretKey ?? string.Empty;
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
{
    throw new InvalidOperationException("Jwt:SecretKey appsettings içinde en az 32 karakter olmalıdır.");
}

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(
        options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                ValidateIssuer = true,
                ValidIssuer = jwtSection.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSection.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
            };
        });

builder.Services.AddAuthorization();

builder.Services.AddHttpClient<IMetaOAuthService, MetaOAuthService>();
builder.Services.AddHttpClient<IMetaInsightsSyncService, MetaInsightsSyncService>();

builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IPdfReportService, PdfReportService>();
builder.Services.AddScoped<IStripeBillingService, StripeBillingService>();

builder.Services.AddScoped<IMetricsComputationService, MetricsComputationService>();
builder.Services.AddScoped<IDirectiveEngineService, DirectiveEngineService>();
builder.Services.AddScoped<IVideoAssetSyncService, VideoAssetSyncService>();
builder.Services.AddScoped<IVideoReportInsightService, VideoReportInsightService>();
builder.Services.AddScoped<IDataQualityService, DataQualityService>();

builder.Services.AddHostedService<MetaInsightsSchedulingService>();
builder.Services.AddHostedService<SuggestionImpactMeasurementService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(
    c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "MetaAdsAnalyzer API", Version = "v1" });
        c.AddSecurityDefinition(
            "Bearer",
            new OpenApiSecurityScheme
            {
                Description = "Authorization: Bearer {token}",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
            });
        c.AddSecurityRequirement(
            new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                    },
                    Array.Empty<string>()
                },
            });
    });

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                  ?? Array.Empty<string>();
builder.Services.AddCors(
    options =>
    {
        options.AddPolicy(
            "Frontend",
            policy =>
            {
                if (corsOrigins.Length > 0)
                {
                    policy.WithOrigins(corsOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                }
                else
                {
                    // Tek kök (UI + /api aynı host) veya geliştirme: liste boşsa tüm köklere izin (JWT header ile).
                    policy.AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                }
            });
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseForwardedHeaders();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

var indexHtml = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "index.html");
// wwwroot/index.html olmasa bile: yanlış /api/* için JSON 404 (boş Content-Length yerine teşhis gövdesi).
// Doğru rota MapControllers ile eşleşir; buraya sadece hiçbir endpoint tutmayan istekler düşer.
app.MapFallback(
    async context =>
    {
        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response
                .WriteAsJsonAsync(
                    new
                    {
                        message =
                            "API yolu bulunamadı. Çalışan proje MetaAdsAnalyzer.API olmalı; Swagger’da rota var mı bakın. " +
                            "Yaygın nedenler: yanlış URL, eski API derlemesi, veya istemcide çift /api/api/ (VITE_API_BASE_URL sonu /api olmamalı; .env.example’e bakın). " +
                            "Örnek: GET /api/video-report/route-ping (JWT gerekmez), POST /api/video-report/aggregate.",
                        path = context.Request.Path.Value,
                        method = context.Request.Method,
                    },
                    cancellationToken: context.RequestAborted)
                .ConfigureAwait(false);
            return;
        }

        if (File.Exists(indexHtml))
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(indexHtml).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("Not found", cancellationToken: context.RequestAborted).ConfigureAwait(false);
    });

app.Run();
