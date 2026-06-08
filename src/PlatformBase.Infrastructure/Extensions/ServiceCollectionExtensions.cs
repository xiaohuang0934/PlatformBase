using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlatformBase.Core;
using PlatformBase.Core.Repositories;
using PlatformBase.Infrastructure.Data;

namespace PlatformBase.Infrastructure.Extensions;

/// <summary>
/// 基础设施层DI注册扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册数据库上下文和工作单元
    /// 根据DatabaseProvider自动配置对应的EF Core数据库驱动
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="provider">数据库类型：Sqlite / SqlServer / MySql</param>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="enableSensitiveDataLogging">是否启用敏感数据日志（仅开发环境开启）</param>
    /// <returns></returns>
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        DatabaseProvider provider,
        string connectionString,
        bool enableSensitiveDataLogging = false)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            switch (provider)
            {
                case DatabaseProvider.SqlServer:
                    options.UseSqlServer(connectionString);
                    break;
                case DatabaseProvider.MySql:
                    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
                    break;
                case DatabaseProvider.Sqlite:
                default:
                    options.UseSqlite(connectionString);
                    break;
            }

            if (enableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        });

        // 注册工作单元，每次HTTP请求创建一个新实例
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
