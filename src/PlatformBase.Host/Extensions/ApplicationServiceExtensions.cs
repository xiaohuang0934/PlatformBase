using System.Reflection;
using PlatformBase.Application.Services;

namespace PlatformBase.Host.Extensions;

/// <summary>
/// 应用服务层 DI 自动注册扩展
/// 根据命名约定扫描 Application 层接口（I*Service）与 Host 层实现（*Service），统一注册为 Scoped
/// 后续新增业务模块只需遵循命名约定，无需修改 Program.cs
/// </summary>
public static class ApplicationServiceExtensions
{
    /// <summary>
    /// 自动扫描并注册所有 Application 层服务接口的实现
    /// 匹配规则：I{Name}Service（Application 层）→ {Name}Service（Host 层），注册为 Scoped
    /// 跳过泛型接口（如 ICrudService&lt;&gt;）
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var hostAssembly = Assembly.GetExecutingAssembly();
        var applicationAssembly = typeof(IUserService).Assembly;

        var serviceInterfaces = applicationAssembly
            .GetTypes()
            .Where(t => t.IsInterface
                        && t.Name.StartsWith("I")
                        && t.Name.EndsWith("Service")
                        && !t.IsGenericType);

        foreach (var iface in serviceInterfaces)
        {
            var implName = iface.Name[1..];
            var impl = hostAssembly
                .GetTypes()
                .FirstOrDefault(t => t.IsClass && !t.IsAbstract && t.Name == implName && iface.IsAssignableFrom(t));

            if (impl != null)
                services.AddScoped(iface, impl);
        }

        return services;
    }
}
