using System.Text;
using FiapGames.Shared.Infrastructure.Auth;
using FiapGames.Shared.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FiapGames.Shared.Infrastructure.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException($"Missing '{JwtSettings.SectionName}' configuration section.");

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenRevocationStore, InMemoryTokenRevocationStore>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            // Layered on top of the stateless signature/expiry check above —
            // a token that's otherwise valid can still be rejected if its
            // jti was revoked via /logout. See notes.md for why this is an
            // in-memory, per-pod store rather than a persisted one.
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var jti = context.Principal?.FindFirst("jti")?.Value;
                    if (jti is not null)
                    {
                        var store = context.HttpContext.RequestServices.GetRequiredService<ITokenRevocationStore>();
                        if (store.IsRevoked(jti))
                            context.Fail("Token has been revoked.");
                    }

                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization();

        return services;
    }
}
