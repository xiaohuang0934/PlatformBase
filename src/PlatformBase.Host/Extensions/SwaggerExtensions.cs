using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PlatformBase.Host.Extensions;

/// <summary>
/// Swagger Bearer Token 安全定义 + 全局安全要求
/// Swashbuckle 6.x 使用 Microsoft.OpenApi.Models 命名空间
/// </summary>
public static class SwaggerExtensions
{
    public static SwaggerGenOptions AddJwtSecurity(
        this SwaggerGenOptions options)
    {
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT认证授权，粘贴 Bearer {token}（通过 POST /api/auth/login 获取）",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                new List<string>()
            }
        });

        return options;
    }
}
