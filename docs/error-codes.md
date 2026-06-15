# 错误码参考 / Error Code Reference

## 响应格式

所有响应统一 HTTP 200，错误信息在 `ApiResult` body 中：

```json
{
  "success": false,
  "code": 1006,
  "message": "用户不存在",
  "traceId": null
}
```

## 错误码分段

| 范围 | 类别 | 说明 |
|------|------|------|
| -1 | 未知错误 | |
| 400–422 | HTTP 标准码 | 请求参数、认证、权限、资源、验证 |
| 500 | 服务器错误 | |
| 1001–1010 | 业务错误（通用/认证） | 重复记录、数据不存在、令牌、用户、密码、锁定、频控、权限拒绝 |
| 1011–1020 | 业务错误（租户/部门/角色校验） | 租户必填、无权访问、部门/角色归属校验 |
| 2001 | 基础设施错误 | 数据库错误 |
| 3001 | 外部服务错误 | 外部服务调用失败 |

## 完整参考

### 通用错误

| 错误码 | 常量名 | 说明 | 使用场景 |
|:---:|------|------|------|
| -1 | `Unknown` | 未知错误 | 兜底 |
| 400 | `BadRequest` | 请求参数错误 | ModelState 校验失败 / 参数缺失 |
| 401 | `Unauthorized` | 未认证 | Token 无效/过期/缺失 |
| 403 | `Forbidden` | 无权限访问 | 角色不具备所需权限 |
| 404 | `NotFound` | 资源不存在 | URL 路径不匹配 |
| 409 | `Conflict` | 数据冲突 | 并发修改冲突 |
| 422 | `ValidationFailed` | 模型验证失败 | 输入数据格式非法 |
| 500 | `InternalError` | 服务器内部错误 | 未处理异常兜底 |

### 业务错误 — 通用/认证（1000 起）

| 错误码 | 常量名 | 说明 | 使用场景 |
|:---:|------|------|------|
| 1001 | `DuplicateRecord` | 重复记录 | 用户名/角色名/参数编码已存在时抛出 |
| 1002 | `DataNotFound` | 数据不存在 | 查询的资源不存在 |
| 1003 | `InvalidOperation` | 非法操作 | 操作不被允许（如删除有关联的角色） |
| 1004 | `TokenExpired` | 令牌已过期 | RefreshToken 已过期 |
| 1005 | `TokenInvalid` | 令牌无效 | RefreshToken 不合法 |
| 1006 | `UserNotFound` | 用户不存在 | 登录用户名不存在 / 查询的用户不存在 |
| 1007 | `PasswordMismatch` | 密码错误 | 登录 / 修改密码时密码不匹配 |
| 1008 | `UserLocked` | 用户已被锁定 | 登录时账户处于锁定状态 |
| 1009 | `TooManyRequests` | 请求过于频繁 | 登录频控触发 / API 限流 |
| 1010 | `PermissionDenied` | 权限拒绝 | 权限校验未通过 |

### 业务错误 — 租户/部门/角色校验（1011–1020）

| 错误码 | 常量名 | 说明 | 使用场景 |
|:---:|------|------|------|
| 1011 | `TenantIdRequired` | 必须指定租户 | 平台管理员操作时必须指定租户ID |
| 1012 | `TenantAccessDenied` | 无权访问指定租户 | 平台管理员尝试访问未分配的租户 |
| 1013 | `OrganizationRequired` | 部门必选 | 创建用户时未指定部门 |
| 1014 | `RoleRequired` | 角色必选 | 创建用户时未指定角色 |
| 1015 | `OrganizationNotInTenant` | 部门不属于目标租户 | 指定部门与目标租户不匹配 |
| 1016 | `RoleNotInTenant` | 角色不属于目标租户 | 指定角色与目标租户不匹配 |
| 1017 | `CannotCreatePlatformAdmin` | 不能创建平台管理员 | 平台管理员不允许通过API创建同级 |
| 1018 | `CanOnlyCreateTenantUser` | 只能创建租户普通用户 | 租户管理员只能创建 TenantUser |
| 1019 | `ParentOrgNotInTenant` | 父部门不属于当前租户 | 创建子部门/菜单时父级归属校验 |
| 1020 | `NoPermissionToOperate` | 无权执行此操作 | 被禁止的用户类型尝试操作 |

### 基础设施错误（2000 起）

| 错误码 | 常量名 | 说明 |
|:---:|------|------|
| 2001 | `DatabaseError` | 数据库连接或操作异常 |

### 外部服务错误（3000 起）

| 错误码 | 常量名 | 说明 |
|:---:|------|------|
| 3001 | `ExternalServiceError` | 外部服务调用失败（预留：邮件/短信/OSS） |

## 客户端处理建议

```javascript
// 前端统一响应拦截器
if (!response.success) {
  switch (response.code) {
    case 401:  // 未认证 → 跳转登录页
      router.push('/login');
      break;
    case 403:  // 无权限 → 提示
      message.error('无权限访问');
      break;
    case 1004: // Token 过期 → 刷新 Token 或跳登录
    case 1005:
      refreshToken();
      break;
    case 1006: // 用户名或密码错误 → 表单提示
    case 1007:
      form.setError(response.message);
      break;
    case 1009: // 频控 → 提示稍后
      message.warning(response.message);
      break;
    case 1011: // 租户必填 → 提示选择租户
    case 1012:
      message.warning(response.message);
      break;
    case 1013: // 部门必选 / 角色必选
    case 1014:
      message.warning(response.message);
      break;
    case 1015: // 部门/角色归属不匹配
    case 1016:
      message.error(response.message);
      break;
    default:
      message.error(response.message || '操作失败');
  }
}
```
