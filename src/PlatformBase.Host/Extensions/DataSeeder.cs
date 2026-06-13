using Microsoft.EntityFrameworkCore;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Repositories;
using PlatformBase.Infrastructure.Data;

namespace PlatformBase.Host.Extensions;

/// <summary>
/// 应用启动种子数据初始化，所有操作均为幂等（已存在则跳过）
/// 种子顺序：admin 用户 → 角色 → 权限 → 角色-权限关联 → testuser
/// admin 的 Id 作为所有种子数据的 CreatedBy，确保审计字段不为 null
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(this IHost app)
    {
        using var scope = app.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await context.Database.EnsureCreatedAsync();

        // ① 先创建 admin 用户，获取其 Id 作为种子操作人
        var adminId = await SeedAdminAsync(uow, context);

        // ② 使用 adminId 创建角色/权限/关联/其他用户
        await SeedRolesAsync(uow, adminId);
        await SeedPermissionsAsync(uow, adminId);
        await SeedRolePermissionsAsync(uow, context, adminId);
        await SeedTestUserAsync(uow, context, adminId);
        await SeedSystemParamsAsync(uow, context, adminId);
        await SeedDataDictAsync(uow, context, adminId);
        await SeedNotificationTemplatesAsync(uow, context, adminId);
        await SeedTenantsAsync(uow, context, adminId);
        await SeedMenusAsync(uow, adminId);

        scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DataSeeder")
            .LogInformation("种子数据初始化完成");
    }

    /// <summary>创建 admin 用户并返回其 Id</summary>
    private static async Task<Guid> SeedAdminAsync(IUnitOfWork uow, AppDbContext context)
    {
        var existing = await uow.Repository<User>()
            .FirstOrDefaultAsync(u => u.NormalizedUsername == Norm("admin"));
        if (existing != null) return existing.Id;

        // 软删除场景：检查是否被删除后需要恢复
        var deleted = await context.Set<User>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.NormalizedUsername == Norm("admin") && u.IsDeleted);
        if (deleted != null) return deleted.Id;

        var admin = new User
        {
            Username = "admin",
            NormalizedUsername = Norm("admin"),
            Email = "admin@platformbase.com",
            NormalizedEmail = Norm("admin@platformbase.com"),
            EmailConfirmed = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            IsActive = true
        };

        await uow.Repository<User>().AddAsync(admin);
        await uow.SaveChangesAsync();
        return admin.Id;
    }

    private static async Task SeedRolesAsync(IUnitOfWork uow, Guid createdBy)
    {
        var existing = await uow.Repository<Role>().GetAllAsync();
        var existingNames = existing.Select(r => r.NormalizedName).ToHashSet();

        foreach (var (name, desc) in GetSeedRoles())
        {
            if (existingNames.Contains(Norm(name))) continue;
            await uow.Repository<Role>().AddAsync(new Role
            {
                Name = name,
                NormalizedName = Norm(name),
                Description = desc,
                CreatedBy = createdBy
            });
        }
        await uow.SaveChangesAsync();
    }

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
                if (existingRps.Any(rp => rp.RoleId == roleId && rp.PermissionId == permId))
                    continue;
                context.Set<RolePermission>().Add(new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permId
                });
            }
        }
        await context.SaveChangesAsync();
    }

    private static async Task SeedTestUserAsync(IUnitOfWork uow, AppDbContext context, Guid createdBy)
    {
        var roles = await uow.Repository<Role>().GetAllAsync();
        var roleMap = roles.ToDictionary(r => r.Name, r => r.Id);

        var userRoleIds = new List<Guid>();
        if (roleMap.TryGetValue("User", out var usrId)) userRoleIds.Add(usrId);

        var exists = await uow.Repository<User>()
            .AnyAsync(u => u.NormalizedUsername == Norm("testuser"));
        if (exists) return;

        // 给 testuser 赋予 User 角色
        var user = new User
        {
            Username = "testuser",
            NormalizedUsername = Norm("testuser"),
            Email = "testuser@platformbase.com",
            NormalizedEmail = Norm("testuser@platformbase.com"),
            EmailConfirmed = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123"),
            IsActive = true,
            CreatedBy = createdBy
        };

        await uow.Repository<User>().AddAsync(user);
        await uow.SaveChangesAsync();

        foreach (var roleId in userRoleIds)
        {
            context.Set<UserRole>().Add(new UserRole
            {
                UserId = user.Id,
                RoleId = roleId
            });
        }

        // 给 admin 赋予 Admin 角色
        var adminRoleIds = new List<Guid>();
        if (roleMap.TryGetValue("Admin", out var adminRoleId)) adminRoleIds.Add(adminRoleId);

        var adminExists = await context.Set<UserRole>()
            .AnyAsync(ur => ur.UserId == createdBy && ur.RoleId == adminRoleId);
        if (!adminExists && adminRoleIds.Count > 0)
        {
            context.Set<UserRole>().Add(new UserRole
            {
                UserId = createdBy,
                RoleId = adminRoleId
            });
        }

        await context.SaveChangesAsync();
    }

    // ═══════════════════ 种子数据定义 ═══════════════════

    private static (string Name, string Description)[] GetSeedRoles() =>
    [
        ("Admin", "系统管理员 — 拥有全部权限"),
        ("Manager", "业务管理员 — 用户和角色查看"),
        ("User", "普通用户 — 最小权限")
    ];

    private static Permission[] GetSeedPermissions() =>
    [
        new() { Code = "users.list", Name = "用户列表", ResourcePath = "/api/v1/users", HttpMethod = "GET", GroupName = "用户管理", SortOrder = 1 },
        new() { Code = "users.create", Name = "创建用户", ResourcePath = "/api/v1/users", HttpMethod = "POST", GroupName = "用户管理", SortOrder = 2 },
        new() { Code = "users.edit", Name = "编辑用户", ResourcePath = "/api/v1/users", HttpMethod = "PUT", GroupName = "用户管理", SortOrder = 3 },
        new() { Code = "users.delete", Name = "删除用户", ResourcePath = "/api/v1/users", HttpMethod = "DELETE", GroupName = "用户管理", SortOrder = 4 },
        new() { Code = "roles.list", Name = "角色列表", ResourcePath = "/api/v1/roles", HttpMethod = "GET", GroupName = "角色管理", SortOrder = 1 },
        new() { Code = "roles.create", Name = "创建角色", ResourcePath = "/api/v1/roles", HttpMethod = "POST", GroupName = "角色管理", SortOrder = 2 },
        new() { Code = "roles.edit", Name = "编辑角色", ResourcePath = "/api/v1/roles", HttpMethod = "PUT", GroupName = "角色管理", SortOrder = 3 },
        new() { Code = "roles.delete", Name = "删除角色", ResourcePath = "/api/v1/roles", HttpMethod = "DELETE", GroupName = "角色管理", SortOrder = 4 },
        new() { Code = "perms.list", Name = "权限列表", ResourcePath = "/api/v1/permissions", HttpMethod = "GET", GroupName = "权限管理", SortOrder = 1 },
        new() { Code = "perms.create", Name = "创建权限", ResourcePath = "/api/v1/permissions", HttpMethod = "POST", GroupName = "权限管理", SortOrder = 2 },
        new() { Code = "perms.edit", Name = "编辑权限", ResourcePath = "/api/v1/permissions", HttpMethod = "PUT", GroupName = "权限管理", SortOrder = 3 },
        new() { Code = "perms.delete", Name = "删除权限", ResourcePath = "/api/v1/permissions", HttpMethod = "DELETE", GroupName = "权限管理", SortOrder = 4 },
        new() { Code = "system-params.list", Name = "系统参数列表", ResourcePath = "/api/v1/system-params", HttpMethod = "GET", GroupName = "系统管理", SortOrder = 1 },
        new() { Code = "system-params.create", Name = "创建系统参数", ResourcePath = "/api/v1/system-params", HttpMethod = "POST", GroupName = "系统管理", SortOrder = 2 },
        new() { Code = "system-params.edit", Name = "编辑系统参数", ResourcePath = "/api/v1/system-params", HttpMethod = "PUT", GroupName = "系统管理", SortOrder = 3 },
        new() { Code = "system-params.delete", Name = "删除系统参数", ResourcePath = "/api/v1/system-params", HttpMethod = "DELETE", GroupName = "系统管理", SortOrder = 4 },
        new() { Code = "datadict.list", Name = "字典列表", ResourcePath = "/api/v1/data-dict", HttpMethod = "GET", GroupName = "系统管理", SortOrder = 5 },
        new() { Code = "datadict.create", Name = "创建字典", ResourcePath = "/api/v1/data-dict", HttpMethod = "POST", GroupName = "系统管理", SortOrder = 6 },
        new() { Code = "datadict.edit", Name = "编辑字典", ResourcePath = "/api/v1/data-dict", HttpMethod = "PUT", GroupName = "系统管理", SortOrder = 7 },
        new() { Code = "datadict.delete", Name = "删除字典", ResourcePath = "/api/v1/data-dict", HttpMethod = "DELETE", GroupName = "系统管理", SortOrder = 8 },
        new() { Code = "jobs.list", Name = "任务列表", ResourcePath = "/api/v1/jobs", HttpMethod = "GET", GroupName = "系统管理", SortOrder = 9 },
        new() { Code = "jobs.manage", Name = "任务管理", ResourcePath = "/api/v1/jobs", HttpMethod = "POST", GroupName = "系统管理", SortOrder = 10 },
        new() { Code = "operation-logs.list", Name = "操作日志列表", ResourcePath = "/api/v1/operation-logs", HttpMethod = "GET", GroupName = "系统管理", SortOrder = 11 },
        new() { Code = "files.upload", Name = "文件管理", ResourcePath = "/api/v1/files", HttpMethod = "POST", GroupName = "系统管理", SortOrder = 12 },
        new() { Code = "tenants.list", Name = "租户列表", ResourcePath = "/api/v1/tenants", HttpMethod = "GET", GroupName = "多租户", SortOrder = 1 },
        new() { Code = "tenants.create", Name = "创建租户", ResourcePath = "/api/v1/tenants", HttpMethod = "POST", GroupName = "多租户", SortOrder = 2 },
        new() { Code = "tenants.edit", Name = "编辑租户", ResourcePath = "/api/v1/tenants", HttpMethod = "PUT", GroupName = "多租户", SortOrder = 3 },
        new() { Code = "tenants.delete", Name = "删除租户", ResourcePath = "/api/v1/tenants", HttpMethod = "DELETE", GroupName = "多租户", SortOrder = 4 },
        new() { Code = "tenant-params.list", Name = "租户参数列表", ResourcePath = "/api/v1/tenant-params", HttpMethod = "GET", GroupName = "多租户", SortOrder = 5 },
        new() { Code = "tenant-params.create", Name = "创建租户参数", ResourcePath = "/api/v1/tenant-params", HttpMethod = "POST", GroupName = "多租户", SortOrder = 6 },
        new() { Code = "tenant-params.edit", Name = "编辑租户参数", ResourcePath = "/api/v1/tenant-params", HttpMethod = "PUT", GroupName = "多租户", SortOrder = 7 },
        new() { Code = "tenant-params.delete", Name = "删除租户参数", ResourcePath = "/api/v1/tenant-params", HttpMethod = "DELETE", GroupName = "多租户", SortOrder = 8 },
        new() { Code = "org-units.list", Name = "组织架构列表", ResourcePath = "/api/v1/organization-units", HttpMethod = "GET", GroupName = "组织架构", SortOrder = 1 },
        new() { Code = "org-units.create", Name = "创建部门", ResourcePath = "/api/v1/organization-units", HttpMethod = "POST", GroupName = "组织架构", SortOrder = 2 },
        new() { Code = "org-units.edit", Name = "编辑部门", ResourcePath = "/api/v1/organization-units", HttpMethod = "PUT", GroupName = "组织架构", SortOrder = 3 },
        new() { Code = "org-units.delete", Name = "删除部门", ResourcePath = "/api/v1/organization-units", HttpMethod = "DELETE", GroupName = "组织架构", SortOrder = 4 },
        new() { Code = "menus.list", Name = "菜单列表", ResourcePath = "/api/v1/menus", HttpMethod = "GET", GroupName = "菜单管理", SortOrder = 1 },
        new() { Code = "menus.create", Name = "创建菜单", ResourcePath = "/api/v1/menus", HttpMethod = "POST", GroupName = "菜单管理", SortOrder = 2 },
        new() { Code = "menus.edit", Name = "编辑菜单", ResourcePath = "/api/v1/menus", HttpMethod = "PUT", GroupName = "菜单管理", SortOrder = 3 },
        new() { Code = "menus.delete", Name = "删除菜单", ResourcePath = "/api/v1/menus", HttpMethod = "DELETE", GroupName = "菜单管理", SortOrder = 4 },
        new() { Code = "notifications.manage", Name = "通知管理", ResourcePath = "/api/v1/notifications", HttpMethod = "POST", GroupName = "系统管理", SortOrder = 13 }
    ];

    private static (string RoleName, string[] PermCodes)[] GetSeedRolePermissions() =>
    [
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
        ("Manager", ["users.list", "roles.list", "perms.list"]),
        ("User", ["users.list"])
    ];

    private static string Norm(string value) => (value ?? string.Empty).ToUpperInvariant();

    /// <summary>初始化系统参数种子数据（幂等）</summary>
    private static async Task SeedSystemParamsAsync(IUnitOfWork uow, AppDbContext context, Guid createdBy)
    {
        var existingCodes = (await uow.Repository<SystemParam>().GetAllAsync())
            .Select(p => p.Code).ToHashSet();

        // 检查已软删除的记录避免唯一约束冲突
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
        new() { Code = "site_name",           Name = "站点名称",          Value = "PlatformBase",     Category = "general",  SortOrder = 1,  Description = "站点/应用名称，前端页面标题等位置使用" },
        new() { Code = "page_size",           Name = "默认分页大小",       Value = "20",                Category = "general",  SortOrder = 2,  Description = "列表分页的默认每页条数" },
        new() { Code = "max_login_attempts",  Name = "最大登录失败次数",    Value = "5",                 Category = "security", SortOrder = 1,  Description = "连续登录失败达到此次数后锁定账户" },
        new() { Code = "lockout_minutes",     Name = "锁定分钟数",         Value = "5",                 Category = "security", SortOrder = 2,  Description = "账户被锁定后自动解锁的分钟数" },
        new() { Code = "access_token_lifetime", Name = "Token有效期(秒)",  Value = "300",               Category = "security", SortOrder = 3,  Description = "AccessToken 签发的有效时长" },
        new() { Code = "refresh_token_days",  Name = "RefreshToken有效期(天)", Value = "30",             Category = "security", SortOrder = 4,  Description = "RefreshToken 的有效天数" },
        new() { Code = "enable_register",     Name = "开放注册",           Value = "true",              Category = "feature-toggle", SortOrder = 1,  Description = "是否允许新用户自行注册" },
        new() { Code = "enable_captcha",      Name = "验证码开关",         Value = "false",             Category = "feature-toggle", SortOrder = 2,  Description = "登录/注册时是否启用验证码校验" },
        new() { Code = "maintenance_mode",    Name = "维护模式",           Value = "false",             Category = "feature-toggle", SortOrder = 3,  Description = "开启后仅管理员可访问系统" },
        new() { Code = "smtp:default",        Name = "SMTP邮件配置",       Value = "{\"Host\":\"\",\"Port\":587,\"User\":\"\",\"Password\":\"\",\"From\":\"\"}", Category = "smtp", SortOrder = 1, Description = "SMTP 邮件服务器配置（JSON），为空则不发送邮件。支持多配置：smtp:alert/smtp:marketing" }
    ];

    /// <summary>初始化数据字典种子数据（幂等）</summary>
    private static async Task SeedDataDictAsync(IUnitOfWork uow, AppDbContext context, Guid createdBy)
    {
        var existingTypeCodes = (await uow.Repository<DataDictType>().GetAllAsync())
            .Select(t => t.TypeCode).ToHashSet();

        Guid? genderTypeId = null, userStatusTypeId = null, enabledStatusTypeId = null;

        foreach (var seed in GetSeedDictTypes())
        {
            if (existingTypeCodes.Contains(seed.TypeCode)) continue;
            seed.CreatedBy = createdBy;
            var created = await uow.Repository<DataDictType>().AddAsync(seed);
            await uow.SaveChangesAsync();

            if (seed.TypeCode == "gender") genderTypeId = created.Id;
            if (seed.TypeCode == "user_status") userStatusTypeId = created.Id;
            if (seed.TypeCode == "enabled_status") enabledStatusTypeId = created.Id;
        }

        // 查找已有类型的 ID（已存在的不会重新创建）
        var allTypes = await uow.Repository<DataDictType>().GetAllAsync();
        genderTypeId ??= allTypes.FirstOrDefault(t => t.TypeCode == "gender")?.Id;
        userStatusTypeId ??= allTypes.FirstOrDefault(t => t.TypeCode == "user_status")?.Id;
        enabledStatusTypeId ??= allTypes.FirstOrDefault(t => t.TypeCode == "enabled_status")?.Id;

        foreach (var seed in GetSeedDictItems(genderTypeId, userStatusTypeId, enabledStatusTypeId))
        {
            if (seed.DictTypeId == Guid.Empty) continue;
            // 使用 IgnoreQueryFilters 跳过软删除全局过滤器，确保幂等检查覆盖已删除记录
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
        new() { TypeCode = "gender",         TypeName = "性别",       SortOrder = 1,  Description = "用户性别选项" },
        new() { TypeCode = "user_status",    TypeName = "用户状态",   SortOrder = 2,  Description = "系统用户状态枚举" },
        new() { TypeCode = "enabled_status", TypeName = "启用状态",   SortOrder = 3,  Description = "通用启用/禁用状态" }
    ];

    private static DataDictItem[] GetSeedDictItems(
        Guid? genderTypeId, Guid? userStatusTypeId, Guid? enabledStatusTypeId) =>
    [
        // gender
        new() { DictTypeId = genderTypeId ?? Guid.Empty, ItemCode = "male",   ItemName = "男",   SortOrder = 1 },
        new() { DictTypeId = genderTypeId ?? Guid.Empty, ItemCode = "female", ItemName = "女",   SortOrder = 2 },
        new() { DictTypeId = genderTypeId ?? Guid.Empty, ItemCode = "other",  ItemName = "其他", SortOrder = 3 },
        // user_status
        new() { DictTypeId = userStatusTypeId ?? Guid.Empty, ItemCode = "activated",   ItemName = "正常",   SortOrder = 1 },
        new() { DictTypeId = userStatusTypeId ?? Guid.Empty, ItemCode = "inactivated", ItemName = "未激活", SortOrder = 2 },
        new() { DictTypeId = userStatusTypeId ?? Guid.Empty, ItemCode = "disabled",    ItemName = "停用",   SortOrder = 3 },
        new() { DictTypeId = userStatusTypeId ?? Guid.Empty, ItemCode = "locked",      ItemName = "已锁定", SortOrder = 4 },
        // enabled_status
        new() { DictTypeId = enabledStatusTypeId ?? Guid.Empty, ItemCode = "enabled",  ItemName = "启用", SortOrder = 1 },
        new() { DictTypeId = enabledStatusTypeId ?? Guid.Empty, ItemCode = "disabled", ItemName = "禁用", SortOrder = 2 }
    ];

    /// <summary>初始化通知模板种子数据（幂等）</summary>
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
        new() { Code = "welcome",      Name = "欢迎注册",   TitleTemplate = "欢迎加入 {siteName}",
                 BodyTemplate = "尊敬的 {username}：您已成功注册，请登录系统完成信息设置。",
                 Channel = "in_app", Variables = "username,siteName" },
        new() { Code = "password_changed", Name = "密码已修改", TitleTemplate = "密码修改通知",
                 BodyTemplate = "尊敬的用户：您的登录密码已于 {time} 被修改。如非本人操作，请联系管理员。",
                 Channel = "in_app", Variables = "time" },
        new() { Code = "account_locked", Name = "账户已锁定", TitleTemplate = "账户锁定通知",
                 BodyTemplate = "尊敬的用户：您的账户因 {failedCount} 次登录失败已被锁定 {lockMinutes} 分钟。",
                 Channel = "in_app", Variables = "failedCount,lockMinutes" }
    ];

    /// <summary>初始化多租户种子数据（幂等）</summary>
    private static async Task SeedTenantsAsync(IUnitOfWork uow, AppDbContext context, Guid createdBy)
    {
        // 默认租户
        var tenantExists = await uow.Repository<Tenant>().AnyAsync(t => t.Code == "default");
        if (!tenantExists)
        {
            await uow.Repository<Tenant>().AddAsync(new Tenant
            {
                Code = "default", Name = "默认租户", ContactEmail = "tenant@platformbase.com",
                IsEnabled = true, CreatedBy = createdBy
            });
            await uow.SaveChangesAsync();
        }

        var defaultTenant = await uow.Repository<Tenant>()
            .FirstOrDefaultAsync(t => t.Code == "default");
        if (defaultTenant == null) return;

        // 更新 admin 为平台管理员
        var admin = await uow.Repository<User>()
            .FirstOrDefaultAsync(u => u.NormalizedUsername == Norm("admin"));
        if (admin != null && admin.UserType != UserType.PlatformAdmin)
        {
            admin.UserType = UserType.PlatformAdmin;
            uow.Repository<User>().Update(admin);
            await uow.SaveChangesAsync();
        }

        // 映射 admin → 默认租户
        if (admin != null)
        {
            var exists = await context.Set<PlatformUserTenant>()
                .AnyAsync(p => p.PlatformUserId == admin.Id && p.TenantId == defaultTenant.Id);
            if (!exists)
                context.Set<PlatformUserTenant>().Add(
                    new PlatformUserTenant { PlatformUserId = admin.Id, TenantId = defaultTenant.Id });
        }

        // 更新 testuser 为租户用户（归属默认租户）
        var testuser = await uow.Repository<User>()
            .FirstOrDefaultAsync(u => u.NormalizedUsername == Norm("testuser"));
        if (testuser != null)
        {
            if (testuser.TenantId != defaultTenant.Id || testuser.UserType != UserType.TenantUser)
            {
                testuser.TenantId = defaultTenant.Id;
                testuser.UserType = UserType.TenantUser;
                uow.Repository<User>().Update(testuser);
            }
        }

        await uow.SaveChangesAsync();
    }

    /// <summary>初始化菜单种子数据（幂等）</summary>
    private static async Task SeedMenusAsync(IUnitOfWork uow, Guid createdBy)
    {
        var existing = (await uow.Repository<Menu>().GetAllAsync());
        if (existing.Count > 0) return;

        // ═══════════════ 一级菜单（目录，Type=1）═══════════════
        var sysMenu  = await AddMenu(uow, createdBy, new Menu { Name = "系统管理", Type = 1, Icon = "IconSettings",    SortOrder = 100 });
        var confMenu = await AddMenu(uow, createdBy, new Menu { Name = "系统配置", Type = 1, Icon = "IconTool",   SortOrder = 200 });
        var monMenu  = await AddMenu(uow, createdBy, new Menu { Name = "系统监控", Type = 1, Icon = "IconActivity",   SortOrder = 300 });

        // ═══════════════ 系统管理 → 子菜单（Type=2 页面）═══════════════
        await AddMenu(uow, createdBy, new Menu { ParentId = sysMenu.Id, Name = "用户管理", Type = 2, Icon = "IconUsers",       Path = "/users",              PermissionCode = "users.list",          SortOrder = 1, KeepAlive = true });
        await AddMenu(uow, createdBy, new Menu { ParentId = sysMenu.Id, Name = "角色管理", Type = 2, Icon = "IconUserShield",  Path = "/roles",              PermissionCode = "roles.list",          SortOrder = 2, KeepAlive = true });
        await AddMenu(uow, createdBy, new Menu { ParentId = sysMenu.Id, Name = "权限管理", Type = 2, Icon = "IconLock",        Path = "/permissions",        PermissionCode = "perms.list",          SortOrder = 3, KeepAlive = true });
        await AddMenu(uow, createdBy, new Menu { ParentId = sysMenu.Id, Name = "组织架构", Type = 2, Icon = "IconShare",       Path = "/organization-units", PermissionCode = "org-units.list",      SortOrder = 4, KeepAlive = true });
        await AddMenu(uow, createdBy, new Menu { ParentId = sysMenu.Id, Name = "租户管理", Type = 2, Icon = "IconBuilding",    Path = "/tenants",            PermissionCode = "tenants.list",        SortOrder = 5, KeepAlive = true });

        // ═══════════════ 系统配置 → 子菜单（Type=2 页面）═══════════════
        await AddMenu(uow, createdBy, new Menu { ParentId = confMenu.Id, Name = "菜单管理", Type = 2, Icon = "IconMenu2",        Path = "/menus",            PermissionCode = "menus.list",          SortOrder = 1, KeepAlive = true });
        await AddMenu(uow, createdBy, new Menu { ParentId = confMenu.Id, Name = "系统参数", Type = 2, Icon = "IconSettingsCog",  Path = "/system-params",    PermissionCode = "system-params.list",  SortOrder = 2, KeepAlive = true });
        await AddMenu(uow, createdBy, new Menu { ParentId = confMenu.Id, Name = "数据字典", Type = 2, Icon = "IconBooks",        Path = "/data-dict",        PermissionCode = "datadict.list",       SortOrder = 3, KeepAlive = true });
        await AddMenu(uow, createdBy, new Menu { ParentId = confMenu.Id, Name = "文件管理", Type = 2, Icon = "IconFolders",      Path = "/files",            PermissionCode = "files.upload",        SortOrder = 4, KeepAlive = true });

        // ═══════════════ 系统监控 → 子菜单（Type=2 页面）═══════════════
        await AddMenu(uow, createdBy, new Menu { ParentId = monMenu.Id, Name = "操作日志", Type = 2, Icon = "IconFileText",  Path = "/operation-logs",   PermissionCode = "operation-logs.list", SortOrder = 1, KeepAlive = true });
        await AddMenu(uow, createdBy, new Menu { ParentId = monMenu.Id, Name = "定时任务", Type = 2, Icon = "IconClock",     Path = "/jobs",              PermissionCode = "jobs.list",           SortOrder = 2, KeepAlive = true });

        await uow.SaveChangesAsync();
    }

    private static async Task<Menu> AddMenu(IUnitOfWork uow, Guid createdBy, Menu menu)
    {
        menu.CreatedBy = createdBy;
        return await uow.Repository<Menu>().AddAsync(menu);
    }
}
