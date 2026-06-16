using Microsoft.EntityFrameworkCore;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Repositories;
using PlatformBase.Infrastructure.Data;

namespace PlatformBase.Host.Extensions;

/// <summary>
/// 应用启动种子数据初始化，所有操作均为幂等（已存在则跳过）
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(this IHost app)
    {
        using var scope = app.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await context.Database.EnsureCreatedAsync();

        // 种子顺序：平台用户 → 角色 → 权限 → 角色-权限 → 系统参数 → 数据字典 → 通知模板 → 租户 → 组织架构 → 租户用户 → 用户-部门 → 菜单 → 演示数据
        var adminId = await SeedPlatformUsersAsync(uow, context);
        await SeedRolesAsync(uow, adminId);
        await SeedPermissionsAsync(uow, adminId);
        await SeedRolePermissionsAsync(uow, context, adminId);
        await SeedSystemParamsAsync(uow, context, adminId);
        await SeedDataDictAsync(uow, context, adminId);
        await SeedNotificationTemplatesAsync(uow, context, adminId);
        var (defaultTenantId, zhijihuiTenantId) = await SeedTenantsAsync(uow, context, adminId);
        var orgMaps = await SeedOrganizationUnitsAsync(uow, context, adminId, defaultTenantId, zhijihuiTenantId);
        var userMaps = await SeedTenantUsersAsync(uow, context, adminId, defaultTenantId, zhijihuiTenantId);
        await SeedUserOrganizationsAsync(context, userMaps, orgMaps);
        await SeedMenusAsync(uow, context, adminId);
        await SeedDemoDataAsync(uow, context, userMaps);

        scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DataSeeder")
            .LogInformation("种子数据初始化完成");
    }

    private static string Norm(string value) => (value ?? string.Empty).ToUpperInvariant();

    // ═══════════════════ 平台用户 ═══════════════════

    private static async Task<Guid> SeedPlatformUsersAsync(IUnitOfWork uow, AppDbContext context)
    {
        var adminId = Guid.Empty;

        // 创建超级管理员 admin
        var adminExists = await uow.Repository<User>()
            .FirstOrDefaultAsync(u => u.NormalizedUsername == Norm("admin"));
        if (adminExists == null)
        {
            var deletedAdmin = await context.Set<User>().IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.NormalizedUsername == Norm("admin") && u.IsDeleted);
            if (deletedAdmin != null)
            {
                adminId = deletedAdmin.Id;
            }
            else
            {
                var admin = new User
                {
                    Username = "admin",
                    NormalizedUsername = Norm("admin"),
                    Email = "admin@platformbase.com",
                    NormalizedEmail = Norm("admin@platformbase.com"),
                    EmailConfirmed = true,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    IsActive = true,
                    IsSuperAdmin = true,
                    UserType = UserType.PlatformAdmin
                };
                var created = await uow.Repository<User>().AddAsync(admin);
                await uow.SaveChangesAsync();
                adminId = created.Id;
            }
        }
        else
        {
            adminId = adminExists.Id;
        }

        // 创建平台运维用户 platform_ops
        var opsExists = await uow.Repository<User>()
            .AnyAsync(u => u.NormalizedUsername == Norm("platform_ops"));
        if (!opsExists)
        {
            var deletedOps = await context.Set<User>().IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.NormalizedUsername == Norm("platform_ops") && u.IsDeleted);
            if (deletedOps == null)
            {
                var ops = new User
                {
                    Username = "platform_ops",
                    NormalizedUsername = Norm("platform_ops"),
                    Email = "ops@platformbase.com",
                    NormalizedEmail = Norm("ops@platformbase.com"),
                    EmailConfirmed = true,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Ops@123"),
                    IsActive = true,
                    IsSuperAdmin = false,
                    UserType = UserType.PlatformAdmin,
                    CreatedBy = adminId
                };
                await uow.Repository<User>().AddAsync(ops);
                await uow.SaveChangesAsync();
            }
        }

        return adminId;
    }

    // ═══════════════════ 角色 ═══════════════════

    private static async Task SeedRolesAsync(IUnitOfWork uow, Guid createdBy)
    {
        var existing = await uow.Repository<Role>().GetAllAsync();
        var existingNames = existing.Select(r => r.NormalizedName).ToHashSet();

        foreach (var (name, code, desc, isSystem) in GetSeedRoles())
        {
            if (existingNames.Contains(Norm(name))) continue;
            await uow.Repository<Role>().AddAsync(new Role
            {
                Name = name,
                Code = code,
                NormalizedName = Norm(name),
                Description = desc,
                IsSystem = isSystem,
                TenantId = null,
                CreatedBy = createdBy
            });
        }
        await uow.SaveChangesAsync();
    }

    private static (string Name, string Code, string Description, bool IsSystem)[] GetSeedRoles() =>
    [
        ("Admin", "admin", "系统管理员 — 拥有全部权限（仅平台管理员）", true),
        ("TenantAdmin", "tenant_admin", "租户管理员 — 租户内全部权限（不含租户管理、系统参数、系统监控）", true),
        ("Manager", "manager", "业务管理员 — 用户和角色查看", true),
        ("User", "user", "普通用户 — 最小权限", true)
    ];

    // ═══════════════════ 权限 ═══════════════════

    private static async Task SeedPermissionsAsync(IUnitOfWork uow, Guid createdBy)
    {
        var existing = await uow.Repository<Permission>().GetAllAsync();
        var existingCodes = existing.Select(p => p.Code).ToHashSet();

        foreach (var perm in GetSeedPermissions())
        {
            if (existingCodes.Contains(perm.Code)) continue;
            perm.CreatedBy = createdBy;
            await uow.Repository<Permission>().AddAsync(perm);
        }
        await uow.SaveChangesAsync();
    }

    private static Permission[] GetSeedPermissions() =>
    [
        new() { Code = "users.list", Name = "用户列表", ResourcePath = "/api/v1/users", HttpMethod = "GET", GroupName = "用户管理", SortOrder = 1, IsEnabled = true },
        new() { Code = "users.create", Name = "创建用户", ResourcePath = "/api/v1/users", HttpMethod = "POST", GroupName = "用户管理", SortOrder = 2, IsEnabled = true },
        new() { Code = "users.edit", Name = "编辑用户", ResourcePath = "/api/v1/users", HttpMethod = "PUT", GroupName = "用户管理", SortOrder = 3, IsEnabled = true },
        new() { Code = "users.delete", Name = "删除用户", ResourcePath = "/api/v1/users", HttpMethod = "DELETE", GroupName = "用户管理", SortOrder = 4, IsEnabled = true },
        new() { Code = "roles.list", Name = "角色列表", ResourcePath = "/api/v1/roles", HttpMethod = "GET", GroupName = "角色管理", SortOrder = 1, IsEnabled = true },
        new() { Code = "roles.create", Name = "创建角色", ResourcePath = "/api/v1/roles", HttpMethod = "POST", GroupName = "角色管理", SortOrder = 2, IsEnabled = true },
        new() { Code = "roles.edit", Name = "编辑角色", ResourcePath = "/api/v1/roles", HttpMethod = "PUT", GroupName = "角色管理", SortOrder = 3, IsEnabled = true },
        new() { Code = "roles.delete", Name = "删除角色", ResourcePath = "/api/v1/roles", HttpMethod = "DELETE", GroupName = "角色管理", SortOrder = 4, IsEnabled = true },
        new() { Code = "perms.list", Name = "权限列表", ResourcePath = "/api/v1/permissions", HttpMethod = "GET", GroupName = "权限管理", SortOrder = 1, IsEnabled = true },
        new() { Code = "perms.create", Name = "创建权限", ResourcePath = "/api/v1/permissions", HttpMethod = "POST", GroupName = "权限管理", SortOrder = 2, IsEnabled = true },
        new() { Code = "perms.edit", Name = "编辑权限", ResourcePath = "/api/v1/permissions", HttpMethod = "PUT", GroupName = "权限管理", SortOrder = 3, IsEnabled = true },
        new() { Code = "perms.delete", Name = "删除权限", ResourcePath = "/api/v1/permissions", HttpMethod = "DELETE", GroupName = "权限管理", SortOrder = 4, IsEnabled = true },
        new() { Code = "system-params.list", Name = "系统参数列表", ResourcePath = "/api/v1/system-params", HttpMethod = "GET", GroupName = "系统管理", SortOrder = 1, IsEnabled = true },
        new() { Code = "system-params.create", Name = "创建系统参数", ResourcePath = "/api/v1/system-params", HttpMethod = "POST", GroupName = "系统管理", SortOrder = 2, IsEnabled = true },
        new() { Code = "system-params.edit", Name = "编辑系统参数", ResourcePath = "/api/v1/system-params", HttpMethod = "PUT", GroupName = "系统管理", SortOrder = 3, IsEnabled = true },
        new() { Code = "system-params.delete", Name = "删除系统参数", ResourcePath = "/api/v1/system-params", HttpMethod = "DELETE", GroupName = "系统管理", SortOrder = 4, IsEnabled = true },
        new() { Code = "datadict.list", Name = "字典列表", ResourcePath = "/api/v1/data-dict", HttpMethod = "GET", GroupName = "系统管理", SortOrder = 5, IsEnabled = true },
        new() { Code = "datadict.create", Name = "创建字典", ResourcePath = "/api/v1/data-dict", HttpMethod = "POST", GroupName = "系统管理", SortOrder = 6, IsEnabled = true },
        new() { Code = "datadict.edit", Name = "编辑字典", ResourcePath = "/api/v1/data-dict", HttpMethod = "PUT", GroupName = "系统管理", SortOrder = 7, IsEnabled = true },
        new() { Code = "datadict.delete", Name = "删除字典", ResourcePath = "/api/v1/data-dict", HttpMethod = "DELETE", GroupName = "系统管理", SortOrder = 8, IsEnabled = true },
        new() { Code = "jobs.list", Name = "任务列表", ResourcePath = "/api/v1/jobs", HttpMethod = "GET", GroupName = "系统管理", SortOrder = 9, IsEnabled = true },
        new() { Code = "jobs.manage", Name = "任务管理", ResourcePath = "/api/v1/jobs", HttpMethod = "POST", GroupName = "系统管理", SortOrder = 10, IsEnabled = true },
        new() { Code = "operation-logs.list", Name = "操作日志列表", ResourcePath = "/api/v1/operation-logs", HttpMethod = "GET", GroupName = "系统管理", SortOrder = 11, IsEnabled = true },
        new() { Code = "files.upload", Name = "文件管理", ResourcePath = "/api/v1/files", HttpMethod = "POST", GroupName = "系统管理", SortOrder = 12, IsEnabled = true },
        new() { Code = "tenants.list", Name = "租户列表", ResourcePath = "/api/v1/tenants", HttpMethod = "GET", GroupName = "多租户", SortOrder = 1, IsEnabled = true },
        new() { Code = "tenants.create", Name = "创建租户", ResourcePath = "/api/v1/tenants", HttpMethod = "POST", GroupName = "多租户", SortOrder = 2, IsEnabled = true },
        new() { Code = "tenants.edit", Name = "编辑租户", ResourcePath = "/api/v1/tenants", HttpMethod = "PUT", GroupName = "多租户", SortOrder = 3, IsEnabled = true },
        new() { Code = "tenants.delete", Name = "删除租户", ResourcePath = "/api/v1/tenants", HttpMethod = "DELETE", GroupName = "多租户", SortOrder = 4, IsEnabled = true },
        new() { Code = "tenant-params.list", Name = "租户参数列表", ResourcePath = "/api/v1/tenant-params", HttpMethod = "GET", GroupName = "多租户", SortOrder = 5, IsEnabled = true },
        new() { Code = "tenant-params.create", Name = "创建租户参数", ResourcePath = "/api/v1/tenant-params", HttpMethod = "POST", GroupName = "多租户", SortOrder = 6, IsEnabled = true },
        new() { Code = "tenant-params.edit", Name = "编辑租户参数", ResourcePath = "/api/v1/tenant-params", HttpMethod = "PUT", GroupName = "多租户", SortOrder = 7, IsEnabled = true },
        new() { Code = "tenant-params.delete", Name = "删除租户参数", ResourcePath = "/api/v1/tenant-params", HttpMethod = "DELETE", GroupName = "多租户", SortOrder = 8, IsEnabled = true },
        new() { Code = "org-units.list", Name = "组织架构列表", ResourcePath = "/api/v1/organization-units", HttpMethod = "GET", GroupName = "组织架构", SortOrder = 1, IsEnabled = true },
        new() { Code = "org-units.create", Name = "创建部门", ResourcePath = "/api/v1/organization-units", HttpMethod = "POST", GroupName = "组织架构", SortOrder = 2, IsEnabled = true },
        new() { Code = "org-units.edit", Name = "编辑部门", ResourcePath = "/api/v1/organization-units", HttpMethod = "PUT", GroupName = "组织架构", SortOrder = 3, IsEnabled = true },
        new() { Code = "org-units.delete", Name = "删除部门", ResourcePath = "/api/v1/organization-units", HttpMethod = "DELETE", GroupName = "组织架构", SortOrder = 4, IsEnabled = true },
        new() { Code = "menus.list", Name = "菜单列表", ResourcePath = "/api/v1/menus", HttpMethod = "GET", GroupName = "菜单管理", SortOrder = 1, IsEnabled = true },
        new() { Code = "menus.create", Name = "创建菜单", ResourcePath = "/api/v1/menus", HttpMethod = "POST", GroupName = "菜单管理", SortOrder = 2, IsEnabled = true },
        new() { Code = "menus.edit", Name = "编辑菜单", ResourcePath = "/api/v1/menus", HttpMethod = "PUT", GroupName = "菜单管理", SortOrder = 3, IsEnabled = true },
        new() { Code = "menus.delete", Name = "删除菜单", ResourcePath = "/api/v1/menus", HttpMethod = "DELETE", GroupName = "菜单管理", SortOrder = 4, IsEnabled = true },
        new() { Code = "notifications.manage", Name = "通知管理", ResourcePath = "/api/v1/notifications", HttpMethod = "POST", GroupName = "系统管理", SortOrder = 13, IsEnabled = true }
    ];

    // ═══════════════════ 角色-权限关联 ═══════════════════

    private static async Task SeedRolePermissionsAsync(IUnitOfWork uow, AppDbContext context, Guid createdBy)
    {
        var roles = await uow.Repository<Role>().GetAllAsync();
        var perms = await uow.Repository<Permission>().GetAllAsync();
        var existingRps = await context.Set<RolePermission>().ToListAsync();

        var roleMap = roles.ToDictionary(r => r.Name, r => r.Id);
        var permMap = perms.ToDictionary(p => p.Code, p => p.Id);

        foreach (var (roleName, permCodes) in GetSeedRolePermissions())
        {
            if (!roleMap.TryGetValue(roleName, out var roleId)) continue;
            foreach (var code in permCodes)
            {
                if (!permMap.TryGetValue(code, out var permId)) continue;
                if (existingRps.Any(rp => rp.RoleId == roleId && rp.PermissionId == permId)) continue;
                context.Set<RolePermission>().Add(new RolePermission { RoleId = roleId, PermissionId = permId });
            }
        }
        await context.SaveChangesAsync();

        // 给 platform_ops 分配 Manager 角色
        var ops = await uow.Repository<User>()
            .FirstOrDefaultAsync(u => u.NormalizedUsername == Norm("platform_ops"));
        if (ops != null && roleMap.TryGetValue("Manager", out var managerRoleId))
        {
            var opsRoleExists = await context.Set<UserRole>()
                .AnyAsync(ur => ur.UserId == ops.Id && ur.RoleId == managerRoleId);
            if (!opsRoleExists)
            {
                context.Set<UserRole>().Add(new UserRole { UserId = ops.Id, RoleId = managerRoleId });
                await context.SaveChangesAsync();
            }
        }

        // 给 admin 分配 Admin 角色（平台管理员）
        var admin = await uow.Repository<User>()
            .FirstOrDefaultAsync(u => u.NormalizedUsername == Norm("admin"));
        if (admin != null && roleMap.TryGetValue("Admin", out var adminRoleId))
        {
            var adminRoleExists = await context.Set<UserRole>()
                .AnyAsync(ur => ur.UserId == admin.Id && ur.RoleId == adminRoleId);
            if (!adminRoleExists)
            {
                context.Set<UserRole>().Add(new UserRole { UserId = admin.Id, RoleId = adminRoleId });
                await context.SaveChangesAsync();
            }
        }

        // 给租户管理员分配 TenantAdmin 角色
        var tenantAdmins = await uow.Repository<User>()
            .FindAsync(u => u.UserType == UserType.TenantAdmin);
        if (roleMap.TryGetValue("TenantAdmin", out var tenantAdminRoleId))
        {
            foreach (var ta in tenantAdmins)
            {
                var exists = await context.Set<UserRole>()
                    .AnyAsync(ur => ur.UserId == ta.Id && ur.RoleId == tenantAdminRoleId);
                if (!exists)
                {
                    context.Set<UserRole>().Add(new UserRole { UserId = ta.Id, RoleId = tenantAdminRoleId });
                }
            }
            await context.SaveChangesAsync();
        }
    }

    private static (string RoleName, string[] PermCodes)[] GetSeedRolePermissions() =>
    [
        // Admin 角色：平台管理员专用，拥有全部权限
        ("Admin", ["users.list", "users.create", "users.edit", "users.delete",
                   "roles.list", "roles.create", "roles.edit", "roles.delete",
                   "perms.list", "perms.create", "perms.edit", "perms.delete",
                   "system-params.list", "system-params.create", "system-params.edit", "system-params.delete",
                   "datadict.list", "datadict.create", "datadict.edit", "datadict.delete",
                   "jobs.list", "jobs.manage", "operation-logs.list", "files.upload",
                   "tenants.list", "tenants.create", "tenants.edit", "tenants.delete",
                   "tenant-params.list", "tenant-params.create", "tenant-params.edit", "tenant-params.delete",
                   "org-units.list", "org-units.create", "org-units.edit", "org-units.delete",
                   "menus.list", "menus.create", "menus.edit", "menus.delete",
                   "notifications.manage"]),
        // TenantAdmin 角色：租户管理员专用，不含租户管理、系统参数、权限管理、系统监控、菜单管理
        ("TenantAdmin", ["users.list", "users.create", "users.edit", "users.delete",
                         "roles.list", "roles.create", "roles.edit", "roles.delete",
                         "datadict.list", "datadict.create", "datadict.edit", "datadict.delete",
                         "tenant-params.list", "tenant-params.create", "tenant-params.edit", "tenant-params.delete",
                         "org-units.list", "org-units.create", "org-units.edit", "org-units.delete",
                         "files.upload", "notifications.manage"]),
        // Manager 角色：业务管理员，仅查看
        ("Manager", ["users.list", "roles.list", "perms.list"]),
        // User 角色：普通用户，最小权限
        ("User", [])
    ];

    // ═══════════════════ 系统参数 ═══════════════════

    private static async Task SeedSystemParamsAsync(IUnitOfWork uow, AppDbContext context, Guid createdBy)
    {
        var existingCodes = (await uow.Repository<SystemParam>().GetAllAsync())
            .Select(p => p.Code).ToHashSet();

        var deletedCodes = await context.Set<SystemParam>().IgnoreQueryFilters()
            .Where(p => p.IsDeleted)
            .Select(p => p.Code).ToListAsync();
        foreach (var code in deletedCodes) existingCodes.Add(code);

        foreach (var seed in GetSeedSystemParams())
        {
            if (existingCodes.Contains(seed.Code)) continue;
            seed.CreatedBy = createdBy;
            await uow.Repository<SystemParam>().AddAsync(seed);
        }
        await uow.SaveChangesAsync();
    }

    private static SystemParam[] GetSeedSystemParams() =>
    [
        // general 分类：全部可继承（租户可为不同名称/配置）
        new() { Code = "site_name", Name = "站点名称", Value = "PlatformBase", Category = "general", SortOrder = 1, Inheritable = true, Description = "站点/应用名称，前端页面标题等位置使用" },
        new() { Code = "company_name", Name = "公司名称", Value = "PlatformBase", Category = "general", SortOrder = 2, Inheritable = true, Description = "系统所属公司名称" },
        new() { Code = "default_page_size", Name = "默认分页大小", Value = "20", Category = "general", SortOrder = 3, Inheritable = true, Description = "列表分页的默认每页条数" },
        new() { Code = "max_export_rows", Name = "最大导出行数", Value = "10000", Category = "general", SortOrder = 4, Inheritable = true, Description = "导出数据的最大行数限制" },
        new() { Code = "log_retention_days", Name = "日志保留天数", Value = "90", Category = "general", SortOrder = 5, Inheritable = true, Description = "操作日志保留天数" },
        // security 分类：平台级安全策略，不可继承
        new() { Code = "max_login_attempts", Name = "最大登录失败次数", Value = "5", Category = "security", SortOrder = 1, Inheritable = false, Description = "连续登录失败达到此次数后锁定账户" },
        new() { Code = "lockout_minutes", Name = "锁定分钟数", Value = "5", Category = "security", SortOrder = 2, Inheritable = false, Description = "账户被锁定后自动解锁的分钟数" },
        new() { Code = "access_token_lifetime", Name = "Token有效期(秒)", Value = "300", Category = "security", SortOrder = 3, Inheritable = false, Description = "AccessToken 签发的有效时长" },
        new() { Code = "refresh_token_days", Name = "RefreshToken有效期(天)", Value = "30", Category = "security", SortOrder = 4, Inheritable = false, Description = "RefreshToken 的有效天数" },
        new() { Code = "super_admin_username", Name = "超级管理员用户名", Value = "admin", Category = "security", SortOrder = 5, Inheritable = false, Description = "系统超级管理员用户名标识" },
        // feature-toggle 分类：平台级功能开关，不可继承
        new() { Code = "enable_register", Name = "开放注册", Value = "true", Category = "feature-toggle", SortOrder = 1, Inheritable = false, Description = "是否允许新用户自行注册" },
        new() { Code = "enable_captcha", Name = "验证码开关", Value = "false", Category = "feature-toggle", SortOrder = 2, Inheritable = false, Description = "登录/注册时是否启用验证码校验" },
        new() { Code = "maintenance_mode", Name = "维护模式", Value = "false", Category = "feature-toggle", SortOrder = 3, Inheritable = false, Description = "开启后仅管理员可访问系统" },
        new() { Code = "enable_multi_tenant", Name = "启用多租户", Value = "true", Category = "feature-toggle", SortOrder = 4, Inheritable = false, Description = "是否启用多租户功能" },
        new() { Code = "enable_org_unit", Name = "启用组织架构", Value = "true", Category = "feature-toggle", SortOrder = 5, Inheritable = false, Description = "是否启用组织架构功能" },
        // data-scope：数据可见性，租户可自定义
        new() { Code = "org_null_data_visibility", Name = "未归属部门数据可见性", Value = "all", Category = "data-scope", SortOrder = 1, Inheritable = true, Description = "未归属部门数据的可见性配置：all=租户内全部可见，admin_only=仅管理员可见" },
        // tenant 分类：平台级租户管理参数，不可继承
        new() { Code = "default_tenant_quota", Name = "默认租户配额", Value = "100", Category = "tenant", SortOrder = 1, Inheritable = false, Description = "新租户默认用户数量上限" },
        new() { Code = "tenant_trial_days", Name = "试用天数", Value = "30", Category = "tenant", SortOrder = 2, Inheritable = false, Description = "新租户试用期天数" },
        // smtp：平台级邮件配置，不可继承
        new() { Code = "smtp:default", Name = "SMTP邮件配置", Value = "{\"Host\":\"\",\"Port\":587,\"User\":\"\",\"Password\":\"\",\"From\":\"\"}", Category = "smtp", SortOrder = 1, Inheritable = false, Description = "SMTP 邮件服务器配置（JSON）" }
    ];

    // ═══════════════════ 数据字典 ═══════════════════

    private static async Task SeedDataDictAsync(IUnitOfWork uow, AppDbContext context, Guid createdBy)
    {
        var existingTypeCodes = (await uow.Repository<DataDictType>().GetAllAsync())
            .Select(t => t.TypeCode).ToHashSet();

        var typeIds = new Dictionary<string, Guid>();

        foreach (var seed in GetSeedDictTypes())
        {
            if (existingTypeCodes.Contains(seed.TypeCode))
            {
                var existingType = await uow.Repository<DataDictType>()
                    .FirstOrDefaultAsync(t => t.TypeCode == seed.TypeCode);
                if (existingType != null) typeIds[seed.TypeCode] = existingType.Id;
                continue;
            }
            seed.CreatedBy = createdBy;
            var created = await uow.Repository<DataDictType>().AddAsync(seed);
            await uow.SaveChangesAsync();
            typeIds[seed.TypeCode] = created.Id;
        }

        foreach (var seed in GetSeedDictItems(typeIds))
        {
            if (seed.DictTypeId == Guid.Empty) continue;
            var exists = await context.Set<DataDictItem>()
                .IgnoreQueryFilters()
                .AnyAsync(i => i.DictTypeId == seed.DictTypeId && i.ItemCode == seed.ItemCode);
            if (exists) continue;
            seed.CreatedBy = createdBy;
            context.Set<DataDictItem>().Add(seed);
        }
        await context.SaveChangesAsync();
    }

    private static DataDictType[] GetSeedDictTypes() =>
    [
        new() { TypeCode = "gender", TypeName = "性别", SortOrder = 1, Description = "用户性别选项" },
        new() { TypeCode = "user_status", TypeName = "用户状态", SortOrder = 2, Description = "系统用户状态枚举" },
        new() { TypeCode = "enabled_status", TypeName = "启用状态", SortOrder = 3, Description = "通用启用/禁用状态" },
        new() { TypeCode = "user_type", TypeName = "用户类型", SortOrder = 4, Description = "平台管理员/租户管理员/租户用户" },
        new() { TypeCode = "tenant_status", TypeName = "租户状态", SortOrder = 5, Description = "租户运行状态" },
        new() { TypeCode = "org_type", TypeName = "组织类型", SortOrder = 6, Description = "公司/中心/部门/小组/岗位" },
        new() { TypeCode = "menu_type", TypeName = "菜单类型", SortOrder = 7, Description = "目录/页面/按钮" },
        new() { TypeCode = "notification_channel", TypeName = "通知渠道", SortOrder = 8, Description = "站内信/邮件/短信" },
        new() { TypeCode = "priority_level", TypeName = "优先级", SortOrder = 9, Description = "高/中/低" },
        new() { TypeCode = "task_status", TypeName = "任务状态", SortOrder = 10, Description = "待处理/进行中/已完成/已取消" }
    ];

    private static DataDictItem[] GetSeedDictItems(Dictionary<string, Guid> typeIds) =>
    [
        // gender
        new() { DictTypeId = typeIds.GetValueOrDefault("gender", Guid.Empty), ItemCode = "male", ItemName = "男", SortOrder = 1 },
        new() { DictTypeId = typeIds.GetValueOrDefault("gender", Guid.Empty), ItemCode = "female", ItemName = "女", SortOrder = 2 },
        new() { DictTypeId = typeIds.GetValueOrDefault("gender", Guid.Empty), ItemCode = "other", ItemName = "其他", SortOrder = 3 },
        // user_status
        new() { DictTypeId = typeIds.GetValueOrDefault("user_status", Guid.Empty), ItemCode = "activated", ItemName = "正常", SortOrder = 1 },
        new() { DictTypeId = typeIds.GetValueOrDefault("user_status", Guid.Empty), ItemCode = "inactivated", ItemName = "未激活", SortOrder = 2 },
        new() { DictTypeId = typeIds.GetValueOrDefault("user_status", Guid.Empty), ItemCode = "disabled", ItemName = "停用", SortOrder = 3 },
        new() { DictTypeId = typeIds.GetValueOrDefault("user_status", Guid.Empty), ItemCode = "locked", ItemName = "已锁定", SortOrder = 4 },
        // enabled_status
        new() { DictTypeId = typeIds.GetValueOrDefault("enabled_status", Guid.Empty), ItemCode = "enabled", ItemName = "启用", SortOrder = 1 },
        new() { DictTypeId = typeIds.GetValueOrDefault("enabled_status", Guid.Empty), ItemCode = "disabled", ItemName = "禁用", SortOrder = 2 },
        // user_type
        new() { DictTypeId = typeIds.GetValueOrDefault("user_type", Guid.Empty), ItemCode = "platform_admin", ItemName = "平台管理员", SortOrder = 1 },
        new() { DictTypeId = typeIds.GetValueOrDefault("user_type", Guid.Empty), ItemCode = "tenant_admin", ItemName = "租户管理员", SortOrder = 2 },
        new() { DictTypeId = typeIds.GetValueOrDefault("user_type", Guid.Empty), ItemCode = "tenant_user", ItemName = "租户用户", SortOrder = 3 },
        // tenant_status
        new() { DictTypeId = typeIds.GetValueOrDefault("tenant_status", Guid.Empty), ItemCode = "normal", ItemName = "正常", SortOrder = 1 },
        new() { DictTypeId = typeIds.GetValueOrDefault("tenant_status", Guid.Empty), ItemCode = "trial", ItemName = "试用", SortOrder = 2 },
        new() { DictTypeId = typeIds.GetValueOrDefault("tenant_status", Guid.Empty), ItemCode = "overdue", ItemName = "欠费", SortOrder = 3 },
        new() { DictTypeId = typeIds.GetValueOrDefault("tenant_status", Guid.Empty), ItemCode = "suspended", ItemName = "停用", SortOrder = 4 },
        // org_type
        new() { DictTypeId = typeIds.GetValueOrDefault("org_type", Guid.Empty), ItemCode = "company", ItemName = "公司", SortOrder = 1 },
        new() { DictTypeId = typeIds.GetValueOrDefault("org_type", Guid.Empty), ItemCode = "center", ItemName = "中心", SortOrder = 2 },
        new() { DictTypeId = typeIds.GetValueOrDefault("org_type", Guid.Empty), ItemCode = "department", ItemName = "部门", SortOrder = 3 },
        new() { DictTypeId = typeIds.GetValueOrDefault("org_type", Guid.Empty), ItemCode = "group", ItemName = "小组", SortOrder = 4 },
        new() { DictTypeId = typeIds.GetValueOrDefault("org_type", Guid.Empty), ItemCode = "position", ItemName = "岗位", SortOrder = 5 },
        // menu_type
        new() { DictTypeId = typeIds.GetValueOrDefault("menu_type", Guid.Empty), ItemCode = "directory", ItemName = "目录", SortOrder = 1 },
        new() { DictTypeId = typeIds.GetValueOrDefault("menu_type", Guid.Empty), ItemCode = "page", ItemName = "页面", SortOrder = 2 },
        new() { DictTypeId = typeIds.GetValueOrDefault("menu_type", Guid.Empty), ItemCode = "button", ItemName = "按钮", SortOrder = 3 },
        // notification_channel
        new() { DictTypeId = typeIds.GetValueOrDefault("notification_channel", Guid.Empty), ItemCode = "in_app", ItemName = "站内信", SortOrder = 1 },
        new() { DictTypeId = typeIds.GetValueOrDefault("notification_channel", Guid.Empty), ItemCode = "email", ItemName = "邮件", SortOrder = 2 },
        new() { DictTypeId = typeIds.GetValueOrDefault("notification_channel", Guid.Empty), ItemCode = "sms", ItemName = "短信", SortOrder = 3 },
        // priority_level
        new() { DictTypeId = typeIds.GetValueOrDefault("priority_level", Guid.Empty), ItemCode = "high", ItemName = "高", SortOrder = 1 },
        new() { DictTypeId = typeIds.GetValueOrDefault("priority_level", Guid.Empty), ItemCode = "medium", ItemName = "中", SortOrder = 2 },
        new() { DictTypeId = typeIds.GetValueOrDefault("priority_level", Guid.Empty), ItemCode = "low", ItemName = "低", SortOrder = 3 },
        // task_status
        new() { DictTypeId = typeIds.GetValueOrDefault("task_status", Guid.Empty), ItemCode = "pending", ItemName = "待处理", SortOrder = 1 },
        new() { DictTypeId = typeIds.GetValueOrDefault("task_status", Guid.Empty), ItemCode = "in_progress", ItemName = "进行中", SortOrder = 2 },
        new() { DictTypeId = typeIds.GetValueOrDefault("task_status", Guid.Empty), ItemCode = "completed", ItemName = "已完成", SortOrder = 3 },
        new() { DictTypeId = typeIds.GetValueOrDefault("task_status", Guid.Empty), ItemCode = "cancelled", ItemName = "已取消", SortOrder = 4 }
    ];

    // ═══════════════════ 通知模板 ═══════════════════

    private static async Task SeedNotificationTemplatesAsync(IUnitOfWork uow, AppDbContext context, Guid createdBy)
    {
        var existingCodes = (await context.Set<NotificationTemplate>().IgnoreQueryFilters().ToListAsync())
            .Select(t => t.Code).ToHashSet();

        foreach (var seed in GetSeedNotificationTemplates())
        {
            if (existingCodes.Contains(seed.Code)) continue;
            seed.CreatedBy = createdBy;
            await uow.Repository<NotificationTemplate>().AddAsync(seed);
        }
        await uow.SaveChangesAsync();
    }

    private static NotificationTemplate[] GetSeedNotificationTemplates() =>
    [
        new() { Code = "welcome", Name = "欢迎注册", TitleTemplate = "欢迎加入 {siteName}",
                BodyTemplate = "尊敬的 {username}：您已成功注册，请登录系统完成信息设置。",
                Channel = "in_app", Variables = "username,siteName", IsEnabled = true },
        new() { Code = "password_changed", Name = "密码修改", TitleTemplate = "密码修改通知",
                BodyTemplate = "尊敬的用户：您的登录密码已于 {time} 被修改。如非本人操作，请联系管理员。",
                Channel = "in_app", Variables = "time", IsEnabled = true },
        new() { Code = "account_locked", Name = "账户锁定", TitleTemplate = "账户锁定通知",
                BodyTemplate = "尊敬的用户：您的账户因 {failedCount} 次登录失败已被锁定 {lockMinutes} 分钟。",
                Channel = "in_app", Variables = "failedCount,lockMinutes", IsEnabled = true },
        new() { Code = "tenant_created", Name = "租户创建", TitleTemplate = "租户创建成功",
                BodyTemplate = "租户 {tenantName} 已创建成功，有效期至 {expireDate}。",
                Channel = "in_app", Variables = "tenantName,expireDate", IsEnabled = true },
        new() { Code = "tenant_expired", Name = "租户到期", TitleTemplate = "租户即将到期提醒",
                BodyTemplate = "租户 {tenantName} 将于 {expireDate} 到期，请及时续费。",
                Channel = "email", Variables = "tenantName,expireDate", IsEnabled = true },
        new() { Code = "role_assigned", Name = "角色分配", TitleTemplate = "您已被分配新角色",
                BodyTemplate = "尊敬的 {username}：您已被分配 {roleName} 角色。",
                Channel = "in_app", Variables = "username,roleName", IsEnabled = true },
        new() { Code = "org_joined", Name = "加入部门", TitleTemplate = "您已加入新部门",
                BodyTemplate = "尊敬的 {username}：您已加入 {orgName} 部门。",
                Channel = "in_app", Variables = "username,orgName", IsEnabled = true },
        new() { Code = "system_maintenance", Name = "系统维护", TitleTemplate = "系统维护通知",
                BodyTemplate = "系统将于 {startTime} 至 {endTime} 进行维护，届时系统将暂停服务。",
                Channel = "in_app,email", Variables = "startTime,endTime", IsEnabled = true },
        new() { Code = "password_reset", Name = "密码重置", TitleTemplate = "密码重置通知",
                BodyTemplate = "尊敬的 {username}：您的密码已被管理员重置为 {newPassword}，请及时登录修改。",
                Channel = "email", Variables = "username,newPassword", IsEnabled = true },
        new() { Code = "account_disabled", Name = "账户停用", TitleTemplate = "账户已停用",
                BodyTemplate = "尊敬的 {username}：您的账户已被管理员停用，如有疑问请联系管理员。",
                Channel = "in_app", Variables = "username", IsEnabled = true },
        new() { Code = "task_assigned", Name = "任务分配", TitleTemplate = "新任务分配通知",
                BodyTemplate = "您被分配了新任务：{taskTitle}，截止时间：{deadline}。",
                Channel = "in_app", Variables = "taskTitle,deadline", IsEnabled = true }
    ];

    // ═══════════════════ 租户 ═══════════════════

    private static async Task<(Guid defaultTenantId, Guid zhijihuiTenantId)> SeedTenantsAsync(
        IUnitOfWork uow, AppDbContext context, Guid createdBy)
    {
        Guid defaultTenantId = Guid.Empty;
        Guid zhijihuiTenantId = Guid.Empty;

        // 创建默认租户
        var defaultTenant = await uow.Repository<Tenant>()
            .FirstOrDefaultAsync(t => t.Code == "default");
        if (defaultTenant == null)
        {
            defaultTenant = new Tenant
            {
                Code = "default",
                Name = "默认租户",
                ContactEmail = "default@platformbase.com",
                IsEnabled = true,
                Description = "测试/演示租户",
                CreatedBy = createdBy
            };
            await uow.Repository<Tenant>().AddAsync(defaultTenant);
            await uow.SaveChangesAsync();
        }
        defaultTenantId = defaultTenant.Id;

        // 创建山东智汇集租户
        var zhijihuiTenant = await uow.Repository<Tenant>()
            .FirstOrDefaultAsync(t => t.Code == "zhijihui");
        if (zhijihuiTenant == null)
        {
            zhijihuiTenant = new Tenant
            {
                Code = "zhijihui",
                Name = "山东智汇集智能科技有限公司",
                ContactEmail = "admin@zhijihui.com",
                IsEnabled = true,
                Description = "业务租户",
                CreatedBy = createdBy
            };
            await uow.Repository<Tenant>().AddAsync(zhijihuiTenant);
            await uow.SaveChangesAsync();
        }
        zhijihuiTenantId = zhijihuiTenant.Id;

        // 关联平台用户到租户
        var admin = await uow.Repository<User>()
            .FirstOrDefaultAsync(u => u.NormalizedUsername == Norm("admin"));
        var ops = await uow.Repository<User>()
            .FirstOrDefaultAsync(u => u.NormalizedUsername == Norm("platform_ops"));

        if (admin != null)
        {
            // admin 关联所有租户
            foreach (var tenantId in new[] { defaultTenantId, zhijihuiTenantId })
            {
                var exists = await context.Set<PlatformUserTenant>()
                    .AnyAsync(p => p.PlatformUserId == admin.Id && p.TenantId == tenantId);
                if (!exists)
                {
                    context.Set<PlatformUserTenant>().Add(
                        new PlatformUserTenant { PlatformUserId = admin.Id, TenantId = tenantId });
                }
            }
        }

        if (ops != null)
        {
            // platform_ops 仅关联智汇集租户
            var exists = await context.Set<PlatformUserTenant>()
                .AnyAsync(p => p.PlatformUserId == ops.Id && p.TenantId == zhijihuiTenantId);
            if (!exists)
            {
                context.Set<PlatformUserTenant>().Add(
                    new PlatformUserTenant { PlatformUserId = ops.Id, TenantId = zhijihuiTenantId });
            }
        }

        await context.SaveChangesAsync();
        return (defaultTenantId, zhijihuiTenantId);
    }

    // ═══════════════════ 组织架构 ═══════════════════

    private static async Task<(Dictionary<string, Guid> defaultOrgs, Dictionary<string, Guid> zhijihuiOrgs)> 
        SeedOrganizationUnitsAsync(IUnitOfWork uow, AppDbContext context, Guid createdBy, Guid defaultTenantId, Guid zhijihuiTenantId)
    {
        var defaultOrgs = new Dictionary<string, Guid>();
        var zhijihuiOrgs = new Dictionary<string, Guid>();

        // 山东智汇集组织架构
        zhijihuiOrgs = await CreateZhijihuiOrgStructure(uow, context, createdBy, zhijihuiTenantId);

        // 默认租户组织架构
        defaultOrgs = await CreateDefaultOrgStructure(uow, context, createdBy, defaultTenantId);

        return (defaultOrgs, zhijihuiOrgs);
    }

    private static async Task<Dictionary<string, Guid>> CreateZhijihuiOrgStructure(
        IUnitOfWork uow, AppDbContext context, Guid createdBy, Guid tenantId)
    {
        var orgs = new Dictionary<string, Guid>();

        // 检查是否已存在
        var existing = await uow.Repository<OrganizationUnit>()
            .FindAsync(o => o.TenantId == tenantId);
        if (existing.Count > 0)
        {
            foreach (var o in existing)
                orgs[o.Name] = o.Id;
            return orgs;
        }

        // Level 1: 公司总部
        var company = await AddOrg(uow, context, tenantId, createdBy, "山东智汇集智能科技有限公司", "ZHJH_COMPANY", null, 1);
        orgs["公司总部"] = company.Id;

        // Level 2: 中心
        var devCenter = await AddOrg(uow, context, tenantId, createdBy, "研发中心", "ZHJH_DEV_CENTER", company.Id, 1);
        var opsCenter = await AddOrg(uow, context, tenantId, createdBy, "运营中心", "ZHJH_OPS_CENTER", company.Id, 2);
        var adminCenter = await AddOrg(uow, context, tenantId, createdBy, "行政中心", "ZHJH_ADMIN_CENTER", company.Id, 3);
        var csCenter = await AddOrg(uow, context, tenantId, createdBy, "客服部", "ZHJH_CS", company.Id, 4);
        orgs["研发中心"] = devCenter.Id;
        orgs["运营中心"] = opsCenter.Id;
        orgs["行政中心"] = adminCenter.Id;
        orgs["客服部"] = csCenter.Id;

        // Level 3: 部门（研发中心下）
        var techDept = await AddOrg(uow, context, tenantId, createdBy, "技术部", "ZHJH_TECH_DEPT", devCenter.Id, 1);
        var productDept = await AddOrg(uow, context, tenantId, createdBy, "产品部", "ZHJH_PRODUCT_DEPT", devCenter.Id, 2);
        var designDept = await AddOrg(uow, context, tenantId, createdBy, "设计部", "ZHJH_DESIGN_DEPT", devCenter.Id, 3);
        orgs["技术部"] = techDept.Id;
        orgs["产品部"] = productDept.Id;
        orgs["设计部"] = designDept.Id;

        // Level 3: 部门（运营中心下）
        var marketDept = await AddOrg(uow, context, tenantId, createdBy, "市场部", "ZHJH_MARKET_DEPT", opsCenter.Id, 1);
        var salesDept = await AddOrg(uow, context, tenantId, createdBy, "销售部", "ZHJH_SALES_DEPT", opsCenter.Id, 2);
        orgs["市场部"] = marketDept.Id;
        orgs["销售部"] = salesDept.Id;

        // Level 3: 部门（行政中心下）
        var hrDept = await AddOrg(uow, context, tenantId, createdBy, "人事部", "ZHJH_HR_DEPT", adminCenter.Id, 1);
        var financeDept = await AddOrg(uow, context, tenantId, createdBy, "财务部", "ZHJH_FINANCE_DEPT", adminCenter.Id, 2);
        orgs["人事部"] = hrDept.Id;
        orgs["财务部"] = financeDept.Id;

        // Level 4: 小组（技术部下）
        var backendGroup = await AddOrg(uow, context, tenantId, createdBy, "后端组", "ZHJH_BACKEND_GRP", techDept.Id, 1);
        var frontendGroup = await AddOrg(uow, context, tenantId, createdBy, "前端组", "ZHJH_FRONTEND_GRP", techDept.Id, 2);
        var qaGroup = await AddOrg(uow, context, tenantId, createdBy, "测试组", "ZHJH_QA_GRP", techDept.Id, 3);
        orgs["后端组"] = backendGroup.Id;
        orgs["前端组"] = frontendGroup.Id;
        orgs["测试组"] = qaGroup.Id;

        // Level 4: 小组（产品部下）
        var planGroup = await AddOrg(uow, context, tenantId, createdBy, "产品策划组", "ZHJH_PLAN_GRP", productDept.Id, 1);
        var uxGroup = await AddOrg(uow, context, tenantId, createdBy, "用户体验组", "ZHJH_UX_GRP", productDept.Id, 2);
        orgs["产品策划组"] = planGroup.Id;
        orgs["用户体验组"] = uxGroup.Id;

        // Level 4: 小组（设计部下）
        var visualGroup = await AddOrg(uow, context, tenantId, createdBy, "视觉设计组", "ZHJH_VISUAL_GRP", designDept.Id, 1);
        orgs["视觉设计组"] = visualGroup.Id;

        // Level 4: 小组（市场部下）
        var brandGroup = await AddOrg(uow, context, tenantId, createdBy, "品牌推广组", "ZHJH_BRAND_GRP", marketDept.Id, 1);
        var salesSupportGroup = await AddOrg(uow, context, tenantId, createdBy, "销售支持组", "ZHJH_SALES_SUPPORT_GRP", marketDept.Id, 2);
        orgs["品牌推广组"] = brandGroup.Id;
        orgs["销售支持组"] = salesSupportGroup.Id;

        // Level 4: 小组（销售部下）
        var keyClientGroup = await AddOrg(uow, context, tenantId, createdBy, "大客户组", "ZHJH_KEY_CLIENT_GRP", salesDept.Id, 1);
        var channelGroup = await AddOrg(uow, context, tenantId, createdBy, "渠道组", "ZHJH_CHANNEL_GRP", salesDept.Id, 2);
        orgs["大客户组"] = keyClientGroup.Id;
        orgs["渠道组"] = channelGroup.Id;

        // Level 4: 小组（人事部下）
        var recruitGroup = await AddOrg(uow, context, tenantId, createdBy, "招聘组", "ZHJH_RECRUIT_GRP", hrDept.Id, 1);
        var relationGroup = await AddOrg(uow, context, tenantId, createdBy, "员工关系组", "ZHJH_RELATION_GRP", hrDept.Id, 2);
        orgs["招聘组"] = recruitGroup.Id;
        orgs["员工关系组"] = relationGroup.Id;

        // Level 4: 小组（财务部下）
        var accountGroup = await AddOrg(uow, context, tenantId, createdBy, "会计组", "ZHJH_ACCOUNT_GRP", financeDept.Id, 1);
        var cashierGroup = await AddOrg(uow, context, tenantId, createdBy, "出纳组", "ZHJH_CASHIER_GRP", financeDept.Id, 2);
        orgs["会计组"] = accountGroup.Id;
        orgs["出纳组"] = cashierGroup.Id;

        // Level 4: 小组（客服部下）
        var techSupportGroup = await AddOrg(uow, context, tenantId, createdBy, "技术支持组", "ZHJH_TECH_SUPPORT_GRP", csCenter.Id, 1);
        var careGroup = await AddOrg(uow, context, tenantId, createdBy, "客户关怀组", "ZHJH_CARE_GRP", csCenter.Id, 2);
        orgs["技术支持组"] = techSupportGroup.Id;
        orgs["客户关怀组"] = careGroup.Id;

        // Level 5: 岗位（部分关键岗位）
        await AddOrg(uow, context, tenantId, createdBy, "后端开发岗", "ZHJH_BACKEND_POS", backendGroup.Id, 1);
        await AddOrg(uow, context, tenantId, createdBy, "后端架构岗", "ZHJH_BACKEND_ARCH_POS", backendGroup.Id, 2);
        await AddOrg(uow, context, tenantId, createdBy, "前端开发岗", "ZHJH_FRONTEND_POS", frontendGroup.Id, 1);
        await AddOrg(uow, context, tenantId, createdBy, "UI设计岗", "ZHJH_UI_POS", frontendGroup.Id, 2);
        await AddOrg(uow, context, tenantId, createdBy, "测试工程师岗", "ZHJH_QA_POS", qaGroup.Id, 1);
        await AddOrg(uow, context, tenantId, createdBy, "测试主管岗", "ZHJH_QA_LEAD_POS", qaGroup.Id, 2);
        await AddOrg(uow, context, tenantId, createdBy, "产品经理岗", "ZHJH_PM_POS", planGroup.Id, 1);
        await AddOrg(uow, context, tenantId, createdBy, "UX设计师岗", "ZHJH_UX_POS", uxGroup.Id, 1);
        await AddOrg(uow, context, tenantId, createdBy, "视觉设计师岗", "ZHJH_VISUAL_POS", visualGroup.Id, 1);
        await AddOrg(uow, context, tenantId, createdBy, "品牌经理岗", "ZHJH_BRAND_POS", brandGroup.Id, 1);
        await AddOrg(uow, context, tenantId, createdBy, "销售专员岗", "ZHJH_SALES_POS", salesSupportGroup.Id, 1);
        await AddOrg(uow, context, tenantId, createdBy, "大客户经理岗", "ZHJH_KEY_CLIENT_POS", keyClientGroup.Id, 1);
        await AddOrg(uow, context, tenantId, createdBy, "渠道专员岗", "ZHJH_CHANNEL_POS", channelGroup.Id, 1);
        await AddOrg(uow, context, tenantId, createdBy, "HR专员岗", "ZHJH_HR_POS", recruitGroup.Id, 1);
        await AddOrg(uow, context, tenantId, createdBy, "HR经理岗", "ZHJH_HR_MGR_POS", relationGroup.Id, 1);
        await AddOrg(uow, context, tenantId, createdBy, "会计岗", "ZHJH_ACCOUNT_POS", accountGroup.Id, 1);
        await AddOrg(uow, context, tenantId, createdBy, "出纳岗", "ZHJH_CASHIER_POS", cashierGroup.Id, 1);
        await AddOrg(uow, context, tenantId, createdBy, "技术支持岗", "ZHJH_SUPPORT_POS", techSupportGroup.Id, 1);
        await AddOrg(uow, context, tenantId, createdBy, "客服专员岗", "ZHJH_CS_POS", careGroup.Id, 1);

        await uow.SaveChangesAsync();
        return orgs;
    }

    private static async Task<Dictionary<string, Guid>> CreateDefaultOrgStructure(
        IUnitOfWork uow, AppDbContext context, Guid createdBy, Guid tenantId)
    {
        var orgs = new Dictionary<string, Guid>();

        var existing = await uow.Repository<OrganizationUnit>()
            .FindAsync(o => o.TenantId == tenantId);
        if (existing.Count > 0)
        {
            foreach (var o in existing)
                orgs[o.Name] = o.Id;
            return orgs;
        }

        // Level 1: 公司
        var company = await AddOrg(uow, context, tenantId, createdBy, "默认租户公司", "DEFAULT_COMPANY", null, 1);
        orgs["公司总部"] = company.Id;

        // Level 2: 中心
        var techCenter = await AddOrg(uow, context, tenantId, createdBy, "技术中心", "DEFAULT_TECH_CENTER", company.Id, 1);
        var opsCenter = await AddOrg(uow, context, tenantId, createdBy, "运营中心", "DEFAULT_OPS_CENTER", company.Id, 2);
        var adminCenter = await AddOrg(uow, context, tenantId, createdBy, "行政中心", "DEFAULT_ADMIN_CENTER", company.Id, 3);
        orgs["技术中心"] = techCenter.Id;
        orgs["运营中心"] = opsCenter.Id;
        orgs["行政中心"] = adminCenter.Id;

        // Level 3: 部门
        var devDept = await AddOrg(uow, context, tenantId, createdBy, "开发部", "DEFAULT_DEV_DEPT", techCenter.Id, 1);
        var opsDept = await AddOrg(uow, context, tenantId, createdBy, "运维部", "DEFAULT_OPS_DEPT", techCenter.Id, 2);
        var marketDept = await AddOrg(uow, context, tenantId, createdBy, "市场部", "DEFAULT_MARKET_DEPT", opsCenter.Id, 1);
        var adminDept = await AddOrg(uow, context, tenantId, createdBy, "行政部", "DEFAULT_ADMIN_DEPT", adminCenter.Id, 1);
        orgs["开发部"] = devDept.Id;
        orgs["运维部"] = opsDept.Id;
        orgs["市场部"] = marketDept.Id;
        orgs["行政部"] = adminDept.Id;

        // Level 4: 小组
        var devGroup = await AddOrg(uow, context, tenantId, createdBy, "开发组", "DEFAULT_DEV_GRP", devDept.Id, 1);
        var qaGroup = await AddOrg(uow, context, tenantId, createdBy, "测试组", "DEFAULT_QA_GRP", devDept.Id, 2);
        var opsGroup = await AddOrg(uow, context, tenantId, createdBy, "运维组", "DEFAULT_OPS_GRP", opsDept.Id, 1);
        var marketGroup = await AddOrg(uow, context, tenantId, createdBy, "市场组", "DEFAULT_MARKET_GRP", marketDept.Id, 1);
        var adminGroup = await AddOrg(uow, context, tenantId, createdBy, "行政组", "DEFAULT_ADMIN_GRP", adminDept.Id, 1);
        orgs["开发组"] = devGroup.Id;
        orgs["测试组"] = qaGroup.Id;
        orgs["运维组"] = opsGroup.Id;
        orgs["市场组"] = marketGroup.Id;
        orgs["行政组"] = adminGroup.Id;

        // Level 5: 岗位
        await AddOrg(uow, context, tenantId, createdBy, "开发工程师岗", "DEFAULT_DEV_POS", devGroup.Id, 1);
        await AddOrg(uow, context, tenantId, createdBy, "技术主管岗", "DEFAULT_TECH_LEAD_POS", devGroup.Id, 2);
        await AddOrg(uow, context, tenantId, createdBy, "测试工程师岗", "DEFAULT_QA_POS", qaGroup.Id, 1);
        await AddOrg(uow, context, tenantId, createdBy, "QA主管岗", "DEFAULT_QA_LEAD_POS", qaGroup.Id, 2);
        await AddOrg(uow, context, tenantId, createdBy, "运维工程师岗", "DEFAULT_OPS_POS", opsGroup.Id, 1);
        await AddOrg(uow, context, tenantId, createdBy, "市场专员岗", "DEFAULT_MARKET_POS", marketGroup.Id, 1);
        await AddOrg(uow, context, tenantId, createdBy, "行政专员岗", "DEFAULT_ADMIN_POS", adminGroup.Id, 1);

        await uow.SaveChangesAsync();
        return orgs;
    }

    private static async Task<OrganizationUnit> AddOrg(
        IUnitOfWork uow, AppDbContext context, Guid tenantId, Guid createdBy,
        string name, string code, Guid? parentId, int sortOrder)
    {
        var entity = new OrganizationUnit
        {
            TenantId = tenantId,
            Name = name,
            Code = code,
            ParentId = parentId,
            SortOrder = sortOrder,
            IsEnabled = true,
            CreatedBy = createdBy
        };
        var created = await uow.Repository<OrganizationUnit>().AddAsync(entity);
        await uow.SaveChangesAsync();

        // 计算物化路径
        if (parentId != null)
        {
            var parent = await uow.Repository<OrganizationUnit>().GetByIdAsync(parentId.Value);
            created.Path = $"{parent?.Path ?? "/"}{created.Id}/";
        }
        else
        {
            created.Path = $"/{created.Id}/";
        }
        uow.Repository<OrganizationUnit>().Update(created);
        await uow.SaveChangesAsync();
        return created;
    }

    // ═══════════════════ 租户用户 ═══════════════════

    private static async Task<(Dictionary<string, Guid> defaultUsers, Dictionary<string, Guid> zhijihuiUsers)>
        SeedTenantUsersAsync(IUnitOfWork uow, AppDbContext context, Guid createdBy,
            Guid defaultTenantId, Guid zhijihuiTenantId)
    {
        var defaultUsers = new Dictionary<string, Guid>();
        var zhijihuiUsers = new Dictionary<string, Guid>();

        var roles = await uow.Repository<Role>().GetAllAsync();
        var roleMap = roles.ToDictionary(r => r.Name, r => r.Id);

        // 山东智汇集用户
        zhijihuiUsers = await CreateZhijihuiUsers(uow, context, createdBy, zhijihuiTenantId, roleMap);

        // 默认租户用户
        defaultUsers = await CreateDefaultUsers(uow, context, createdBy, defaultTenantId, roleMap);

        return (defaultUsers, zhijihuiUsers);
    }

    private static async Task<Dictionary<string, Guid>> CreateZhijihuiUsers(
        IUnitOfWork uow, AppDbContext context, Guid createdBy, Guid tenantId,
        Dictionary<string, Guid> roleMap)
    {
        var users = new Dictionary<string, Guid>();

        var usersToCreate = new[]
        {
            (Username: "zhijihui_admin", Password: "Admin@123", Email: "admin@zhijihui.com",
             UserType: UserType.TenantAdmin, Role: "TenantAdmin", DisplayName: "智汇集管理员"),
            (Username: "zhijihui_ceo", Password: "Ceo@123", Email: "ceo@zhijihui.com",
             UserType: UserType.TenantUser, Role: "Manager", DisplayName: "CEO"),
            (Username: "zhijihui_cto", Password: "Cto@123", Email: "cto@zhijihui.com",
             UserType: UserType.TenantUser, Role: "Manager", DisplayName: "CTO"),
            (Username: "zhijihui_dev1", Password: "Dev1@123", Email: "dev1@zhijihui.com",
             UserType: UserType.TenantUser, Role: "User", DisplayName: "后端开发1"),
            (Username: "zhijihui_dev2", Password: "Dev2@123", Email: "dev2@zhijihui.com",
             UserType: UserType.TenantUser, Role: "User", DisplayName: "前端开发"),
            (Username: "zhijihui_qa", Password: "Qa@123", Email: "qa@zhijihui.com",
             UserType: UserType.TenantUser, Role: "User", DisplayName: "测试工程师")
        };

        foreach (var (username, password, email, userType, role, displayName) in usersToCreate)
        {
            var existing = await uow.Repository<User>()
                .FirstOrDefaultAsync(u => u.NormalizedUsername == Norm(username));
            if (existing != null)
            {
                users[displayName] = existing.Id;
                continue;
            }

            var user = new User
            {
                Username = username,
                NormalizedUsername = Norm(username),
                Email = email,
                NormalizedEmail = Norm(email),
                EmailConfirmed = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                IsActive = true,
                TenantId = tenantId,
                UserType = userType,
                IsSuperAdmin = false,
                CreatedBy = createdBy
            };
            var created = await uow.Repository<User>().AddAsync(user);
            await uow.SaveChangesAsync();
            users[displayName] = created.Id;

            // 分配角色
            if (roleMap.TryGetValue(role, out var roleId))
            {
                context.Set<UserRole>().Add(new UserRole { UserId = created.Id, RoleId = roleId });
                await context.SaveChangesAsync();
            }
        }

        return users;
    }

    private static async Task<Dictionary<string, Guid>> CreateDefaultUsers(
        IUnitOfWork uow, AppDbContext context, Guid createdBy, Guid tenantId,
        Dictionary<string, Guid> roleMap)
    {
        var users = new Dictionary<string, Guid>();

        var usersToCreate = new[]
        {
            (Username: "default_admin", Password: "Admin@123", Email: "admin@default.com",
             UserType: UserType.TenantAdmin, Role: "TenantAdmin", DisplayName: "默认管理员"),
            (Username: "default_manager", Password: "Manager@123", Email: "manager@default.com",
             UserType: UserType.TenantUser, Role: "Manager", DisplayName: "技术经理"),
            (Username: "default_dev", Password: "Dev@123", Email: "dev@default.com",
             UserType: UserType.TenantUser, Role: "User", DisplayName: "开发工程师"),
            (Username: "default_qa", Password: "Qa@123", Email: "qa@default.com",
             UserType: UserType.TenantUser, Role: "User", DisplayName: "测试工程师"),
            (Username: "testuser", Password: "Test@123", Email: "testuser@default.com",
             UserType: UserType.TenantUser, Role: "User", DisplayName: "测试用户")
        };

        foreach (var (username, password, email, userType, role, displayName) in usersToCreate)
        {
            var existing = await uow.Repository<User>()
                .FirstOrDefaultAsync(u => u.NormalizedUsername == Norm(username));
            if (existing != null)
            {
                users[displayName] = existing.Id;
                continue;
            }

            var user = new User
            {
                Username = username,
                NormalizedUsername = Norm(username),
                Email = email,
                NormalizedEmail = Norm(email),
                EmailConfirmed = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                IsActive = true,
                TenantId = tenantId,
                UserType = userType,
                IsSuperAdmin = false,
                CreatedBy = createdBy
            };
            var created = await uow.Repository<User>().AddAsync(user);
            await uow.SaveChangesAsync();
            users[displayName] = created.Id;

            // 分配角色
            if (roleMap.TryGetValue(role, out var roleId))
            {
                context.Set<UserRole>().Add(new UserRole { UserId = created.Id, RoleId = roleId });
                await context.SaveChangesAsync();
            }
        }

        return users;
    }

    // ═══════════════════ 用户-部门关联 ═══════════════════

    private static async Task SeedUserOrganizationsAsync(
        AppDbContext context,
        (Dictionary<string, Guid> defaultUsers, Dictionary<string, Guid> zhijihuiUsers) userMaps,
        (Dictionary<string, Guid> defaultOrgs, Dictionary<string, Guid> zhijihuiOrgs) orgMaps)
    {
        // 山东智汇集用户-部门关联
        var zhijihuiMappings = new[]
        {
            (User: "智汇集管理员", Org: "公司总部"),
            (User: "CEO", Org: "公司总部"),
            (User: "CTO", Org: "研发中心"),
            (User: "后端开发1", Org: "后端组"),
            (User: "前端开发", Org: "前端组"),
            (User: "测试工程师", Org: "测试组")
        };

        foreach (var (userKey, orgKey) in zhijihuiMappings)
        {
            if (!userMaps.zhijihuiUsers.TryGetValue(userKey, out var userId) ||
                !orgMaps.zhijihuiOrgs.TryGetValue(orgKey, out var orgId)) continue;

            var exists = await context.Set<UserOrganizationUnit>()
                .AnyAsync(uo => uo.UserId == userId && uo.OrganizationUnitId == orgId);
            if (!exists)
            {
                context.Set<UserOrganizationUnit>().Add(
                    new UserOrganizationUnit { UserId = userId, OrganizationUnitId = orgId });
            }
        }

        // 默认租户用户-部门关联
        var defaultMappings = new[]
        {
            (User: "默认管理员", Org: "公司总部"),
            (User: "技术经理", Org: "技术中心"),
            (User: "开发工程师", Org: "开发组"),
            (User: "测试工程师", Org: "测试组"),
            (User: "测试用户", Org: "测试组")
        };

        foreach (var (userKey, orgKey) in defaultMappings)
        {
            if (!userMaps.defaultUsers.TryGetValue(userKey, out var userId) ||
                !orgMaps.defaultOrgs.TryGetValue(orgKey, out var orgId)) continue;

            var exists = await context.Set<UserOrganizationUnit>()
                .AnyAsync(uo => uo.UserId == userId && uo.OrganizationUnitId == orgId);
            if (!exists)
            {
                context.Set<UserOrganizationUnit>().Add(
                    new UserOrganizationUnit { UserId = userId, OrganizationUnitId = orgId });
            }
        }

        await context.SaveChangesAsync();
    }

    // ═══════════════════ 菜单 ═══════════════════

    private static async Task SeedMenusAsync(IUnitOfWork uow, AppDbContext context, Guid createdBy)
    {
        var existing = await uow.Repository<Menu>().GetAllAsync();
        if (existing.Count > 0) return;

        var sysMenu = await AddMenu(uow, createdBy, new Menu { Name = "系统管理", Type = 1, Icon = "IconSettings", SortOrder = 100, IsVisible = true, IsEnabled = true });
        var confMenu = await AddMenu(uow, createdBy, new Menu { Name = "系统配置", Type = 1, Icon = "IconTool", SortOrder = 200, IsVisible = true, IsEnabled = true });
        var monMenu = await AddMenu(uow, createdBy, new Menu { Name = "系统监控", Type = 1, Icon = "IconActivity", SortOrder = 300, IsVisible = true, IsEnabled = true });

        await AddMenu(uow, createdBy, new Menu { ParentId = sysMenu.Id, Name = "用户管理", Type = 2, Icon = "IconUsers", Path = "/users", PermissionCode = "users.list", SortOrder = 1, KeepAlive = true, IsVisible = true, IsEnabled = true });
        await AddMenu(uow, createdBy, new Menu { ParentId = sysMenu.Id, Name = "角色管理", Type = 2, Icon = "IconUserShield", Path = "/roles", PermissionCode = "roles.list", SortOrder = 2, KeepAlive = true, IsVisible = true, IsEnabled = true });
        await AddMenu(uow, createdBy, new Menu { ParentId = sysMenu.Id, Name = "权限管理", Type = 2, Icon = "IconLock", Path = "/permissions", PermissionCode = "perms.list", SortOrder = 3, KeepAlive = true, IsVisible = true, IsEnabled = true });
        await AddMenu(uow, createdBy, new Menu { ParentId = sysMenu.Id, Name = "组织架构", Type = 2, Icon = "IconShare", Path = "/organization-units", PermissionCode = "org-units.list", SortOrder = 4, KeepAlive = true, IsVisible = true, IsEnabled = true });
        await AddMenu(uow, createdBy, new Menu { ParentId = sysMenu.Id, Name = "租户管理", Type = 2, Icon = "IconBuilding", Path = "/tenants", PermissionCode = "tenants.list", SortOrder = 5, KeepAlive = true, IsVisible = true, IsEnabled = true });

        await AddMenu(uow, createdBy, new Menu { ParentId = confMenu.Id, Name = "菜单管理", Type = 2, Icon = "IconMenu2", Path = "/menus", PermissionCode = "menus.list", SortOrder = 1, KeepAlive = true, IsVisible = true, IsEnabled = true });
        await AddMenu(uow, createdBy, new Menu { ParentId = confMenu.Id, Name = "系统参数", Type = 2, Icon = "IconSettingsCog", Path = "/system-params", PermissionCode = "system-params.list", SortOrder = 2, KeepAlive = true, IsVisible = true, IsEnabled = true });
        await AddMenu(uow, createdBy, new Menu { ParentId = confMenu.Id, Name = "租户参数", Type = 2, Icon = "IconSettingsCog", Path = "/tenant-params", PermissionCode = "tenant-params.list", SortOrder = 3, KeepAlive = true, IsVisible = true, IsEnabled = true });
        await AddMenu(uow, createdBy, new Menu { ParentId = confMenu.Id, Name = "数据字典", Type = 2, Icon = "IconBooks", Path = "/data-dict", PermissionCode = "datadict.list", SortOrder = 4, KeepAlive = true, IsVisible = true, IsEnabled = true });
        await AddMenu(uow, createdBy, new Menu { ParentId = confMenu.Id, Name = "文件管理", Type = 2, Icon = "IconFolders", Path = "/files", PermissionCode = "files.upload", SortOrder = 5, KeepAlive = true, IsVisible = true, IsEnabled = true });

        await AddMenu(uow, createdBy, new Menu { ParentId = monMenu.Id, Name = "操作日志", Type = 2, Icon = "IconFileText", Path = "/operation-logs", PermissionCode = "operation-logs.list", SortOrder = 1, KeepAlive = true, IsVisible = true, IsEnabled = true });
        await AddMenu(uow, createdBy, new Menu { ParentId = monMenu.Id, Name = "定时任务", Type = 2, Icon = "IconClock", Path = "/jobs", PermissionCode = "jobs.list", SortOrder = 2, KeepAlive = true, IsVisible = true, IsEnabled = true });

        await uow.SaveChangesAsync();

        // 为用户分配菜单（平台管理员看全部，租户管理员和普通用户需要关联）
        await SeedUserMenusAsync(uow, context);
    }

    private static async Task<Menu> AddMenu(IUnitOfWork uow, Guid createdBy, Menu menu)
    {
        menu.CreatedBy = createdBy;
        return await uow.Repository<Menu>().AddAsync(menu);
    }

    // ═══════════════════ 用户-菜单关联 ═══════════════════

    private static async Task SeedUserMenusAsync(IUnitOfWork uow, AppDbContext context)
    {
        var allMenus = await uow.Repository<Menu>().GetAllAsync();
        var allMenuIds = allMenus.Select(m => m.Id).ToList();

        // 平台管理员 admin 不需要关联，代码中直接看全部
        // 租户管理员和普通用户需要关联菜单

        // 获取所有租户用户
        var tenantUsers = await uow.Repository<User>()
            .FindAsync(u => u.UserType == UserType.TenantAdmin || u.UserType == UserType.TenantUser);

        // 获取租户管理员可用的菜单（排除 tenants.*, system-params.*, jobs.*, operation-logs.*）
        var tenantAdminMenuIds = allMenus
            .Where(m => !IsBannedPermission(m.PermissionCode))
            .Select(m => m.Id)
            .ToList();

        // 获取普通用户可用的菜单（仅基础菜单）
        var normalUserMenuIds = allMenus
            .Where(m => m.PermissionCode == null || 
                        m.PermissionCode.StartsWith("users.") || 
                        m.PermissionCode.StartsWith("datadict.") ||
                        m.PermissionCode.StartsWith("files."))
            .Select(m => m.Id)
            .ToList();

        foreach (var user in tenantUsers)
        {
            // 检查是否已有关联
            var existingCount = await context.Set<UserMenu>()
                .CountAsync(um => um.UserId == user.Id);
            if (existingCount > 0) continue;

            var menuIds = user.UserType == UserType.TenantAdmin ? tenantAdminMenuIds : normalUserMenuIds;

            foreach (var menuId in menuIds)
            {
                context.Set<UserMenu>().Add(new UserMenu
                {
                    UserId = user.Id,
                    MenuId = menuId
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static bool IsBannedPermission(string? permissionCode)
    {
        if (string.IsNullOrEmpty(permissionCode)) return false;
        return permissionCode.StartsWith("tenants.", StringComparison.OrdinalIgnoreCase) ||
               permissionCode.StartsWith("system-params.", StringComparison.OrdinalIgnoreCase) ||
               permissionCode.StartsWith("jobs.", StringComparison.OrdinalIgnoreCase) ||
               permissionCode.StartsWith("operation-logs.", StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════ 演示数据 ═══════════════════

    private static async Task SeedDemoDataAsync(
        IUnitOfWork uow, AppDbContext context,
        (Dictionary<string, Guid> defaultUsers, Dictionary<string, Guid> zhijihuiUsers) userMaps)
    {
        var templates = await uow.Repository<NotificationTemplate>().GetAllAsync();
        var templateMap = templates.ToDictionary(t => t.Code, t => t.Id);

        // 操作日志演示数据
        var logs = new[]
        {
            (Username: "admin", Action: "login", Resource: "System", Detail: "平台管理员登录", IsSuccess: true),
            (Username: "zhijihui_admin", Action: "login", Resource: "System", Detail: "租户管理员登录", IsSuccess: true),
            (Username: "zhijihui_admin", Action: "create", Resource: "User:zhijihui_dev1", Detail: "创建用户", IsSuccess: true),
            (Username: "zhijihui_cto", Action: "create", Resource: "OrganizationUnit:后端组", Detail: "创建部门", IsSuccess: true),
            (Username: "default_admin", Action: "login", Resource: "System", Detail: "租户管理员登录", IsSuccess: true),
            (Username: "platform_ops", Action: "login", Resource: "System", Detail: "平台运维登录", IsSuccess: true),
            (Username: "platform_ops", Action: "create", Resource: "Tenant:zhijihui", Detail: "创建租户", IsSuccess: true),
            (Username: "testuser", Action: "login", Resource: "System", Detail: "普通用户登录", IsSuccess: true),
            (Username: "zhijihui_dev1", Action: "login", Resource: "System", Detail: "租户用户登录", IsSuccess: true),
            (Username: "zhijihui_dev2", Action: "login", Resource: "System", Detail: "租户用户登录", IsSuccess: true)
        };

        foreach (var (username, action, resource, detail, isSuccess) in logs)
        {
            var user = await uow.Repository<User>()
                .FirstOrDefaultAsync(u => u.NormalizedUsername == Norm(username));
            if (user == null) continue;

            context.Set<OperationLog>().Add(new OperationLog
            {
                UserId = user.Id,
                Username = username,
                Action = action,
                Resource = resource,
                Detail = detail,
                IpAddress = "127.0.0.1",
                UserAgent = "Mozilla/5.0",
                IsSuccess = isSuccess,
                Timestamp = DateTime.UtcNow.AddHours(-Random.Shared.Next(1, 24))
            });
        }
        await context.SaveChangesAsync();

        // 通知演示数据
        var notifications = new[]
        {
            (UserKey: "后端开发1", Template: "welcome", Title: "欢迎加入 PlatformBase", IsRead: false),
            (UserKey: "后端开发1", Template: "role_assigned", Title: "您已被分配 User 角色", IsRead: false),
            (UserKey: "后端开发1", Template: "org_joined", Title: "您已加入 后端组 部门", IsRead: true),
            (UserKey: "开发工程师", Template: "welcome", Title: "欢迎加入 PlatformBase", IsRead: false),
            (UserKey: "默认管理员", Template: "tenant_created", Title: "租户创建成功", IsRead: true)
        };

        foreach (var (userKey, templateCode, title, isRead) in notifications)
        {
            Guid userId;
            if (userMaps.zhijihuiUsers.TryGetValue(userKey, out var zjUserId))
                userId = zjUserId;
            else if (userMaps.defaultUsers.TryGetValue(userKey, out var dfUserId))
                userId = dfUserId;
            else continue;

            if (!templateMap.TryGetValue(templateCode, out var templateId)) continue;

            context.Set<Notification>().Add(new Notification
            {
                UserId = userId,
                TemplateCode = templateCode,
                Title = title,
                Content = "演示通知内容",
                IsRead = isRead,
                Channel = "in_app",
                Timestamp = DateTime.UtcNow.AddHours(-Random.Shared.Next(1, 12))
            });
        }
        await context.SaveChangesAsync();
    }
}