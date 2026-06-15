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
export function getUserList(params?: { keyword?: string, isActive?: boolean, pageIndex?: number, pageSize?: number }): Promise<ApiResult<PagedResult<UserDto>>> {
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

---

> **后端 API 参考**：[docs/api-reference.md](../../docs/api-reference.md) — 全量 API 端点表（方法/路径/权限/说明）
