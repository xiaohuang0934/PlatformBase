namespace PlatformBase.Core;

/// <summary>
/// 支持的数据库类型，通过appsettings.json的Database:Provider配置切换
/// </summary>
public enum DatabaseProvider
{
    /// <summary>SQLite，默认值，适用于开发和单机部署</summary>
    Sqlite,

    /// <summary>Microsoft SQL Server</summary>
    SqlServer,

    /// <summary>MySQL，使用Pomelo.EntityFrameworkCore.MySql驱动</summary>
    MySql
}
