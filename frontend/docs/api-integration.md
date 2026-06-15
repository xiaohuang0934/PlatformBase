# API 对接规范 / API Integration

## 统一响应格式

所有后端 API 返回统一格式：

```json
{
  "success": true,
  "code": 200,
  "message": "success",
  "data": {},
  "traceId": null
}
```

## axios 实例

`src/api/index.ts` 统一封装：

- **baseURL**: `/api/v1`
- **请求拦截器**: 自动附加 `Authorization: Bearer {token}`，token 快过期时自动静默刷新
- **响应拦截器**: 统一错误处理 + 401 自动刷新重试

## API 函数规范

```ts
// src/api/users.ts
import type { ApiResult, PagedResult } from '@/types/api-result'
import http from './index'

/**
 * 分页查询
 * @param params keyword + 后端 DTO 字段（camelCase）
 */
export function getUserList(params?: {
  keyword?: string
  isActive?: boolean
  pageIndex?: number
  pageSize?: number
  tenantIds?: string[]       // v1.8: 平台用户多租户查询
}): Promise<ApiResult<PagedResult<UserDto>>> {
  return http.get('/users', { params }).then(res => res.data)
}
```

### 字段命名

- 请求参数使用 **camelCase**（与后端 `PropertyNamingPolicy.CamelCase` 一致）
- 后端 PascalCase 字段自动转换为 camelCase

## 常见响应类型

| 后端返回 | 前端类型 |
|---------|---------|
| `PagedResult<T>` | `ApiResult<PagedResult<T>>` |
| `IReadOnlyList<T>` | `ApiResult<T[]>` |
| 单个对象 | `ApiResult<T>` |
| 无返回 | `ApiResult<null>` |

## 错误处理

响应拦截器统一处理：
- 业务错误（`success: false`）→ `ElMessage.error`
- 401 → 自动刷新 token → 失败 → 跳转登录
- 403 → `ElMessage.error('无权限访问')`
- 网络错误 → `ElMessage.error('网络异常')`

## 文件下载

使用 `src/utils/download.ts` 的 `downloadFile(url, filename)` 函数，通过 axios blob 下载，自动携带 token：

```ts
import { downloadFile } from '@/utils/download'
downloadFile('/api/v1/files/{id}/download', 'file.pdf')
```

## v1.8 变更摘要

### 新增 API 端点

| 端点 | 方法 | 说明 |
|------|------|------|
| `GET /users/{id}/organizations` | GET | 查看用户所属部门 |
| `PUT /users/{id}/organizations` | PUT | 分配用户部门（全量替换） |
| `POST /users/{id}/organizations/{orgId}` | POST | 添加用户到部门 |
| `DELETE /users/{id}/organizations/{orgId}` | DELETE | 将用户移出部门 |
| `GET /organization-units/{id}/users` | GET | 查询部门下的用户 |
| `GET /organization-units/{id}/users/with-children` | GET | 分页查询部门及子级用户 |
| `GET /tenants/accessible` | GET | 当前平台用户可访问租户列表 |

### DTO 字段变更

| DTO | 新增字段 | 类型 | 必填 |
|-----|---------|------|:--:|
| `CreateUserDto` | `tenantId` | `Guid?` | 平台管理员必填 |
| `CreateUserDto` | `userType` | `int?` | 否 |
| `CreateUserDto` | `roleIds` | `Guid[]` | **是**（变更） |
| `CreateUserDto` | `organizationUnitIds` | `Guid[]` | **是** |
| `UpdateUserDto` | `userType` | `int?` | 否 |
| `UpdateUserDto` | `organizationUnitIds` | `Guid[]` | 否 |
| `UserDto` | `userType` | `int` | — |
| `UserDto` | `organizationUnits` | `OrgUnitNode[]` | — |
| `CreateRoleDto` | `tenantId` | `Guid?` | 否（null=全局角色） |

### 查询参数变更

| 端点 | 新参数 | 说明 |
|------|-------|------|
| `GET /users` | `tenantIds` (query) | 平台用户指定查询的租户列表 |
| `GET /roles` | `tenantIds` (query) | 平台用户指定查询的租户列表 |
| `GET /import-export/users` | `tenantId` (query) | 按租户过滤导出 |
| `POST /import-export/users` | `tenantId` (query) | 指定导入目标租户 |

### 关键差异

- `profile` 响应不再包含 `email` / `roles` 字段（JWT 简化）
- `login` 响应的 JWT 不再内嵌 `roles` / `email` / `tenant_id` Claim
- 用户详情 **必须** 通过 `GET /users/{id}` 获取完整信息
- 所有操作需先获取 `tenantIds` 列表，再提供给查询类端点

---

> **后端 API 参考**：[docs/api-reference.md](../../docs/api-reference.md) — 全量 API 端点表（方法/路径/权限/说明）
